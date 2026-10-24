using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.Tests;

public class MidiMessageParserTests
{
    [Fact]
    public void ControlChange_IsParsed()
    {
        Assert.True(MidiMessageParser.TryParse([0xB0, 0x00, 0x7F], out MidiMessage m));
        Assert.Equal(new MidiMessage(MidiMessageKind.ControlChange, 0, 0, 127), m);
        Assert.Equal(new ControlId(ControlKind.ControlChange, 0, 0), m.Control);
    }

    [Fact]
    public void Channel_IsTakenFromLowNibble()
    {
        Assert.True(MidiMessageParser.TryParse([0xB5, 7, 64], out MidiMessage m));
        Assert.Equal(5, m.Channel);
    }

    [Fact]
    public void NoteOn_WithVelocityZero_BecomesNoteOff()
    {
        Assert.True(MidiMessageParser.TryParse([0x90, 60, 0], out MidiMessage m));
        Assert.Equal(MidiMessageKind.NoteOff, m.Kind);
    }

    [Fact]
    public void NoteOn_AndNoteOff_ShareOneControlId()
    {
        MidiMessageParser.TryParse([0x90, 60, 100], out MidiMessage on);
        MidiMessageParser.TryParse([0x80, 60, 0], out MidiMessage off);
        Assert.Equal(on.Control, off.Control);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0xB0, 1 })]
    [InlineData(new byte[] { 0xB0, 1, 2, 3 })]
    [InlineData(new byte[] { 0x30, 1, 2 })] // no status bit
    [InlineData(new byte[] { 0xB0, 0x80, 2 })] // data byte out of range
    [InlineData(new byte[] { 0xB0, 1, 0x80 })]
    [InlineData(new byte[] { 0xE0, 1, 2 })] // pitch bend: unsupported
    [InlineData(new byte[] { 0xF8, 0, 0 })] // system real-time
    public void InvalidOrUnsupported_IsRejected(byte[] bytes)
    {
        Assert.False(MidiMessageParser.TryParse(bytes, out MidiMessage m));
        Assert.Equal(default, m);
    }

    [Fact]
    public void PackedWinMmMessage_IsParsed()
    {
        int packed = 0xB1 | (23 << 8) | (99 << 16);
        Assert.True(MidiMessageParser.TryParse(packed, out MidiMessage m));
        Assert.Equal(new MidiMessage(MidiMessageKind.ControlChange, 1, 23, 99), m);
    }

    [Fact]
    public void PackedMessage_WithHighByteSet_IsRejected()
    {
        Assert.False(MidiMessageParser.TryParse(0xB0 | (1 << 8) | (2 << 16) | (1 << 24), out _));
    }
}
