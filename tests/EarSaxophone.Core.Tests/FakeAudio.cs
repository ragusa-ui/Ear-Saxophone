using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Mapping;

namespace EarSaxophone.Core.Tests;

internal sealed class FakeEndpoint(string id, string name, DeviceFlow flow) : IAudioEndpoint
{
    private float _volume = 0.5f;
    private bool _muted;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public DeviceFlow Flow { get; } = flow;

    /// <summary>When set, volume and mute changes throw this (e.g. a headset without Windows volume control).</summary>
    public Exception? Failure { get; set; }

    public float Volume
    {
        get => _volume;
        set => _volume = Failure is null ? value : throw Failure;
    }

    public bool Muted
    {
        get => _muted;
        set => _muted = Failure is null ? value : throw Failure;
    }
}

internal sealed class FakeSession(AudioSessionInfo info) : IAudioSession
{
    private float _volume = 1f;
    private bool _muted;

    public AudioSessionInfo Info { get; } = info;

    /// <summary>Simulates a process that exited after the session list was read.</summary>
    public bool Gone { get; set; }

    public int VolumeWrites { get; private set; }

    /// <summary>Whether the session plays right now (Windows: AudioSessionState).</summary>
    public AudioSessionActivity Activity { get; set; } = AudioSessionActivity.Unknown;

    public float Volume
    {
        get => _volume;
        set
        {
            if (Gone)
            {
                throw new AudioGoneException("session closed");
            }

            VolumeWrites++;
            _volume = value;
        }
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            if (Gone)
            {
                throw new AudioGoneException("session closed");
            }

            _muted = value;
        }
    }
}

internal sealed class FakeAudioSystem : IAudioSystem
{
    private int _next;

    public List<FakeEndpoint> Endpoints { get; } = [];

    public List<FakeSession> Sessions { get; } = [];

    /// <summary>Default endpoint ids per flow (Console role); null means none.</summary>
    public Dictionary<DeviceFlow, string?> Defaults { get; } = new() { [DeviceFlow.Render] = null, [DeviceFlow.Capture] = null };

    public bool ThrowOnListing { get; set; }

    public event Action? Changed;

    public FakeEndpoint AddEndpoint(string id, string name, DeviceFlow flow, bool makeDefault = true)
    {
        var endpoint = new FakeEndpoint(id, name, flow);
        Endpoints.Add(endpoint);
        if (makeDefault)
        {
            Defaults[flow] = id;
        }

        return endpoint;
    }

    public FakeSession AddSession(string? exePath, string deviceId, DeviceFlow flow = DeviceFlow.Render, int? pid = null, bool systemSounds = false)
    {
        int id = ++_next;
        var session = new FakeSession(new AudioSessionInfo($"{deviceId}#{id}", pid ?? 1000 + id, exePath, deviceId, flow, systemSounds));
        Sessions.Add(session);
        return session;
    }

    public void RaiseChanged() => Changed?.Invoke();

    public IAudioEndpoint? GetDefaultEndpoint(DeviceFlow flow, DeviceRole role) =>
        Defaults[flow] is { } id ? Endpoints.FirstOrDefault(e => e.Id == id) : null;

    public IAudioEndpoint? GetEndpoint(string deviceId) => Endpoints.FirstOrDefault(e => e.Id == deviceId);

    public Exception? SetDefaultFailure { get; set; }

    public List<(string Id, DeviceFlow Flow)> DefaultChanges { get; } = [];

    public IReadOnlyList<IAudioEndpoint> GetEndpoints(DeviceFlow flow) => Endpoints.Where(e => e.Flow == flow).ToList<IAudioEndpoint>();

    public void SetDefaultEndpoint(string deviceId, DeviceFlow flow)
    {
        if (SetDefaultFailure is not null)
        {
            throw SetDefaultFailure;
        }

        DefaultChanges.Add((deviceId, flow));
        Defaults[flow] = deviceId;
    }

    public IReadOnlyList<IAudioSession> GetSessions() =>
        ThrowOnListing ? throw new InvalidOperationException("audio service unavailable") : Sessions.ToList<IAudioSession>();
}
