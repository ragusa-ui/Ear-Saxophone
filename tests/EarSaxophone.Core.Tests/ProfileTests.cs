using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;

namespace EarSaxophone.Core.Tests;

public class ProfileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "earsax-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static ProfileDocument Sample() => new(ProfileDocument.CurrentSchemaVersion, "Main",
    [
        new Profile("Main",
        [
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 0), new SetVolumeAction(AudioTarget.Speakers)),
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 1), new SetVolumeAction(AudioTarget.Microphone), new ValueScaling(0.1f, 0.9f, true)),
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 2), new SetVolumeAction(AudioTarget.Application("Discord.exe"))),
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 32), new ToggleMuteAction(new AudioTarget(AudioTargetKind.DefaultDevice, DeviceFlow.Capture, DeviceRole.Communications))),
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 41), new MediaAction(MediaCommand.PlayPause)),
            new ControlMapping(new ControlId(ControlKind.Note, 1, 60), new HotkeyAction("Ctrl+Alt+M")),
        ]),
    ]);

    [Fact]
    public void Serialization_RoundTripsAllActionTypes()
    {
        ProfileDocument original = Sample();
        ProfileDocument copy = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        Assert.Equal(original.ActiveProfile, copy.ActiveProfile);
        Profile a = original.Profiles[0];
        Profile b = copy.Profiles[0];
        Assert.Equal(a.Mappings.Count, b.Mappings.Count);
        for (int i = 0; i < a.Mappings.Count; i++)
        {
            Assert.Equal(a.Mappings[i], b.Mappings[i]);
        }
    }

    [Fact]
    public void Serialization_UsesReadableActionDiscriminator()
    {
        string json = ProfileSerializer.Serialize(Sample());
        Assert.Contains("\"type\": \"setVolume\"", json);
        Assert.Contains("\"flow\": \"capture\"", json);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":99,\"activeProfile\":\"x\",\"profiles\":[]}")]
    [InlineData("{\"schemaVersion\":0,\"activeProfile\":\"x\",\"profiles\":[]}")]
    public void Deserialize_RejectsBadInput(string json) =>
        Assert.Throws<ProfileFormatException>(() => ProfileSerializer.Deserialize(json));

    [Fact]
    public void Validate_AcceptsSampleProfile() =>
        Assert.Empty(Sample().Profiles[0].Validate());

    [Fact]
    public void Validate_ReportsDuplicateIncompleteAndBadScaling()
    {
        var id = new ControlId(ControlKind.ControlChange, 0, 3);
        var profile = new Profile("x",
        [
            new ControlMapping(id, new MediaAction(MediaCommand.Next)),
            new ControlMapping(id, new SetVolumeAction(new AudioTarget(AudioTargetKind.Application))),
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 4), new SetVolumeAction(AudioTarget.Speakers), new ValueScaling(0.9f, 0.1f)),
        ]);

        Assert.Equal(3, profile.Validate().Count);
    }

    [Fact]
    public void Store_MissingFile_ReturnsDefaultWithoutWarning()
    {
        ProfileLoadResult result = new ProfileStore(Path.Combine(_dir, "profiles.json")).Load();
        Assert.Null(result.Warning);
        Assert.Equal("Default", result.Document.ActiveProfile);
    }

    [Fact]
    public void Store_SaveThenLoad_ReturnsSameData_AndLeavesNoTempFile()
    {
        var store = new ProfileStore(Path.Combine(_dir, "sub", "profiles.json"));
        store.Save(Sample());
        store.Save(Sample()); // overwrite path

        ProfileLoadResult result = store.Load();
        Assert.Null(result.Warning);
        Assert.Equal(6, result.Document.Profiles[0].Mappings.Count);
        Assert.False(File.Exists(store.Path + ".tmp"));
    }

    [Fact]
    public void Store_CorruptFile_FallsBackToDefaultAndKeepsBackup()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "profiles.json");
        File.WriteAllText(path, "{ broken");

        ProfileLoadResult result = new ProfileStore(path).Load();

        Assert.NotNull(result.Warning);
        Assert.Equal("Default", result.Document.ActiveProfile);
        Assert.True(File.Exists(path + ".corrupt"));
        Assert.False(File.Exists(path));
    }
}
