using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Audio;

/// <summary>A program that started playing and is not on any control yet; the App asks where to put it.</summary>
public sealed record ProgramPrompt(string ExeName, string? ExePath);

/// <summary>
/// Notices programs that play sound for the first time and are not mapped yet (design V2, section 6). A program is
/// offered when one of its playback sessions (not recording) has been <see cref="AudioSessionActivity.Active"/> without
/// interruption for at least <see cref="MinimumActiveTime"/>, it is not the system sounds or this program itself, no
/// mapping of the given profile has it as a target and it is not ignored. Every program is offered at most once per run,
/// only one question is open at a time and after a question closed the next one waits at least <see cref="MinimumGap"/>.
/// <see cref="Check"/> is meant to be called about once per second; it is thread-safe.
/// </summary>
public sealed class ProgramPromptService
{
    /// <summary>A short notification sound must not cause a question.</summary>
    public static readonly TimeSpan MinimumActiveTime = TimeSpan.FromSeconds(2);

    /// <summary>Several programs starting together are asked about one after the other, not all at once.</summary>
    public static readonly TimeSpan MinimumGap = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly IAudioSystem _audio;
    private readonly TimeProvider _time;
    private readonly string _ownName;
    private readonly int _ownProcessId;
    private readonly Dictionary<string, DateTimeOffset> _activeSince = [];
    private readonly HashSet<string> _asked = [];
    private ProgramPrompt? _open;
    private DateTimeOffset? _closedAt;

    /// <param name="audio">Audio sessions with their activity.</param>
    /// <param name="time">Clock (tests use a manual one).</param>
    /// <param name="ownExeName">Executable of this program, never offered.</param>
    /// <param name="ownProcessId">Process id of this program, never offered.</param>
    public ProgramPromptService(IAudioSystem audio, TimeProvider? time = null, string ownExeName = "EarSaxophone.exe", int ownProcessId = -1)
    {
        _audio = audio;
        _time = time ?? TimeProvider.System;
        _ownName = AppMatcher.NormalizeName(ownExeName);
        _ownProcessId = ownProcessId;
    }

    /// <summary>The question that is shown right now, if any.</summary>
    public ProgramPrompt? OpenPrompt
    {
        get
        {
            lock (_gate)
            {
                return _open;
            }
        }
    }

    /// <summary>
    /// Looks at the audio sessions and returns a program to ask about now, or null. The returned program counts as asked
    /// (for this run) and as open until <see cref="Close"/>.
    /// </summary>
    public ProgramPrompt? Check(Profile profile, IEnumerable<string> ignored)
    {
        IReadOnlyList<IAudioSession> sessions;
        try
        {
            sessions = _audio.GetSessions();
        }
        catch (Exception)
        {
            return null; // the audio service may be restarting; the next check tries again
        }

        DateTimeOffset now = _time.GetUtcNow();
        var playing = new Dictionary<string, (string ExeName, string? ExePath)>();
        foreach (IAudioSession session in sessions)
        {
            if (IsCandidate(session) && session.Info.ExePath is { } path)
            {
                playing.TryAdd(AppMatcher.NormalizeName(path), (AppMatcher.FileNameOf(path), path));
            }
        }

        var ignoredNames = ignored.Select(AppMatcher.NormalizeName).ToHashSet();
        lock (_gate)
        {
            foreach (string gone in _activeSince.Keys.Where(k => !playing.ContainsKey(k)).ToList())
            {
                _activeSince.Remove(gone);
            }

            foreach (string key in playing.Keys)
            {
                _activeSince.TryAdd(key, now);
            }

            if (_open is not null || (_closedAt is { } closed && now - closed < MinimumGap))
            {
                return null;
            }

            (string Key, DateTimeOffset Since)? next = _activeSince
                .Where(p => now - p.Value >= MinimumActiveTime && !_asked.Contains(p.Key) && !ignoredNames.Contains(p.Key) && !IsMapped(profile, p.Key))
                .OrderBy(p => p.Value)
                .ThenBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => ((string, DateTimeOffset)?)(p.Key, p.Value))
                .FirstOrDefault();
            if (next is not { } chosen)
            {
                return null;
            }

            _asked.Add(chosen.Key);
            (string exeName, string? exePath) = playing[chosen.Key];
            _open = new ProgramPrompt(exeName, exePath);
            return _open;
        }
    }

    /// <summary>The question was answered, dismissed or timed out; the pause before the next one starts now.</summary>
    public void Close()
    {
        lock (_gate)
        {
            if (_open is null)
            {
                return;
            }

            _open = null;
            _closedAt = _time.GetUtcNow();
        }
    }

    /// <summary>True when any volume or mute action of the profile targets this program (name or path, ".exe" optional).</summary>
    public static bool IsMapped(Profile profile, string exeNameOrPath)
    {
        string name = AppMatcher.NormalizeName(exeNameOrPath);
        return profile.Mappings.SelectMany(m => m.Actions).Any(a => TargetOf(a) is { Kind: AudioTargetKind.Application, App: { } app }
            && AppMatcher.NormalizeName(app.ExeName) == name);
    }

    /// <summary>
    /// Puts the program's volume on a control as a further target: existing actions stay as they are; an unmapped control
    /// gets a new mapping. Nothing changes when the control already has a volume action for the program.
    /// </summary>
    public static Profile AddProgram(Profile profile, ControlId control, string exeName)
    {
        var action = new SetVolumeAction(AudioTarget.Application(AppMatcher.FileNameOf(exeName)));
        ControlMapping? existing = profile.Find(control);
        if (existing is null)
        {
            return profile.WithMapping(new ControlMapping(control, action));
        }

        string name = AppMatcher.NormalizeName(exeName);
        if (existing.Actions.Any(a => a is SetVolumeAction { Target: { Kind: AudioTargetKind.Application, App: { } app } } && AppMatcher.NormalizeName(app.ExeName) == name))
        {
            return profile;
        }

        ControlMapping extended = existing with { Actions = [.. existing.Actions, action] };
        return profile with { Mappings = profile.Mappings.Select(m => m.Control == control ? extended : m).ToList() };
    }

    private static AudioTarget? TargetOf(ActionDefinition action) => action switch
    {
        SetVolumeAction volume => volume.Target,
        MuteAction mute => mute.Target,
        _ => null,
    };

    private bool IsCandidate(IAudioSession session)
    {
        AudioSessionInfo info = session.Info;
        if (info.Flow != DeviceFlow.Render || info.IsSystemSounds || string.IsNullOrWhiteSpace(info.ExePath)
            || info.ProcessId == _ownProcessId || AppMatcher.NormalizeName(info.ExePath) == _ownName)
        {
            return false;
        }

        try
        {
            return session.Activity == AudioSessionActivity.Active;
        }
        catch (Exception)
        {
            return false; // the session vanished while it was asked
        }
    }
}
