using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Devices;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.Persistence;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

/// <summary>"Wie Fader" (<see cref="AudioTarget.Strip"/>): resolution, execution, LEDs, texts, transfer and file format.</summary>
public class StripTargetTests
{
    private static readonly ControlId Fader1 = Cc(0);
    private static readonly ControlId Fader3 = Cc(2);
    private static readonly ControlId Fader4 = Cc(3);
    private static readonly ControlId Knob1 = Cc(16);
    private static readonly ControlId S1 = Cc(32);
    private static readonly ControlId M1 = Cc(48);
    private static readonly ControlId M3 = Cc(50);
    private static readonly ControlId M4 = Cc(51);
    private static readonly ControlId Play = Cc(41);
    private static readonly ControlId Cycle = Cc(46);

    private static readonly string[] Games = ["a.exe", "b.exe", "c.exe", "d.exe", "e.exe", "f.exe"];

    private static ControlId Cc(int number) => new(ControlKind.ControlChange, 0, (byte)number);

    private static AudioTarget App(string exe) => AudioTarget.Application(exe);

    private static ControlMapping Volume(ControlId control, params string[] exes) =>
        new(control, exes.Select(e => (ActionDefinition)new SetVolumeAction(App(e))).ToList());

    private static ControlMapping StripMute(ControlId control, MuteMode mode = MuteMode.Toggle) => new(control, new MuteAction(AudioTarget.Strip, mode));

    private static Profile ProfileOf(params ControlMapping[] mappings) => new("p", mappings);

    // ---------- layout and resolution ----------

    [Fact]
    public void Layout_KnowsTheStripOfEveryChannelControl_AndItsFader()
    {
        foreach (string label in new[] { "Drehregler 4", "S 4", "M 4", "R 4", "Fader 4" })
        {
            LayoutElement element = Nanokontrol2Layout.Elements.Single(e => e.Label == label);
            Assert.Equal(4, element.Strip);
            Assert.Equal(Fader4, Nanokontrol2Layout.StripFaderOf(element.Control));
        }

        Assert.Null(Nanokontrol2Layout.StripOf(Play));
        Assert.Null(Nanokontrol2Layout.StripFaderOf(Cycle));
        Assert.Null(Nanokontrol2Layout.StripFaderOf(new ControlId(ControlKind.Note, 3, 60)));
        Assert.Null(Nanokontrol2Layout.FaderOfStrip(9));
        Assert.Equal(40, Nanokontrol2Layout.Elements.Count(e => e.Strip is >= 1 and <= 8));
        Assert.Equal(Fader1, StripTargets.SourceFader(M1));
        Assert.Null(StripTargets.SourceFader(Fader1)); // the fader never follows itself
    }

    [Fact]
    public void Resolve_OneTarget_BecomesAMuteOfTheFadersProgram()
    {
        Profile profile = ProfileOf(Volume(Fader1, "opera.exe"), StripMute(M1));

        ControlMapping resolved = StripTargets.Resolve(profile, profile.Find(M1)!);

        Assert.Equal([new MuteAction(App("opera.exe"))], resolved.Actions);
        Assert.Equal(M1, resolved.Control);
    }

    [Fact]
    public void Resolve_SeveralTargets_KeepsAllInTheFadersOrder_AndTheMuteMode()
    {
        Profile profile = ProfileOf(Volume(Fader4, Games), StripMute(M4, MuteMode.MuteWhileHeld));

        ControlMapping resolved = StripTargets.Resolve(profile, profile.Find(M4)!);

        Assert.Equal(Games.Select(g => (ActionDefinition)new MuteAction(App(g), MuteMode.MuteWhileHeld)), resolved.Actions);
    }

