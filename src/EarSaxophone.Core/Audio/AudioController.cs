using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Actions;
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
    public ActionResult? Execute(ActionInvocation invocation) => invocation.Mapping.Action switch
    {
        SetVolumeAction a => SetVolume(a.Target, invocation.Level),
        ToggleMuteAction a => ToggleMute(a.Target),
        _ => null,
    };

    public ActionResult SetVolume(AudioTarget target, float level)
    {
        level = Math.Clamp(level, 0f, 1f);
        lock (_gate)
        {
            return target.Kind == AudioTargetKind.Application
                ? SetApplication(target, volume: level, muted: null)
                : SetEndpoint(target, e => e.Volume = level, Texts.WhatVolume);
        }
    }

    public ActionResult ToggleMute(AudioTarget target)
    {
        lock (_gate)
        {
            if (target.Kind != AudioTargetKind.Application)
            {
                return SetEndpoint(target, e => e.Muted = !e.Muted, Texts.WhatMute);
            }

            if (!target.IsValid)
            {
                return Fail(Texts.AppNotSet);
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

    private ActionResult SetApplication(AudioTarget target, float? volume, bool? muted)
    {
        if (!target.IsValid)
        {
            return Fail(Texts.AppNotSet);
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
            return new ActionResult(ActionOutcome.Deferred,
                Texts.AppSilentDeferred(name));
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
            ? new ActionResult(ActionOutcome.Applied, Texts.AppSessionsUpdated(name, applied))
            : Fail(Texts.AppSessionsFailed(name));
    }

    private ActionResult SetEndpoint(AudioTarget target, Action<IAudioEndpoint> change, string what)
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
            return Fail(Texts.TargetQueryFailed(label, ex.Message));
        }

        if (endpoint is null)
        {
            return Fail(target.Flow == DeviceFlow.Capture && target.Kind == AudioTargetKind.DefaultDevice
                ? Texts.NoRecordingDevice
                : Texts.TargetUnavailable(label));
        }

        try
        {
            change(endpoint);
            return new ActionResult(ActionOutcome.Applied, Texts.EndpointChanged(endpoint.Name, what));
        }
        catch (AudioUnsupportedException ex)
        {
            return Fail(Texts.EndpointUnsupported(endpoint.Name, what, ex.Message));
        }
        catch (AudioGoneException)
        {
            return Fail(Texts.EndpointDisconnected(endpoint.Name));
        }
        catch (Exception ex)
        {
            return Fail(Texts.EndpointFailed(endpoint.Name, what, ex.Message));
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
        AudioTargetKind.SpecificDevice => Texts.TargetSelectedDevice,
        _ when target.Flow == DeviceFlow.Capture => Texts.TargetDefaultRecording,
        _ => Texts.TargetDefaultPlayback,
    };

    private static ActionResult Fail(string message) => new(ActionOutcome.Failed, message);
}
