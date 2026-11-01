using EarSaxophone.Core.Actions;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Mapping;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

public class MappingDescriberTests
{
    private static readonly ControlId Id = new(ControlKind.ControlChange, 0, 48);

    [Fact]
    public void Describe_MuteModes()
    {
        Assert.Equal("Stumm schalten: Mikrofon", MappingDescriber.Describe(new MuteAction(AudioTarget.Microphone)));
        Assert.Equal("Stumm solange gedrückt: Lautsprecher", MappingDescriber.Describe(new MuteAction(AudioTarget.Speakers, MuteMode.MuteWhileHeld)));
        Assert.Equal("Ton solange gedrückt: Mikrofon", MappingDescriber.Describe(new MuteAction(AudioTarget.Microphone, MuteMode.UnmuteWhileHeld)));
    }

    [Fact]
    public void Describe_Mapping_SingleSeveralNamedEmpty()
    {
        Assert.Equal(Texts.UiNoMapping, MappingDescriber.Describe((ControlMapping?)null));
        Assert.Equal(Texts.UiNoMapping, MappingDescriber.Describe(new ControlMapping(Id, Array.Empty<ActionDefinition>())));
        Assert.Equal("Medien: Stopp", MappingDescriber.Describe(new ControlMapping(Id, new MediaAction(MediaCommand.Stop))));
        Assert.Equal("Stumm schalten: Discord.exe (+2)", MappingDescriber.Describe(new ControlMapping(Id,
            [new MuteAction(AudioTarget.Application("Discord.exe")), new MuteAction(AudioTarget.Speakers), new MuteAction(AudioTarget.Microphone)])));
        Assert.Equal("Spiele", MappingDescriber.Describe(new ControlMapping(Id, new MuteAction(AudioTarget.Speakers), Name: "Spiele")));
        Assert.Equal("Stumm schalten: Lautsprecher", MappingDescriber.Describe(new ControlMapping(Id, new MuteAction(AudioTarget.Speakers), Name: " ")));
    }

    [Fact]
    public void KindNames_AreGerman()
    {
        Assert.Equal("Lautstärke", MappingDescriber.KindName(new SetVolumeAction(AudioTarget.Speakers)));
        Assert.Equal("Stummschaltung", MappingDescriber.KindName(new MuteAction(AudioTarget.Speakers)));
        Assert.Equal("Medienbefehl", MappingDescriber.KindName(new MediaAction(MediaCommand.Next)));
        Assert.Equal("Tastenkombination", MappingDescriber.KindName(new HotkeyAction("Ctrl+M")));
        Assert.Equal("Gerätewechsel", MappingDescriber.KindName(new SwitchDeviceAction()));
    }

    [Fact]
    public void Osd_ForMuteActionsOfEveryMode()
    {
        var applied = new ActionResult(ActionOutcome.Applied, "ok", Muted: true);
        foreach (MuteMode mode in Enum.GetValues<MuteMode>())
        {
            Assert.Equal(new OsdMessage("Mikrofon", Texts.OsdMuted, null, true), OsdMessageFactory.Create(new MuteAction(AudioTarget.Microphone, mode), applied));
        }

        Assert.Null(OsdMessageFactory.Create(new HotkeyAction("Ctrl+M"), applied));
        Assert.Null(OsdMessageFactory.Create(new MuteAction(AudioTarget.Microphone), new ActionResult(ActionOutcome.Failed, "x")));
    }
}