    [Fact]
    public void Resolve_MissingOrEmptyFader_LeavesTheStripActionAsItIs()
    {
        ControlMapping m1 = StripMute(M1);

        Assert.Same(m1, StripTargets.Resolve(ProfileOf(m1), m1)); // no fader mapping
        Assert.Same(m1, StripTargets.Resolve(ProfileOf(new ControlMapping(Fader1, new SetVolumeAction(AudioTarget.Strip)), m1), m1));
        var play = new ControlMapping(Play, new MuteAction(AudioTarget.Strip));
        Assert.Same(play, StripTargets.Resolve(ProfileOf(Volume(Fader1, "x.exe"), play), play)); // no strip
        Assert.Empty(StripTargets.FaderTargets(ProfileOf(m1), M1));
        Assert.Empty(StripTargets.FaderTargets(ProfileOf(Volume(Fader1, "x.exe")), Play));
    }

    [Fact]
    public void Resolve_NoCycle_AFaderThatPointsAtItselfIsNeverFollowed()
    {
        // A file could put "Wie Fader" on the fader itself; neither the fader nor its keys loop or pick up anything from it.
        var fader = new ControlMapping(Fader1, [new SetVolumeAction(AudioTarget.Strip), new SetVolumeAction(App("x.exe"))]);
        Profile profile = ProfileOf(fader, StripMute(M1));

        Assert.Same(fader, StripTargets.Resolve(profile, fader));
        Assert.Equal([new MuteAction(App("x.exe"))], StripTargets.Resolve(profile, profile.Find(M1)!).Actions);
        Assert.Contains(profile.Validate(), p => p.Contains("Wie Fader", StringComparison.Ordinal));
    }

    [Fact]
    public void Resolve_KnobVolume_FollowsTheFader_AndMappingsWithoutStripTargetStayTheSameInstance()
    {
        var knob = new ControlMapping(Knob1, new SetVolumeAction(AudioTarget.Strip));
        Profile profile = ProfileOf(Volume(Fader1, "x.exe", "y.exe"), knob);

        Assert.Equal([new SetVolumeAction(App("x.exe")), new SetVolumeAction(App("y.exe"))], StripTargets.Resolve(profile, knob).Actions);
        ControlMapping plain = Volume(Fader3, "z.exe");
        Assert.Same(plain, StripTargets.Resolve(profile, plain));
        Assert.Null(StripTargets.ResolveOrNull(profile, null));
    }

    [Fact]
    public void Resolve_ExplicitTargetsThatTheFaderHasToo_AreNotDoubled()
    {
        var m1 = new ControlMapping(M1, [new MuteAction(App("x.exe")), new MuteAction(AudioTarget.Strip), new MuteAction(AudioTarget.Microphone)]);
        Profile profile = ProfileOf(Volume(Fader1, "X.exe", "x.exe", "y.exe"), m1);

        Assert.Equal([App("X.exe"), App("x.exe"), App("y.exe")], StripTargets.FaderTargets(profile, M1)); // case is the fader's business
        Assert.Equal(
            [new MuteAction(App("x.exe")), new MuteAction(App("X.exe")), new MuteAction(App("y.exe")), new MuteAction(AudioTarget.Microphone)],
            StripTargets.Resolve(profile, m1).Actions);
    }

    [Fact]
    public void Validate_ReportsStripTargetsOutsideAStrip_OnlyThere()
    {
        Assert.Empty(ProfileOf(Volume(Fader1, "x.exe"), StripMute(M1), new ControlMapping(Knob1, new SetVolumeAction(AudioTarget.Strip))).Validate());
        string problem = Assert.Single(ProfileOf(new ControlMapping(Play, new MuteAction(AudioTarget.Strip))).Validate());
        Assert.Contains("CC 41", problem, StringComparison.Ordinal);
        Assert.True(AudioTarget.Strip.IsValid);
        Assert.True(new MuteAction(AudioTarget.Strip).IsValid);
    }

    // ---------- execution ----------

    private sealed class Rig
    {
        public Rig()
        {
            Speakers = Audio.AddEndpoint("spk", "Lautsprecher", DeviceFlow.Render);
            Mic = Audio.AddEndpoint("mic", "Mikrofon", DeviceFlow.Capture);
            Controller = new AudioController(Audio);
            Executor = new ActionExecutor(Controller, new RecordingKeyboard());
            foreach (string game in Games)
            {
                Sessions[game] = Audio.AddSession($@"C:\Games\{game}", "spk");
            }
        }

