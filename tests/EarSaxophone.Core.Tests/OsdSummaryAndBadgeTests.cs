using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.Core.Tests;

public class OsdSummaryAndBadgeTests
{
    private static readonly ControlId Fader = ViewModelHarness.Cc(3);
    private static readonly ControlId Key = ViewModelHarness.Cc(48);

    private static ActionResult Applied(float? level = null, bool? muted = null) => new(ActionOutcome.Applied, "ok", muted, level);

    private static ActionResult Deferred(float level) => new(ActionOutcome.Deferred, "später", null, level);

    private static ActionResult Failed() => new(ActionOutcome.Failed, "kaputt");

    private static IReadOnlyList<ActionInvocation> Volume(string? name, params AudioTarget[] targets) =>
        ActionInvocation.ForMapping(new ControlMapping(Fader, targets.Select(t => (ActionDefinition)new SetVolumeAction(t)).ToList(), Name: name), null, 0.4f);

    [Fact]
    public void SeveralTargets_TitleIsTheFirstPlusTheNumberOfOthers_LevelFromTheFirst()
    {
        IReadOnlyList<ActionInvocation> runs = Volume(null, AudioTarget.Application("a.exe"), AudioTarget.Application("b.exe"), AudioTarget.Speakers);

        OsdMessage osd = OsdMessageFactory.Create(runs, [Applied(0.4f), Applied(0.5f), Applied(0.6f)])!;

        Assert.Equal("a.exe + 2", osd.Title);
        Assert.Equal(0.4, osd.Level!.Value, 5);
        Assert.Null(osd.Detail);
    }

    [Fact]
    public void AnOwnName_IsTheTitle_ForOneOrSeveralTargets()
    {
        Assert.Equal("Spiele", OsdMessageFactory.Create(Volume("Spiele", AudioTarget.Application("a.exe"), AudioTarget.Application("b.exe")), [Applied(0.4f), Applied(0.4f)])!.Title);
        Assert.Equal("Spiele", OsdMessageFactory.Create(Volume(" Spiele ", AudioTarget.Application("a.exe")), [Applied(0.4f)])!.Title);
        Assert.Equal("a.exe", OsdMessageFactory.Create(Volume(null, AudioTarget.Application("a.exe")), [Applied(0.4f)])!.Title);
    }

    [Fact]
    public void InactiveIsOnlyShown_WhenNoTargetPlays()
    {
        IReadOnlyList<ActionInvocation> runs = Volume(null, AudioTarget.Application("a.exe"), AudioTarget.Application("b.exe"));

        Assert.Equal(Texts.OsdInactive, OsdMessageFactory.Create(runs, [Deferred(0.4f), Deferred(0.4f)])!.Detail);
        OsdMessage someone = OsdMessageFactory.Create(runs, [Deferred(0.4f), Applied(0.4f)])!;
        Assert.Null(someone.Detail);
        Assert.Equal("a.exe + 1", someone.Title);
    }

    [Fact]
    public void AFailedFirstTarget_LetsTheNextOneLead()
    {
        IReadOnlyList<ActionInvocation> runs = Volume(null, AudioTarget.Microphone, AudioTarget.Speakers);

        OsdMessage osd = OsdMessageFactory.Create(runs, [Failed(), Applied(0.4f)])!;

        Assert.Equal("Lautsprecher + 1", osd.Title);
        Assert.Null(OsdMessageFactory.Create(runs, [Failed(), Failed()]));
    }

    [Fact]
    public void GroupMute_ShowsTheCommonState()
    {
        var mapping = new ControlMapping(Key, [new MuteAction(AudioTarget.Speakers), new MuteAction(AudioTarget.Application("Discord.exe"))]);
        IReadOnlyList<ActionInvocation> runs = ActionInvocation.ForMapping(mapping, ActionPhase.Pressed, 1f);

        OsdMessage osd = OsdMessageFactory.Create(runs, [Applied(muted: true), Applied(muted: true)])!;

        Assert.Equal(("Lautsprecher + 1", Texts.OsdMuted, true), (osd.Title, osd.Detail, osd.Muted));
    }

    [Fact]
    public void Badge_ListsEveryKindOnce_InOrder_AndCountsAllActions()
    {
        var mixed = new ControlMapping(Key, [new MuteAction(AudioTarget.Speakers), new MediaAction(MediaCommand.Next), new MuteAction(AudioTarget.Microphone)]);

        MappingBadge badge = MappingBadge.From(mixed);

        Assert.Equal([ActionKind.Mute, ActionKind.Media], badge.Kinds);
        Assert.Equal((ActionKind.Mute, 3, true, true), (badge.PrimaryKind, badge.ActionCount, badge.ShowsCount, badge.HasBadge));
        Assert.Same(MappingBadge.Empty, MappingBadge.From(null));
        Assert.False(MappingBadge.From(new ControlMapping(Key, new HotkeyAction("Ctrl+A"))).ShowsCount);
        Assert.Equal(ActionKind.SwitchDevice, MappingBadge.KindOf(new SwitchDeviceAction()));
    }

    [Fact]
    public void ElementBadge_ChangesWithTheMapping_AndRaisesItsProperties()
    {
        var element = new ControllerElementViewModel(Devices.Nanokontrol2Layout.Find(Fader)!);
        var changed = new List<string?>();
        element.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        element.ShowMapping(new ControlMapping(Fader, [new SetVolumeAction(AudioTarget.Speakers), new SetVolumeAction(AudioTarget.Microphone)]));

        Assert.Equal((ActionKind.SetVolume, 2, true), (element.BadgeKind, element.ActionCount, element.ShowsActionCount));
        Assert.Contains(nameof(ControllerElementViewModel.ShowsActionCount), changed);
        Assert.True(element.HasMapping && element.IsContinuous);
        Assert.Null(typeof(ControllerElementViewModel).GetProperty("ListText"));
        Assert.Null(typeof(MainViewModel).GetProperty("MappedElements"));

        element.ShowMapping(null);
        Assert.Equal((ActionKind.None, 0, false), (element.BadgeKind, element.ActionCount, element.HasMapping));
    }
}
