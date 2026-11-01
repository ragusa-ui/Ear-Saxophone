using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Audio;

/// <summary>The device does not allow this operation through Windows (for example no volume control on a capture endpoint).</summary>
public sealed class AudioUnsupportedException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The session or device vanished between listing and use (process exited, device unplugged).</summary>
public sealed class AudioGoneException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>A device endpoint (speakers, headset, microphone). Volume is the Windows scalar 0..1; setters may throw.</summary>
public interface IAudioEndpoint
{
    string Id { get; }

    string Name { get; }

    DeviceFlow Flow { get; }

    float Volume { get; set; }

    bool Muted { get; set; }
}

/// <summary>
/// What is known about one audio session. <see cref="Key"/> is only valid while the session lives (device + session
/// instance); it must never be used as the identity of an application. <see cref="ExePath"/> is resolved from the
/// process id by the Windows layer and is null when the process could not be queried.
/// </summary>
public sealed record AudioSessionInfo(
    string Key,
    int ProcessId,
    string? ExePath,
    string DeviceId,
    DeviceFlow Flow,
    bool IsSystemSounds = false);

public interface IAudioSession
{
    AudioSessionInfo Info { get; }

    float Volume { get; set; }

    bool Muted { get; set; }
}

public interface IAudioSystem
{
    /// <summary>The current default endpoint, or null when there is none (for example no microphone connected).</summary>
    IAudioEndpoint? GetDefaultEndpoint(DeviceFlow flow, DeviceRole role);

    IAudioEndpoint? GetEndpoint(string deviceId);

    /// <summary>All currently active devices of the given kind (connected and enabled).</summary>
    IReadOnlyList<IAudioEndpoint> GetEndpoints(DeviceFlow flow);

    /// <summary>
    /// Makes the device the Windows default for all roles. Windows has no official API for this; the Windows implementation uses the
    /// same internal interface as other tools. Throws <see cref="AudioUnsupportedException"/> when Windows refuses.
    /// </summary>
    void SetDefaultEndpoint(string deviceId, DeviceFlow flow);

    /// <summary>All current sessions of all active devices, both render and capture.</summary>
    IReadOnlyList<IAudioSession> GetSessions();

    /// <summary>Raised when devices, default devices or sessions changed. May be raised on any thread.</summary>
    event Action? Changed;
}
