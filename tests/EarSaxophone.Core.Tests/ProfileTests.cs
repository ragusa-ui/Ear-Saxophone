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
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 32), new MuteAction(new AudioTarget(AudioTargetKind.DefaultDevice, DeviceFlow.Capture, DeviceRole.Communications))),
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
        var id = new ControlId(ControlKind.ControlChange, 0, 100); // not in the layout: every action fits
        var profile = new Profile("x",
        [
            new ControlMapping(id, new MediaAction(MediaCommand.Next)),
            new ControlMapping(id, new SetVolumeAction(new AudioTarget(AudioTargetKind.Application))),
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 4), new SetVolumeAction(AudioTarget.Speakers), new ValueScaling(0.9f, 0.1f)),
        ]);

        Assert.Equal(3, profile.Validate().Count);
    }

    [Fact]
    public void Validate_ReportsActionsThatDoNotFitTheControl_InGerman()
    {
        var profile = new Profile("x",
        [
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 3), new HotkeyAction("Ctrl+M")),          // Fader 4
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 16), new MuteAction(AudioTarget.Speakers)), // Drehregler 1
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 45), new SetVolumeAction(AudioTarget.Speakers)), // Aufnahme
        ]);

        IReadOnlyList<string> problems = profile.Validate();

        Assert.Equal(3, problems.Count);
        Assert.Contains("CC 3 (Kanal 1): Tastenkombination passt nicht zu einem stufenlosen Regler (Fader oder Drehregler).", problems);
        Assert.Contains("CC 16 (Kanal 1): Stummschaltung passt nicht zu einem stufenlosen Regler (Fader oder Drehregler).", problems);
        Assert.Contains("CC 45 (Kanal 1): Lautstärke passt nicht zu einer Taste.", problems);
    }

    [Fact]
    public void Validate_AcceptsEveryKeyActionOnKeys_AndVolumeOnFadersAndKnobs()
    {
        var key = new ControlId(ControlKind.ControlChange, 0, 41);
        var profile = new Profile("x",
        [
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 0),
                [new SetVolumeAction(AudioTarget.Application("a.exe")), new SetVolumeAction(AudioTarget.Application("b.exe"))]),
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 16), new SetVolumeAction(AudioTarget.Microphone)),
            new ControlMapping(key,
            [
                new MuteAction(AudioTarget.Speakers, MuteMode.MuteWhileHeld),
                new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld),
                new MediaAction(MediaCommand.Stop),
                new HotkeyAction("Ctrl+Alt+M"),
                new SwitchDeviceAction(DeviceFlow.Capture),
            ]),
        ]);

        Assert.Empty(profile.Validate());
    }

    [Fact]
    public void Validate_UsesTheGivenShapes_AndReportsEachUnfitKindOnce()
    {
        var learned = new ControlId(ControlKind.ControlChange, 5, 7);
        var profile = new Profile("x",
            [new ControlMapping(learned, [new MediaAction(MediaCommand.Next), new MediaAction(MediaCommand.Stop), new SetVolumeAction(AudioTarget.Speakers)])]);

        Assert.Empty(profile.Validate());                                        // unknown control: everything allowed
        Assert.Single(profile.Validate(_ => ControlShape.Continuous));           // media twice, reported once
        Assert.Single(profile.Validate(_ => ControlShape.Button));               // the volume action
    }

    [Fact]
    public void Validate_ReportsEmptyMappingAndOneIncompleteActionAmongSeveral()
    {
        var profile = new Profile("x",
        [
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 41), Array.Empty<ActionDefinition>()),
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 0),
                [new SetVolumeAction(AudioTarget.Speakers), new SetVolumeAction(new AudioTarget(AudioTargetKind.Application))]),
            new ControlMapping(new ControlId(ControlKind.ControlChange, 0, 42), new MuteAction(AudioTarget.Speakers, (MuteMode)42)),
        ]);

        IReadOnlyList<string> problems = profile.Validate();

        Assert.Equal(3, problems.Count);
        Assert.Contains("CC 41 (Kanal 1): Die Belegung enthält keine Aktion.", problems);
        Assert.Contains(problems, p => p.StartsWith("CC 0 (Kanal 1): Die Aktion ist unvollständig", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("CC 42 (Kanal 1): Die Aktion ist unvollständig", StringComparison.Ordinal));
    }

    [Fact]
    public void ControlMapping_ComparesActionsByContent()
    {
        var id = new ControlId(ControlKind.ControlChange, 0, 0);
        var a = new ControlMapping(id, [new SetVolumeAction(AudioTarget.Speakers), new SetVolumeAction(AudioTarget.Microphone)], null, "Alles");
        var b = new ControlMapping(id, new List<ActionDefinition> { new SetVolumeAction(AudioTarget.Speakers), new SetVolumeAction(AudioTarget.Microphone) }, null, "Alles");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, b with { Name = null });
        Assert.NotEqual(a, b with { Actions = [new SetVolumeAction(AudioTarget.Microphone), new SetVolumeAction(AudioTarget.Speakers)] });
        Assert.NotEqual(a, b with { Scaling = new ValueScaling(Invert: true) });
        Assert.Equal(new ControlMapping(id, new MediaAction(MediaCommand.Next)), new ControlMapping(id, [new MediaAction(MediaCommand.Next)]));
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
