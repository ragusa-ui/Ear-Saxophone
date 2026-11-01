using System.Text.Json.Serialization;

namespace EarSaxophone.Core.Mapping;

public enum DeviceFlow
{
    /// <summary>Playback devices (speakers, headphones).</summary>
    Render,

    /// <summary>Recording devices (microphones). Kept separate from Render so the microphone is never confused with the speaker volume.</summary>
    Capture,
}

public enum DeviceRole
{
    Console,
    Multimedia,
    Communications,
}

public enum AudioTargetKind
{
    /// <summary>The current default endpoint for <see cref="AudioTarget.Flow"/> and <see cref="AudioTarget.Role"/>.</summary>
    DefaultDevice,

    /// <summary>One concrete endpoint, identified by <see cref="AudioTarget.DeviceId"/>.</summary>
    SpecificDevice,

    /// <summary>All audio sessions of one application on <see cref="AudioTarget.Flow"/> devices.</summary>
    Application,

    /// <summary>
    /// "Wie Fader": the same targets as the volume actions of the fader in the strip (column 1-8) of the control that carries the
    /// action. Resolved when it is used (see <see cref="StripTargets"/>), so a program added to the fader is muted by the strip's
    /// keys at once. The fader itself never refers to itself (no cycles); outside a strip there is nothing to resolve.
    /// </summary>
    Strip,
}

/// <summary>Identifies an application by executable, not by volatile audio-session ids.</summary>
public sealed record AppSelector(string ExeName, string? ExePath = null)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(ExeName);
}

public sealed record AudioTarget(
    AudioTargetKind Kind,
    DeviceFlow Flow = DeviceFlow.Render,
    DeviceRole Role = DeviceRole.Console,
    string? DeviceId = null,
    AppSelector? App = null)
{
    public bool IsValid => Kind switch
    {
        AudioTargetKind.DefaultDevice => true,
        AudioTargetKind.SpecificDevice => !string.IsNullOrWhiteSpace(DeviceId),
        AudioTargetKind.Application => App is { IsValid: true },
        AudioTargetKind.Strip => true,
        _ => false,
    };

    /// <summary>True for the "Wie Fader" target (<see cref="AudioTargetKind.Strip"/>), which stands for the targets of the strip's fader.</summary>
    public bool IsStrip => Kind == AudioTargetKind.Strip;

    public static AudioTarget Speakers { get; } = new(AudioTargetKind.DefaultDevice, DeviceFlow.Render);

    public static AudioTarget Microphone { get; } = new(AudioTargetKind.DefaultDevice, DeviceFlow.Capture);

    public static AudioTarget Application(string exeName) =>
        new(AudioTargetKind.Application, App: new AppSelector(exeName));

    /// <summary>"Wie Fader": the volume targets of the fader in the same strip (see <see cref="AudioTargetKind.Strip"/>).</summary>
    public static AudioTarget Strip { get; } = new(AudioTargetKind.Strip);
}

public enum MediaCommand
{
    PlayPause,
    Next,
    Previous,
    Stop,
}

/// <summary>How a <see cref="MuteAction"/> reacts to a key.</summary>
public enum MuteMode
{
    /// <summary>Each press switches between muted and audible.</summary>
    Toggle,

    /// <summary>Muted while the key is held, audible again on release.</summary>
    MuteWhileHeld,

    /// <summary>Push-to-talk: audible while the key is held, muted again on release.</summary>
    UnmuteWhileHeld,
}

/// <summary>
/// What a mapped control does. Continuous actions use the scaled control value, the others fire on button press
/// (and <see cref="MuteAction"/> in a "while held" mode also on release). The JSON type name of the old
/// "toggleMute" action is migrated to "mute" when a file is loaded (see <c>ProfileSerializer</c>).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SetVolumeAction), "setVolume")]
[JsonDerivedType(typeof(MuteAction), "mute")]
[JsonDerivedType(typeof(MediaAction), "media")]
[JsonDerivedType(typeof(HotkeyAction), "hotkey")]
[JsonDerivedType(typeof(SwitchDeviceAction), "switchDevice")]
public abstract record ActionDefinition
{
    [JsonIgnore]
    public abstract bool IsContinuous { get; }

    [JsonIgnore]
    public abstract bool IsValid { get; }
}

public sealed record SetVolumeAction(AudioTarget Target) : ActionDefinition
{
    public override bool IsContinuous => true;

    public override bool IsValid => Target.IsValid;
}

/// <summary>Mutes or unmutes a target; see <see cref="MuteMode"/>. Several toggles in one mapping switch together.</summary>
public sealed record MuteAction(AudioTarget Target, MuteMode Mode = MuteMode.Toggle) : ActionDefinition
{
    public override bool IsContinuous => false;

    public override bool IsValid => Target.IsValid && Enum.IsDefined(Mode);

    /// <summary>True for the modes that also react when the key is released.</summary>
    [JsonIgnore]
    public bool IsWhileHeld => Mode is MuteMode.MuteWhileHeld or MuteMode.UnmuteWhileHeld;
}

public sealed record MediaAction(MediaCommand Command) : ActionDefinition
{
    public override bool IsContinuous => false;

    public override bool IsValid => Enum.IsDefined(Command);
}

/// <summary>Sends a keyboard shortcut such as "Ctrl+Alt+M" (see <see cref="Input.Hotkey"/> for the accepted syntax).</summary>
public sealed record HotkeyAction(string Keys) : ActionDefinition
{
    public override bool IsContinuous => false;

    public override bool IsValid => Input.Hotkey.TryParse(Keys, out _, out _);
}

/// <summary>
/// Makes the next device in a rotation the Windows default device (playback or recording). DeviceIds limits the rotation
/// to the chosen devices; empty or null means all connected devices. The order is by device name.
/// </summary>
public sealed record SwitchDeviceAction(DeviceFlow Flow = DeviceFlow.Render, IReadOnlyList<string>? DeviceIds = null) : ActionDefinition
{
    public override bool IsContinuous => false;

    public override bool IsValid => Enum.IsDefined(Flow);

    [JsonIgnore]
    public IReadOnlyList<string> SelectedIds => DeviceIds ?? [];

    public bool Equals(SwitchDeviceAction? other) =>
        other is not null && Flow == other.Flow && SelectedIds.SequenceEqual(other.SelectedIds);

    public override int GetHashCode() => HashCode.Combine(Flow, SelectedIds.Count);
}
