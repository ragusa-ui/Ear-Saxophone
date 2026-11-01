using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;
using EarSaxophone.Core.Settings;
using EarSaxophone.Core.Updates;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

/// <summary>A main view model with fake device, fake audio, a manual clock and its own data folder.</summary>
internal sealed class ViewModelHarness : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    public ViewModelHarness()
    {
        Directory.CreateDirectory(Dir);
        Speakers = Audio.AddEndpoint("spk", "Lautsprecher", DeviceFlow.Render);
        Mic = Audio.AddEndpoint("mic", "Mikrofon", DeviceFlow.Capture);
        Discord = Audio.AddSession(@"C:\Users\x\AppData\Local\Discord\app-1\Discord.exe", "spk");
        Provider.Devices.Add(new MidiDeviceInfo(0, "nanoKONTROL2"));
    }

    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "earsax-v2-" + Guid.NewGuid().ToString("N"));

    public string ProfilePath => Path.Combine(Dir, "profiles.json");

    public string SettingsPath => Path.Combine(Dir, "settings.json");

    public FakeMidiProvider Provider { get; } = new();

    public FakeAudioSystem Audio { get; } = new();

    public RecordingKeyboard Keyboard { get; } = new();

    public ManualClock Clock { get; } = new();

    public FakeEndpoint Speakers { get; }

    public FakeEndpoint Mic { get; }

    public FakeSession Discord { get; }

    public static ControlId Cc(int number) => new(ControlKind.ControlChange, 0, (byte)number);

    public MainViewModel Create(UpdateService? updates = null, IAppRestarter? restarter = null)
    {
        var midi = new MidiConnectionManager(Provider);
        var executor = new ActionExecutor(new AudioController(Audio), Keyboard);
        var vm = new MainViewModel(new ProfileStore(ProfilePath), midi, executor, Audio, new ImmediateUi(),
            new SettingsStore(SettingsPath), new FakeAutostart(), Clock, updates: updates, restarter: restarter);
        _disposables.Add(midi);
        _disposables.Add(vm);
        vm.Tick();
        return vm;
    }

    public void SaveProfile(params ControlMapping[] mappings) =>
        new ProfileStore(ProfilePath).Save(new ProfileDocument(ProfileDocument.CurrentSchemaVersion, "p", [new Profile("p", mappings)]));

    public Profile SavedProfile() => new ProfileStore(ProfilePath).Load().Document.Profiles[0];

    /// <summary>Sends a message from the device and waits until the view model processed it.</summary>
    public void Send(MainViewModel vm, int cc, int value)
    {
        Provider.Ports[^1].Send((byte)cc, (byte)value);
        Assert.True(vm.WaitUntilIdle(TimeSpan.FromSeconds(10)), "MIDI messages were not processed in time");
    }

    public static ControllerElementViewModel Element(MainViewModel vm, string label) =>
        vm.Elements.Concat(vm.ExtraElements).Single(e => e.Label == label);

    public static void Assign(MainViewModel vm, string label, Action<MappingEditorViewModel> configure)
    {
        vm.Select(Element(vm, label));
        configure(vm.Editor);
        vm.ApplyEditor();
    }

    public void Dispose()
    {
        foreach (IDisposable d in _disposables)
        {
            d.Dispose();
        }

        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, recursive: true);
        }
    }
}
