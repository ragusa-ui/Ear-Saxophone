using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Audio;

/// <summary>
/// Applies volume and mute actions to endpoints and applications.
/// Applications are resolved from the live session list on every call, so a restarted app, a second process or
/// a changed output device can never leave the controller holding a stale reference. The last value set for an
/// application is remembered and handed once to every session that appears later (restart, new device).
/// Sessions are remembered by their lifetime key only to avoid re-applying; after that the user can change a
/// session in Windows without the fader overriding it again.
/// </summary>
public sealed class AudioController : IDisposable
{
    private sealed class Desired(AppSelector selector, DeviceFlow flow)
    {
        public AppSelector Selector { get; } = selector;

        public DeviceFlow Flow { get; } = flow;

        public float? Volume { get; set; }

        public bool? Muted { get; set; }

        public HashSet<string> AppliedSessionKeys { get; } = [];
    }

    private readonly IAudioSystem _system;
    private readonly object _gate = new();
    private readonly Dictionary<(string Name, string? Path, DeviceFlow Flow), Desired> _desired = [];

    public AudioController(IAudioSystem system)
    {
        _system = system;
        _system.Changed += Reconcile;
    }

    public void Dispose() => _system.Changed -= Reconcile;

    /// <summary>Runs audio actions; returns null for actions that are not audio actions.</summary>
    public AudioActionResult? Execute(ActionInvocation invocation) => invocation.Mapping.Action switch
    {
        SetVolumeAction a => SetVolume(a.Target, invocation.Level),
        ToggleMuteAction a => ToggleMute(a.Target),
        _ => null,
    };

    public AudioActionResult SetVolume(AudioTarget target, float level)
    {
        level = Math.Clamp(level, 0f, 1f);
        lock (_gate)
        {
            return target.Kind == AudioTargetKind.Application
                ? SetApplication(target, volume: level, muted: null)
                : SetEndpoint(target, e => e.Volume = level, "volume");
        }
    }

    public AudioActionResult ToggleMute(AudioTarget target)
    {
        lock (_gate)
        {
            if (target.Kind != AudioTargetKind.Application)
            {
                return SetEndpoint(target, e => e.Muted = !e.Muted, "mute");
            }

            if (!target.IsValid)
            {
                return Fail("The application is not set.");
            }

            Desired desired = GetDesired(target);
            List<IAudioSession> sessions = FindSessions(desired);
            bool currentlyAllMuted = sessions.Count > 0
                ? sessions.All(s => SafeMuted(s) == true)
                : desired.Muted == true;
            return SetApplication(target, volume: null, muted: !currentlyAllMuted);
        }
    }

    /// <summary>Gives remembered values to sessions that have not received them yet.</summary>
    public void Reconcile()
    {
        lock (_gate)
        {
            if (_desired.Count == 0)
            {
                return;
            }

            IReadOnlyList<IAudioSession> all;
            try
            {
                all = _system.GetSessions();
            }
            catch (Exception)
            {
                return;
            }

            var live = all.Select(s => s.Info.Key).ToHashSet();
            foreach (Desired desired in _desired.Values)
            {
                desired.AppliedSessionKeys.RemoveWhere(k => !live.Contains(k));
                foreach (IAudioSession session in all.Where(s => Belongs(desired, s)))
                {
                    if (!desired.AppliedSessionKeys.Add(session.Info.Key))
                    {
                        continue;
                    }

                    if (!TryApply(session, desired.Volume, desired.Muted))
                    {
                        desired.AppliedSessionKeys.Remove(session.Info.Key);
                    }
                }
            }
        }
    }

    private AudioActionResult SetApplication(AudioTarget target, float? volume, bool? muted)
    {
        if (!target.IsValid)
        {
            return Fail("The application is not set.");
        }

        Desired desired = GetDesired(target);
        if (volume is not null)
        {
            desired.Volume = volume;
        }

        if (muted is not null)
        {
            desired.Muted = muted;
        }

        string name = target.App!.ExeName;
        List<IAudioSession> sessions = FindSessions(desired);
        if (sessions.Count == 0)
        {
            return new AudioActionResult(AudioOutcome.Deferred,
                $"{name} is not playing audio right now. The value is kept and applied when it starts.");
        }

        int applied = 0;
        foreach (IAudioSession session in sessions)
        {
            if (TryApply(session, volume, muted))
            {
                desired.AppliedSessionKeys.Add(session.Info.Key);
                applied++;
            }
        }

        return applied > 0
            ? new AudioActionResult(AudioOutcome.Applied, $"{name}: {applied} session(s) updated.")
            : Fail($"{name}: its audio sessions could not be changed (they may have just closed).");
    }

    private AudioActionResult SetEndpoint(AudioTarget target, Action<IAudioEndpoint> change, string what)
    {
        string label = Describe(target);
        IAudioEndpoint? endpoint;
        try
        {
            endpoint = target.Kind == AudioTargetKind.SpecificDevice
                ? (string.IsNullOrWhiteSpace(target.DeviceId) ? null : _system.GetEndpoint(target.DeviceId))
                : _system.GetDefaultEndpoint(target.Flow, target.Role);
        }
        catch (Exception ex)
        {
            return Fail($"{label} could not be queried: {ex.Message}");
        }

        if (endpoint is null)
        {
            return Fail(target.Flow == DeviceFlow.Capture && target.Kind == AudioTargetKind.DefaultDevice
                ? "No default recording device is available. Connect or enable a microphone."
                : $"{label} is not available.");
        }

        try
        {
            change(endpoint);
            return new AudioActionResult(AudioOutcome.Applied, $"{endpoint.Name}: {what} changed.");
        }
        catch (AudioUnsupportedException ex)
        {
            return Fail($"{endpoint.Name} does not allow {what} control through Windows ({ex.Message}).");
        }
        catch (AudioGoneException)
        {
            return Fail($"{endpoint.Name} was just disconnected.");
        }
        catch (Exception ex)
        {
            return Fail($"{endpoint.Name}: {what} could not be changed ({ex.Message}).");
        }
    }

    private Desired GetDesired(AudioTarget target)
    {
        AppSelector selector = target.App!;
        var key = (AppMatcher.NormalizeName(selector.ExeName),
            selector.ExePath is null ? null : AppMatcher.NormalizePath(selector.ExePath),
            target.Flow);
        if (!_desired.TryGetValue(key, out Desired? desired))
        {
            desired = new Desired(selector, target.Flow);
            _desired[key] = desired;
        }

        return desired;
    }

    private List<IAudioSession> FindSessions(Desired desired)
    {
        try
        {
            return _system.GetSessions().Where(s => Belongs(desired, s)).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static bool Belongs(Desired desired, IAudioSession session) =>
        session.Info.Flow == desired.Flow && AppMatcher.Matches(desired.Selector, session.Info);

    private static bool TryApply(IAudioSession session, float? volume, bool? muted)
    {
        try
        {
            if (volume is { } v)
            {
                session.Volume = v;
            }

            if (muted is { } m)
            {
                session.Muted = m;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool? SafeMuted(IAudioSession session)
    {
        try
        {
            return session.Muted;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Describe(AudioTarget target) => target.Kind switch
    {
        AudioTargetKind.SpecificDevice => "The selected device",
        _ when target.Flow == DeviceFlow.Capture => "The default recording device",
        _ => "The default playback device",
    };

    private static AudioActionResult Fail(string message) => new(AudioOutcome.Failed, message);
}
