using EarSaxophone.Core.Localization;
using System.Runtime.InteropServices;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Mapping;
using NAudio.CoreAudioApi;

namespace EarSaxophone.Windows.Audio;

/// <summary>
/// Windows Core Audio via NAudio. Devices and sessions are cached: the cache is rebuilt when Windows reports a
/// device or default-device change, session lists are refreshed when a session is created and once per second as a
/// safety net (apps that exit or restart do not always announce it). <see cref="Changed"/> is raised only when
/// the set of devices or sessions actually changed. Compiled in CI; behaviour with real devices must be verified locally.
/// </summary>
public sealed class NAudioAudioSystem : IAudioSystem, IDisposable
{
    private const int DeviceInvalidated = unchecked((int)0x88890004);
    private const int ElementNotFound = unchecked((int)0x80070490);

    private readonly object _gate = new();
    private readonly Timer _timer;
    private readonly Dictionary<(DataFlow, Role), string?> _defaultIds = [];
    private MMDeviceEnumerator? _enumerator;
    private MMDeviceNotificationClient? _notifications;
    private Dictionary<string, (MMDevice Device, DeviceFlow Flow)> _devices = [];
    private Dictionary<string, NAudioEndpoint> _endpoints = [];
    private Dictionary<string, NAudioSession> _sessions = [];
    private bool _disposed;
    private bool _needsRebuild = true;
    private int _rebuildScheduled;

    public NAudioAudioSystem()
    {
        lock (_gate)
        {
            TryRebuildLocked();
        }

        _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public event Action? Changed;

    /// <summary>Why the Windows audio service could not be used right now, or null. The system keeps retrying on its own.</summary>
    public string? Problem { get; private set; }

    public IAudioEndpoint? GetDefaultEndpoint(DeviceFlow flow, Core.Mapping.DeviceRole role)
    {
        DataFlow dataFlow = ToDataFlow(flow);
        Role naudioRole = ToRole(role);
        lock (_gate)
        {
            if (_enumerator is null)
            {
                return null;
            }

            if (!_defaultIds.TryGetValue((dataFlow, naudioRole), out string? id))
            {
                id = null;
                try
                {
                    if (_enumerator.TryGetDefaultAudioEndpoint(dataFlow, naudioRole, out MMDevice? device) && device is not null)
                    {
                        id = device.ID;
                        device.Dispose();
                    }
                }
                catch (Exception)
                {
                    _needsRebuild = true;
                    return null;
                }

                _defaultIds[(dataFlow, naudioRole)] = id;
            }

            return id is null ? null : GetEndpointLocked(id);
        }
    }

    public IAudioEndpoint? GetEndpoint(string deviceId)
    {
        lock (_gate)
        {
            return GetEndpointLocked(deviceId);
        }
    }

    public IReadOnlyList<IAudioEndpoint> GetEndpoints(DeviceFlow flow)
    {
        lock (_gate)
        {
            return _devices
                .Where(entry => entry.Value.Flow == flow)
                .Select(entry => GetEndpointLocked(entry.Key))
                .OfType<IAudioEndpoint>()
                .ToList();
        }
    }

    public void SetDefaultEndpoint(string deviceId, DeviceFlow flow)
    {
        lock (_gate)
        {
            if (!_devices.TryGetValue(deviceId, out var entry) || entry.Flow != flow)
            {
                throw new AudioGoneException(Texts.DeviceOrSessionGone);
            }
        }

        // Called outside the lock: Windows answers with change notifications that start a rebuild on another thread.
        PolicyConfig.SetDefaultForAllRoles(deviceId);
        lock (_gate)
        {
            _defaultIds.Clear();
        }
    }

    public IReadOnlyList<IAudioSession> GetSessions()
    {
        lock (_gate)
        {
            return _sessions.Values.ToList<IAudioSession>();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _timer.Dispose();
        _notifications?.Dispose();
        lock (_gate)
        {
            ClearCaches();
        }

        _enumerator?.Dispose();
    }

    private NAudioEndpoint? GetEndpointLocked(string id)
    {
        if (_endpoints.TryGetValue(id, out NAudioEndpoint? cached))
        {
            return cached;
        }

        if (!_devices.TryGetValue(id, out var entry))
        {
            return null;
        }

        try
        {
            var endpoint = new NAudioEndpoint(this, entry.Device, entry.Flow);
            _endpoints[id] = endpoint;
            return endpoint;
        }
        catch (Exception)
        {
            _needsRebuild = true;
            return null;
        }
    }

    // Windows reports one plugged-in headset as a burst of notifications (added, state, up to six default-device changes).
    // They are collapsed into a single rebuild shortly after the burst.
    private void TopologyChanged()
    {
        if (Interlocked.Exchange(ref _rebuildScheduled, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(250).ConfigureAwait(false);
            Interlocked.Exchange(ref _rebuildScheduled, 0);
            bool rebuilt;
            try
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    rebuilt = TryRebuildLocked();
                }
            }
            catch (Exception)
            {
                return;
            }

            if (rebuilt)
            {
                RaiseChanged();
            }
        });
    }

