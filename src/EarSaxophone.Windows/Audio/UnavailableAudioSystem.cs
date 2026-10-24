using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Windows.Audio;

/// <summary>Stand-in used when the Windows audio service cannot be reached, so the program still starts and explains the problem.</summary>
public sealed class UnavailableAudioSystem : IAudioSystem
{
    public event Action? Changed
    {
        add { }
        remove { }
    }

    public IAudioEndpoint? GetDefaultEndpoint(DeviceFlow flow, DeviceRole role) => null;

    public IAudioEndpoint? GetEndpoint(string deviceId) => null;

    public IReadOnlyList<IAudioSession> GetSessions() => [];
}
