namespace EarSaxophone.Core.Mapping;

/// <summary>Maps a MIDI value (0-127) to a normalised level in [Min, Max] (each within 0..1), optionally inverted.</summary>
public sealed record ValueScaling(float Min = 0f, float Max = 1f, bool Invert = false)
{
    public bool IsValid => Min >= 0f && Max <= 1f && Min <= Max;

    public float Apply(byte midiValue)
    {
        float t = Math.Clamp(midiValue, (byte)0, (byte)127) / 127f;
        if (Invert)
        {
            t = 1f - t;
        }

        return Min + (Max - Min) * t;
    }
}