    private void Tick()
    {
        bool changed;
        try
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                // A failed or partial rebuild, or a device error during refresh, sets _needsRebuild: retry from scratch.
                changed = _needsRebuild ? TryRebuildLocked() : RefreshSessionsLocked();
            }
        }
        catch (Exception)
        {
            lock (_gate)
            {
                _needsRebuild = true;
            }

            return;
        }

        if (changed)
        {
            RaiseChanged();
        }
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception)
        {
            // A faulty subscriber must never take down the process from a thread-pool thread.
        }
    }

    private void EnsureEnumeratorLocked()
    {
        if (_enumerator is not null)
        {
            return;
        }

        var enumerator = new MMDeviceEnumerator();
        try
        {
            MMDeviceNotificationClient notifications = enumerator.CreateNotificationClient(useSynchronizationContext: false);
            notifications.DeviceAdded += (_, _) => TopologyChanged();
            notifications.DeviceRemoved += (_, _) => TopologyChanged();
            notifications.DeviceStateChanged += (_, _) => TopologyChanged();
            notifications.DefaultDeviceChanged += (_, _) => TopologyChanged();
            _notifications = notifications;
            _enumerator = enumerator;
        }
        catch
        {
            enumerator.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Builds the device list anew. The new state replaces the old one only when it was built completely; on any error the
    /// old state stays in use, <see cref="Problem"/> is set and the next timer tick tries again. Caller holds the lock.
    /// </summary>
    private bool TryRebuildLocked()
    {
        var devices = new Dictionary<string, (MMDevice Device, DeviceFlow Flow)>();
        try
        {
            EnsureEnumeratorLocked();
            foreach ((DataFlow dataFlow, DeviceFlow flow) in new[] { (DataFlow.Render, DeviceFlow.Render), (DataFlow.Capture, DeviceFlow.Capture) })
            {
                using MMDeviceCollection collection = _enumerator!.EnumerateAudioEndPoints(dataFlow, DeviceState.Active);
                foreach (MMDevice device in collection)
                {
                    devices[device.ID] = (device, flow);
                    try
                    {
                        device.AudioSessionManager.OnSessionCreated += OnSessionCreated;
                    }
                    catch (COMException)
                    {
                        // Device without session manager: only endpoint control is possible.
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DisposeDevices(devices);
            _needsRebuild = true;
            Problem = Texts.AudioUnavailable(ex.Message);
            return false;
        }

        ClearCaches();
        _devices = devices;
        _needsRebuild = false;
        Problem = null;
        RefreshSessionsLocked();
        return true;
    }

    private void ClearCaches()
    {
        foreach (NAudioSession session in _sessions.Values)
        {
            session.Dispose();
        }

        foreach (NAudioEndpoint endpoint in _endpoints.Values)
        {
            endpoint.MarkDisposed();
        }

        _sessions = [];
        _endpoints = [];
        _defaultIds.Clear();
        DisposeDevices(_devices);
        _devices = [];
    }

    private void DisposeDevices(Dictionary<string, (MMDevice Device, DeviceFlow Flow)> devices)
    {
        foreach ((MMDevice device, _) in devices.Values)
        {
            try
            {
                device.AudioSessionManager.OnSessionCreated -= OnSessionCreated;
            }
            catch (Exception)
            {
            }

            try
            {
                device.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }

    // Called on a Windows audio worker thread that holds an internal lock: never do COM work here.
    private void OnSessionCreated(object sender, object newSession) => _ = Task.Run(Tick);

    /// <summary>Re-reads all session lists; returns true when sessions appeared or disappeared. Caller holds the lock.</summary>
    private bool RefreshSessionsLocked()
    {
        var fresh = new Dictionary<string, NAudioSession>();
        foreach ((string deviceId, (MMDevice device, DeviceFlow flow)) in _devices)
        {
            try
            {
                AudioSessionManager manager = device.AudioSessionManager;
                manager.RefreshSessions();
                using SessionCollection collection = manager.Sessions;
                for (int i = 0; i < collection.Count; i++)
                {
                    AudioSessionControl control = collection[i];
                    string key = $"{deviceId}|{control.GetSessionInstanceIdentifier}";
                    if (fresh.ContainsKey(key))
                    {
                        DisposeQuietly(control);
                        continue;
                    }

                    if (_sessions.TryGetValue(key, out NAudioSession? existing))
                    {
                        DisposeQuietly(control);
                        fresh[key] = existing;
                        continue;
                    }

                    int pid = (int)control.GetProcessID;
                    var info = new AudioSessionInfo(key, pid, ProcessPath.TryGet(pid), deviceId, flow, control.IsSystemSoundsSession);
                    fresh[key] = new NAudioSession(this, control, info);
                }
            }
            catch (Exception)
            {
                // A temporary error on one device must not make its sessions look closed (they would be treated as new
                // and get the remembered value again). Keep what we knew and rebuild from scratch on the next tick.
                foreach ((string key, NAudioSession previous) in _sessions.Where(kv => kv.Value.Info.DeviceId == deviceId))
                {
                    fresh.TryAdd(key, previous);
                }

                _needsRebuild = true;
            }
        }

        bool changed = !fresh.Keys.ToHashSet().SetEquals(_sessions.Keys);
        foreach ((string key, NAudioSession old) in _sessions)
        {
            if (!fresh.ContainsKey(key))
            {
                old.Dispose();
            }
        }

        _sessions = fresh;
        return changed;
    }

    private static void DisposeQuietly(IDisposable disposable)
    {
        try
        {
            disposable.Dispose();
        }
        catch (Exception)
        {
        }
    }

    private static DataFlow ToDataFlow(DeviceFlow flow) => flow == DeviceFlow.Capture ? DataFlow.Capture : DataFlow.Render;

    private static Role ToRole(Core.Mapping.DeviceRole role) => role switch
    {
        Core.Mapping.DeviceRole.Communications => Role.Communications,
        Core.Mapping.DeviceRole.Multimedia => Role.Multimedia,
        _ => Role.Console,
    };

    internal static Exception Translate(COMException ex) => ex.HResult switch
    {
        DeviceInvalidated or ElementNotFound => new AudioGoneException(Texts.DeviceOrSessionGone, ex),
        _ => new AudioUnsupportedException(Texts.WindowsReported(ex.HResult), ex),
    };

    /// <summary>
    /// All calls into a COM wrapper run under the system lock, the same lock under which wrappers are disposed during a
    /// rebuild, so a wrapper can never be released while another thread is inside a call on it.
    /// </summary>
    private static T Guarded<T>(NAudioAudioSystem owner, ref bool disposed, Func<T> action)
    {
        lock (owner._gate)
        {
            if (disposed)
            {
                throw new AudioGoneException(Texts.DeviceOrSessionGone);
            }

            try
            {
                return action();
            }
            catch (COMException ex)
            {
                throw Translate(ex);
            }
            catch (ObjectDisposedException ex)
            {
                throw new AudioGoneException(Texts.DeviceOrSessionGone, ex);
            }
        }
    }

    private sealed class NAudioEndpoint(NAudioAudioSystem owner, MMDevice device, DeviceFlow flow) : IAudioEndpoint
    {
        private bool _disposed;

        public string Id { get; } = device.ID;

        public string Name { get; } = device.FriendlyName;

        public DeviceFlow Flow { get; } = flow;

        public float Volume
        {
            get => Guarded(owner, ref _disposed, () => device.AudioEndpointVolume.MasterVolumeLevelScalar);
            set => Guarded(owner, ref _disposed, () => device.AudioEndpointVolume.MasterVolumeLevelScalar = value);
        }

        public bool Muted
        {
            get => Guarded(owner, ref _disposed, () => device.AudioEndpointVolume.Mute);
            set => Guarded(owner, ref _disposed, () => device.AudioEndpointVolume.Mute = value);
        }

        public void MarkDisposed() => _disposed = true;
    }

    private sealed class NAudioSession(NAudioAudioSystem owner, AudioSessionControl control, AudioSessionInfo info) : IAudioSession, IDisposable
    {
        private bool _disposed;

        public AudioSessionInfo Info { get; } = info;

        public float Volume
        {
            get => Guarded(owner, ref _disposed, () => control.SimpleAudioVolume.Volume);
            set => Guarded(owner, ref _disposed, () => control.SimpleAudioVolume.Volume = value);
        }

        public bool Muted
        {
            get => Guarded(owner, ref _disposed, () => control.SimpleAudioVolume.Mute);
            set => Guarded(owner, ref _disposed, () => control.SimpleAudioVolume.Mute = value);
        }

        public AudioSessionActivity Activity
        {
            get
            {
                try
                {
                    return Guarded(owner, ref _disposed, () => control.State) == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive
                        ? AudioSessionActivity.Active
                        : AudioSessionActivity.Inactive;
                }
                catch (Exception)
                {
                    return AudioSessionActivity.Unknown;
                }
            }
        }

        /// <summary>Called by the owner while it holds the system lock.</summary>
        public void Dispose()
        {
            _disposed = true;
            DisposeQuietly(control);
        }
    }
}
