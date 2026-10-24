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
        _ => false,
    };

    public static AudioTarget Speakers { get; } = new(AudioTargetKind.DefaultDevice, DeviceFlow.Render);

    public static AudioTarget Microphone { get; } = new(AudioTargetKind.DefaultDevice, DeviceFlow.Capture);

    public static AudioTarget Application(string exeName) =>
        new(AudioTargetKind.Application, App: new AppSelector(exeName));
}

public enum MediaCommand
{
    PlayPause,
    Next,
    Previous,
    Stop,
}

/// <summary>What a mapped control does. Continuous actions use the scaled control value, trigger actions fire on button press.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SetVolumeAction), "setVolume")]
[JsonDerivedType(typeof(ToggleMuteAction), "toggleMute")]
[JsonDerivedType(typeof(MediaAction), "media")]
[JsonDerivedType(typeof(HotkeyAction), "hotkey")]
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

public sealed record ToggleMuteAction(AudioTarget Target) : ActionDefinition
{
    public override bool IsContinuous => false;

    public override bool IsValid => Target.IsValid;
}

public sealed record MediaAction(MediaCommand Command) : ActionDefinition
{
    public override bool IsContinuous => false;

    public override bool IsValid => Enum.IsDefined(Command);
}

/// <summary>Sends a keyboard shortcut such as "Ctrl+Alt+M". Parsing of the text is added with the hotkey milestone.</summary>
public sealed record HotkeyAction(string Keys) : ActionDefinition
{
    public override bool IsContinuous => false;

    public override bool IsValid => !string.IsNullOrWhiteSpace(Keys);
}
