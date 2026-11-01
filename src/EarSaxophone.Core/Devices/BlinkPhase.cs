namespace EarSaxophone.Core.Devices;

/// <summary>
/// The blink rhythm of muted keys: on for <c>half</c>, off for <c>half</c>, counted from <c>start</c> (the moment the blinking
/// (re)started, so a key that was just muted lights up at once). Pure functions, shared by the device LEDs and the picture.
/// </summary>
public static class BlinkPhase
{
    /// <summary>Default length of the on and of the off phase.</summary>
    public static TimeSpan DefaultHalfPeriod { get; } = TimeSpan.FromMilliseconds(600);

    /// <summary>True during an on phase (and before <paramref name="start"/>, or when <paramref name="half"/> is not positive).</summary>
    public static bool IsOn(DateTimeOffset start, DateTimeOffset now, TimeSpan half)
    {
        TimeSpan elapsed = now - start;
        if (half <= TimeSpan.Zero || elapsed < TimeSpan.Zero)
        {
            return true;
        }

        return elapsed.Ticks / half.Ticks % 2 == 0;
    }

    /// <summary>Time until the phase changes next (always positive; a whole phase when it changes right now).</summary>
    public static TimeSpan UntilNextChange(DateTimeOffset start, DateTimeOffset now, TimeSpan half)
    {
        if (half <= TimeSpan.Zero)
        {
            return Timeout.InfiniteTimeSpan;
        }

        TimeSpan elapsed = now - start;
        if (elapsed < TimeSpan.Zero)
        {
            return -elapsed + half; // on before the start and during the first phase
        }

        long remainder = half.Ticks - elapsed.Ticks % half.Ticks;
        return TimeSpan.FromTicks(remainder <= 0 ? half.Ticks : remainder);
    }
}