        public FakeAudioSystem Audio { get; } = new();

        public FakeEndpoint Speakers { get; }

        public FakeEndpoint Mic { get; }

        public AudioController Controller { get; }

        public ActionExecutor Executor { get; }

        public Dictionary<string, FakeSession> Sessions { get; } = [];

        public (IReadOnlyList<ActionInvocation> Invocations, IReadOnlyList<ActionResult> Results) Press(MappingDispatcher dispatcher, ControlId control, byte value = 127)
        {
            IReadOnlyList<ActionInvocation> invocations = dispatcher.Dispatch(new MidiMessage(MidiMessageKind.ControlChange, 0, control.Number, value));
            return (invocations, Executor.ExecuteAll(invocations));
        }
    }

    [Fact]
    public void PressingM_MutesAllSixGamesOfTheFader_AsOneGroup_AndUnmutesThemAgain()
    {
        var rig = new Rig();
        rig.Sessions["c.exe"].Muted = true; // one already muted: the group still mutes all, then unmutes all
        var dispatcher = new MappingDispatcher(ProfileOf(Volume(Fader4, Games), StripMute(M4)));

        (IReadOnlyList<ActionInvocation> invocations, IReadOnlyList<ActionResult> results) = rig.Press(dispatcher, M4);

        Assert.Equal(6, invocations.Count);
        Assert.All(invocations, i => Assert.False(StripTargets.IsStripAction(i.Action)));
        Assert.All(rig.Sessions.Values, s => Assert.True(s.Muted));
        Assert.All(results, r => Assert.Equal(ActionOutcome.Applied, r.Outcome));
        Assert.False(rig.Speakers.Muted);
        Assert.Equal(("a.exe + 5", true), OsdMessageFactory.Create(invocations, results) is { } osd ? (osd.Title, osd.Muted) : default);

        rig.Press(dispatcher, M4, 0);
        rig.Press(dispatcher, M4);
        Assert.All(rig.Sessions.Values, s => Assert.False(s.Muted));
    }

    [Fact]
    public void ChangingTheFadersTargets_ChangesWhatMMutes_AtOnce()
    {
        var rig = new Rig();
        Profile profile = ProfileOf(Volume(Fader4, Games[..5]), StripMute(M4));
        var dispatcher = new MappingDispatcher(profile);
        rig.Press(dispatcher, M4);
        Assert.False(rig.Sessions["f.exe"].Muted);
        rig.Press(dispatcher, M4, 0);
        rig.Press(dispatcher, M4); // all audible again

        dispatcher.SetProfile(profile.WithMapping(Volume(Fader4, Games))); // the 6th game is added to the fader only
        rig.Press(dispatcher, M4);

        Assert.All(rig.Sessions.Values, s => Assert.True(s.Muted));
    }

    [Fact]
    public void WhileHeldModes_WorkOnEveryResolvedTarget()
    {
        var rig = new Rig();
        var dispatcher = new MappingDispatcher(ProfileOf(Volume(Fader4, "a.exe", "b.exe"), StripMute(M4, MuteMode.UnmuteWhileHeld)));

        rig.Press(dispatcher, M4);
        Assert.False(rig.Sessions["a.exe"].Muted);
        rig.Press(dispatcher, M4, 0);
        Assert.True(rig.Sessions["a.exe"].Muted);
        Assert.True(rig.Sessions["b.exe"].Muted);
        Assert.False(rig.Sessions["c.exe"].Muted);
    }

