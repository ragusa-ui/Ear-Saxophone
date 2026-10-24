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
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDeviceNotificationClient _notifications;
    private readonly Timer _timer;
    private readonly Dictionary<string, (MMDevice Device, DeviceFlow Flow)> _devices = [];
    private readonly Dictionary<(DataFlow, Role), string?> _defaultIds = [];
    private readonly Dictionary<string, NAudioEndpoint> _endpoints = [];
    private Dictionary<string, NAudioSession> _sessions = [];
    private bool _disposed;

    public NAudioAudioSystem()
    {
        _notifications = _enumerator.CreateNotificationClient(useSynchronizationContext: false);
        _notifications.DeviceAdded += (_, _) => TopologyChanged();
        _notifications.DeviceRemoved += (_, _) => TopologyChanged();
        _notifications.DeviceStateChanged += (_, _) => TopologyChanged();
        _notifications.DefaultDeviceChanged += (_, _) => TopologyChanged();

        Rebuild();
        _timer = new Timer(_ => Refresh(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public event Action? Changed;

    public IAudioEndpoint? GetDefaultEndpoint(DeviceFlow flow, Core.Mapping.DeviceRole role)
    {
        DataFlow dataFlow = ToDataFlow(flow);
        Role naudioRole = ToRole(role);
        lock (_gate)
        {
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
                catch (COMException)
                {
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
        _notifications.Dispose();
        lock (_gate)
        {
            ClearCaches();
        }

        _enumerator.Dispose();
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

        var endpoint = new NAudioEndpoint(entry.Device, entry.Flow);
        _endpoints[id] = endpoint;
        return endpoint;
    }

    private void TopologyChanged()
    {
        Task.Run(() =>
        {
            try
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    Rebuild();
                }

                Changed?.Invoke();
            }
            catch (Exception)
            {
                // The next timer tick retries.
            }
        });
    }

    private void Refresh()
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

                changed = RefreshSessionsLocked();
            }
        }
        catch (Exception)
        {
            return;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Rebuilds devices, endpoint cache and sessions from scratch. Caller holds the lock (or is the constructor).</summary>
    private void Rebuild()
    {
        ClearCaches();
        foreach ((DataFlow dataFlow, DeviceFlow flow) in new[] { (DataFlow.Render, DeviceFlow.Render), (DataFlow.Capture, DeviceFlow.Capture) })
        {
            foreach (MMDevice device in _enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active))
            {
                _devices[device.ID] = (device, flow);
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

        RefreshSessionsLocked();
    }

    private void ClearCaches()
    {
        foreach (NAudioSession session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions = [];
        _endpoints.Clear();
        _defaultIds.Clear();
        foreach ((MMDevice device, _) in _devices.Values)
        {
            try
            {
                device.AudioSessionManager.OnSessionCreated -= OnSessionCreated;
            }
            catch (Exception)
            {
            }

            device.Dispose();
        }

        _devices.Clear();
    }

    // Called on a Windows audio worker thread that holds an internal lock: never do COM work here.
    private void OnSessionCreated(object sender, object newSession) => Task.Run(Refresh);

    /// <summary>Re-reads all session lists; returns true when sessions appeared or disappeared.</summary>
    private bool RefreshSessionsLocked()
    {
        var fresh = new Dictionary<string, NAudioSession>();
        foreach ((string deviceId, (MMDevice device, DeviceFlow flow)) in _devices)
        {
            try
            {
                AudioSessionManager manager = device.AudioSessionManager;
                manager.RefreshSessions();
                SessionCollection collection = manager.Sessions;
                for (int i = 0; i < collection.Count; i++)
                {
                    AudioSessionControl control = collection[i];
                    string key = $"{deviceId}|{control.GetSessionInstanceIdentifier}";
                    if (fresh.ContainsKey(key))
                    {
                        continue;
                    }

                    if (_sessions.TryGetValue(key, out NAudioSession? existing))
                    {
                        fresh[key] = existing;
                        continue;
                    }

                    int pid = (int)control.GetProcessID;
                    var info = new AudioSessionInfo(key, pid, ProcessPath.TryGet(pid), deviceId, flow, control.IsSystemSoundsSession);
                    fresh[key] = new NAudioSession(control, info);
                }
            }
            catch (COMException)
            {
                // Device vanished while listing; the topology notification will rebuild.
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

    private sealed class NAudioEndpoint(MMDevice device, DeviceFlow flow) : IAudioEndpoint
    {
        public string Id { get; } = device.ID;

        public string Name { get; } = device.FriendlyName;

        public DeviceFlow Flow { get; } = flow;

        public float Volume
        {
            get => Run(() => device.AudioEndpointVolume.MasterVolumeLevelScalar);
            set => Run(() => device.AudioEndpointVolume.MasterVolumeLevelScalar = value);
        }

        public bool Muted
        {
            get => Run(() => device.AudioEndpointVolume.Mute);
            set => Run(() => device.AudioEndpointVolume.Mute = value);
        }

        private static T Run<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (COMException ex)
            {
                throw Translate(ex);
            }
        }
    }

    private sealed class NAudioSession(AudioSessionControl control, AudioSessionInfo info) : IAudioSession, IDisposable
    {
        public AudioSessionInfo Info { get; } = info;

        public float Volume
        {
            get => Run(() => control.SimpleAudioVolume.Volume);
            set => Run(() => control.SimpleAudioVolume.Volume = value);
        }

        public bool Muted
        {
            get => Run(() => control.SimpleAudioVolume.Mute);
            set => Run(() => control.SimpleAudioVolume.Mute = value);
        }

        public void Dispose()
        {
            try
            {
                control.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private static T Run<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (COMException ex)
            {
                throw Translate(ex);
            }
        }
    }
}