    [Fact]
    public void FaderWithoutTargets_MDoesNothing_AndSaysWhy()
    {
        var rig = new Rig();
        var dispatcher = new MappingDispatcher(ProfileOf(new ControlMapping(Fader1, new SetVolumeAction(AudioTarget.Strip)), StripMute(M1),
            new ControlMapping(Play, [new MuteAction(AudioTarget.Strip), new MuteAction(AudioTarget.Microphone)])));

        (IReadOnlyList<ActionInvocation> invocations, IReadOnlyList<ActionResult> results) = rig.Press(dispatcher, M1);

        ActionResult result = Assert.Single(results);
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Contains("Fader 1", result.Message, StringComparison.Ordinal);
        Assert.False(rig.Speakers.Muted); // never falls back to the default device
        Assert.Null(OsdMessageFactory.Create(invocations, results));

        (_, IReadOnlyList<ActionResult> play) = rig.Press(dispatcher, Play); // outside a strip; the microphone still toggles
        Assert.Equal(Texts.StripTargetNoFader, play[0].Message);
        Assert.Equal(ActionOutcome.Applied, play[1].Outcome);
        Assert.True(rig.Mic.Muted);
        Assert.False(rig.Speakers.Muted);
    }

    [Fact]
    public void AudioController_NeverTreatsAnUnresolvedStripTargetAsTheDefaultDevice()
    {
        var rig = new Rig();

        Assert.True(rig.Controller.SetMute(AudioTarget.Strip, true).IsFailure);
        Assert.True(rig.Controller.SetVolume(AudioTarget.Strip, 0.2f).IsFailure);
        Assert.True(rig.Controller.ToggleMute(AudioTarget.Strip).IsFailure);
        Assert.Null(rig.Controller.ReadGroupMuted([AudioTarget.Strip]));
        Assert.False(rig.Speakers.Muted);
        Assert.Equal(0.5f, rig.Speakers.Volume);
    }

    // ---------- LED state and texts ----------

    [Fact]
    public void LedState_UsesTheResolvedTargets()
    {
        var rig = new Rig();
        Profile profile = ProfileOf(Volume(Fader4, "a.exe", "b.exe"), StripMute(M4), StripMute(M1));

        Assert.False(LedController.IsLit(profile, M4, rig.Controller.ReadGroupMuted));
        rig.Sessions["a.exe"].Muted = true;
        Assert.False(LedController.IsLit(profile, M4, rig.Controller.ReadGroupMuted)); // b.exe audible
        rig.Sessions["b.exe"].Muted = true;
        Assert.True(LedController.IsLit(profile, M4, rig.Controller.ReadGroupMuted));

        // A program added to the fader that is audible makes the group audible.
        Assert.False(LedController.IsLit(profile.WithMapping(Volume(Fader4, "a.exe", "b.exe", "c.exe")), M4, rig.Controller.ReadGroupMuted));

        rig.Speakers.Muted = true;
        Assert.False(LedController.IsLit(profile, M1, rig.Controller.ReadGroupMuted)); // empty fader: nothing, never the speakers
    }

    [Fact]
    public void Texts_DescribeTheResolvedTargets_NotWieFader()
    {
        Profile profile = ProfileOf(Volume(Fader1, "opera.exe"), Volume(Fader4, Games), StripMute(M1), StripMute(M4), StripMute(M3));

        Assert.Equal("Stumm schalten: opera.exe", MappingDescriber.Describe(profile, profile.Find(M1)));
        Assert.Equal("Stumm schalten: a.exe (+5)", MappingDescriber.Describe(profile, profile.Find(M4)));
        Assert.Contains("wie Fader", MappingDescriber.Describe(profile, profile.Find(M3)), StringComparison.Ordinal); // fader 3 empty
        Assert.Equal(6, MappingBadge.From(StripTargets.Resolve(profile, profile.Find(M4)!)).ActionCount);
        Assert.Equal(Texts.UiNoMapping, MappingDescriber.Describe(profile, null));
    }

    // ---------- moving and copying ----------

    [Fact]
    public void Move_ToAKeyOfAnotherStrip_KeepsWieFader_WhichThenMeansThatStripsFader()
    {
        Profile profile = ProfileOf(Volume(Fader1, "opera.exe"), Volume(Fader3, "game.exe"), StripMute(M1));

        TransferResult result = profile.TransferMapping(M1, M3, TransferMode.Move, Nanokontrol2Layout.ShapeOf);

        Assert.Equal(TransferOutcome.Moved, result.Outcome);
        Assert.Equal(StripMute(M3), result.Profile.Find(M3));
        Assert.Null(result.Profile.Find(M1));
        Assert.Equal([new MuteAction(App("game.exe"))], StripTargets.Resolve(result.Profile, result.Profile.Find(M3)!).Actions);
        Assert.Empty(result.Profile.Validate());
    }

    [Fact]
    public void Copy_ToAKeyOutsideTheStrips_TakesTheTargetsItHadAtItsOldPlace()
    {
        Profile profile = ProfileOf(Volume(Fader1, "opera.exe", "x.exe"), StripMute(M1, MuteMode.MuteWhileHeld));

        TransferResult result = profile.TransferMapping(M1, Cycle, TransferMode.Copy, Nanokontrol2Layout.ShapeOf);

        Assert.Equal(TransferOutcome.Copied, result.Outcome);
        Assert.Equal([new MuteAction(App("opera.exe"), MuteMode.MuteWhileHeld), new MuteAction(App("x.exe"), MuteMode.MuteWhileHeld)],
            result.Profile.Find(Cycle)!.Actions);
        Assert.Equal(StripMute(M1, MuteMode.MuteWhileHeld), result.Profile.Find(M1)); // the source stays relative
        Assert.Empty(result.Profile.Validate());
    }

    [Fact]
    public void Move_OutsideTheStripsWithAnEmptyFader_IsRefused_OrKeepsOnlyTheOtherActions()
    {
        Profile profile = ProfileOf(StripMute(M1), new ControlMapping(S1, [new MuteAction(AudioTarget.Strip), new MuteAction(AudioTarget.Microphone)]));

        TransferResult refused = profile.TransferMapping(M1, Play, TransferMode.Move, Nanokontrol2Layout.ShapeOf);
        Assert.Equal(TransferOutcome.Incompatible, refused.Outcome);
        Assert.Same(profile, refused.Profile);
        Assert.False(profile.CanTransferTo(M1, Play));

        TransferResult moved = profile.TransferMapping(S1, Play, TransferMode.Move, Nanokontrol2Layout.ShapeOf);
        Assert.Equal(TransferOutcome.Moved, moved.Outcome);
        Assert.Equal([new MuteAction(AudioTarget.Microphone)], moved.Profile.Find(Play)!.Actions);
    }

    [Fact]
    public void Swap_WithAKeyOutsideTheStrips_ResolvesOnlyWhereItMustAndKeepsTheOtherRelative()
    {
        Profile profile = ProfileOf(Volume(Fader1, "opera.exe"), StripMute(M1), new ControlMapping(Play, new MuteAction(AudioTarget.Microphone)),
            new ControlMapping(Cycle, new MuteAction(AudioTarget.Strip)));

        TransferResult result = profile.TransferMapping(M1, Play, TransferMode.Move, Nanokontrol2Layout.ShapeOf);
        Assert.Equal(TransferOutcome.Swapped, result.Outcome);
        Assert.Equal([new MuteAction(App("opera.exe"))], result.Profile.Find(Play)!.Actions);
        Assert.Equal([new MuteAction(AudioTarget.Microphone)], result.Profile.Find(M1)!.Actions);

        // Cycle (outside the strips, from an old file) has nothing to resolve: swapping it back onto a key of a strip makes it work.
        TransferResult back = profile.TransferMapping(Cycle, M3, TransferMode.Move, Nanokontrol2Layout.ShapeOf);
        Assert.Equal(StripMute(M3), back.Profile.Find(M3));
    }

    [Fact]
    public void Move_KnobWithWieFaderOntoAFader_BecomesConcrete_NoSelfReference()
    {
        Profile profile = ProfileOf(Volume(Fader1, "opera.exe"), new ControlMapping(Knob1, new SetVolumeAction(AudioTarget.Strip)));

        TransferResult result = profile.TransferMapping(Knob1, Fader3, TransferMode.Move, Nanokontrol2Layout.ShapeOf);

        Assert.Equal(TransferOutcome.Moved, result.Outcome);
        Assert.Equal([new SetVolumeAction(App("opera.exe"))], result.Profile.Find(Fader3)!.Actions);
        Assert.Empty(result.Profile.Validate());
    }

    // ---------- file format ----------

    [Fact]
    public void File_RoundTripsTheStripTarget_InSchemaVersion2()
    {
        var document = new ProfileDocument(ProfileDocument.CurrentSchemaVersion, "p",
            [ProfileOf(Volume(Fader1, "opera.exe"), StripMute(M1, MuteMode.MuteWhileHeld), new ControlMapping(Knob1, new SetVolumeAction(AudioTarget.Strip)))]);

        string json = ProfileSerializer.Serialize(document);
        ProfileReadResult read = ProfileSerializer.Read(json);

        Assert.Equal(2, ProfileDocument.CurrentSchemaVersion);
        Assert.Contains("\"kind\": \"strip\"", json, StringComparison.Ordinal);
        Assert.False(read.WasMigrated);
        Assert.Equal(document.Profiles[0].Mappings, read.Document.Profiles[0].Mappings);
        Assert.True(((MuteAction)read.Document.Profiles[0].Find(M1)!.Actions[0]).Target.IsStrip);
    }

    [Fact]
    public void File_AShortHandWrittenStripTargetLoads_AndFilesWithoutItAreWrittenAsBefore()
    {
        const string json = """
            { "schemaVersion": 2, "activeProfile": "p", "profiles": [ { "name": "p", "mappings": [
              { "control": { "kind": "controlChange", "channel": 0, "number": 48 },
                "actions": [ { "type": "mute", "target": { "kind": "strip" }, "mode": "unmuteWhileHeld" } ] } ] } ] }
            """;

        Assert.Equal(StripMute(M1, MuteMode.UnmuteWhileHeld), ProfileSerializer.Deserialize(json).Profiles[0].Mappings[0]);

        string plain = ProfileSerializer.Serialize(new ProfileDocument(2, "p", [ProfileOf(Volume(Fader1, "x.exe"), new ControlMapping(M1, new MuteAction(AudioTarget.Microphone)))]));
        Assert.DoesNotContain("strip", plain, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"schemaVersion\": 2", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void File_Version1AndVersion2FilesStillLoadUnchanged()
    {
        const string v1 = """
            { "schemaVersion": 1, "activeProfile": "p", "profiles": [ { "name": "p", "mappings": [
              { "control": { "kind": "controlChange", "channel": 0, "number": 48 }, "action": { "type": "toggleMute", "target": { "kind": "defaultDevice" } } } ] } ] }
            """;
        const string v2 = """
            { "schemaVersion": 2, "activeProfile": "p", "profiles": [ { "name": "p", "mappings": [
              { "control": { "kind": "controlChange", "channel": 0, "number": 0 },
                "actions": [ { "type": "setVolume", "target": { "kind": "application", "flow": "render", "role": "console", "app": { "exeName": "opera.exe" } } } ] } ] } ] }
            """;

        ProfileReadResult old = ProfileSerializer.Read(v1);
        Assert.Equal(1, old.SourceSchemaVersion);
        Assert.Equal([new MuteAction(AudioTarget.Speakers)], old.Document.Profiles[0].Mappings[0].Actions);
        ProfileReadResult current = ProfileSerializer.Read(v2);
        Assert.False(current.WasMigrated);
        Assert.Equal(Volume(Fader1, "opera.exe"), current.Document.Profiles[0].Mappings[0]);
    }
}

internal static class ProfileTransferExtensions
{
    public static bool CanTransferTo(this Profile profile, ControlId from, ControlId to) =>
        profile.TransferMapping(from, to, TransferMode.Move, Nanokontrol2Layout.ShapeOf).Changed;
}
