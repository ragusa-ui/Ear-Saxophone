using System.Globalization;

namespace EarSaxophone.Core.Updates;

/// <summary>
/// A version like <c>1.0.0</c> or <c>1.0.0-rc3</c> (Semantic Versioning 2.0: a release is newer than each of its
/// pre-releases, so <c>1.0.0-rc2 &lt; 1.0.0-rc3 &lt; 1.0.0</c>). Build metadata after '+' is ignored, a leading 'v' is accepted.
/// One deliberate deviation from the standard: inside a pre-release part, digits compare as numbers (<c>rc9 &lt; rc10</c>),
/// because release tags are written that way; SemVer itself would compare "rc10" and "rc9" letter by letter.
/// </summary>
public sealed class SemVersion : IComparable<SemVersion>, IEquatable<SemVersion>
{
    private SemVersion(int major, int minor, int patch, IReadOnlyList<string> prerelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>The dot-separated pre-release parts ("rc3" or "beta", "2"); empty for a release.</summary>
    public IReadOnlyList<string> Prerelease { get; }

    public bool IsPrerelease => Prerelease.Count > 0;

    public static SemVersion Parse(string text) =>
        TryParse(text, out SemVersion? version) ? version : throw new FormatException($"'{text}' is not a version like 1.2.3 or 1.2.3-rc1.");

    public static bool TryParse(string? text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SemVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        int plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];
        }

        string? pre = null;
        int dash = value.IndexOf('-');
        if (dash >= 0)
        {
            pre = value[(dash + 1)..];
            value = value[..dash];
        }

        string[] core = value.Split('.');
        if (core.Length != 3 || !TryNumber(core[0], out int major) || !TryNumber(core[1], out int minor) || !TryNumber(core[2], out int patch))
        {
            return false;
        }

        string[] parts = [];
        if (pre is not null)
        {
            parts = pre.Split('.');
            if (parts.Any(p => p.Length == 0 || !p.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')))
            {
                return false;
            }
        }

        version = new SemVersion(major, minor, patch, parts);
        return true;
    }

    public int CompareTo(SemVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int result = Major.CompareTo(other.Major);
        if (result == 0)
        {
            result = Minor.CompareTo(other.Minor);
        }

        if (result == 0)
        {
            result = Patch.CompareTo(other.Patch);
        }

        if (result != 0)
        {
            return result;
        }

        // A release ranks above its pre-releases.
        if (!IsPrerelease || !other.IsPrerelease)
        {
            return other.IsPrerelease.CompareTo(IsPrerelease);
        }

        for (int i = 0; i < Math.Min(Prerelease.Count, other.Prerelease.Count); i++)
        {
            result = ComparePart(Prerelease[i], other.Prerelease[i]);
            if (result != 0)
            {
                return result;
            }
        }

        return Prerelease.Count.CompareTo(other.Prerelease.Count);
    }

    public bool Equals(SemVersion? other) => other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is SemVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, string.Join('.', Prerelease).ToUpperInvariant());

    public override string ToString() =>
        IsPrerelease ? $"{Major}.{Minor}.{Patch}-{string.Join('.', Prerelease)}" : $"{Major}.{Minor}.{Patch}";

    public static bool operator <(SemVersion left, SemVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(SemVersion left, SemVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(SemVersion left, SemVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SemVersion left, SemVersion right) => left.CompareTo(right) >= 0;

    private static bool TryNumber(string text, out int number) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number) && text.Length > 0;

    // Numeric parts rank below alphanumeric ones (SemVer); otherwise runs of digits compare as numbers, letters ordinally (case-insensitive).
    private static int ComparePart(string a, string b)
    {
        bool aNumeric = a.All(char.IsAsciiDigit);
        bool bNumeric = b.All(char.IsAsciiDigit);
        if (aNumeric != bNumeric)
        {
            return aNumeric ? -1 : 1;
        }

        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsAsciiDigit(a[i]))
                {
                    i++;
                }

                while (j < b.Length && char.IsAsciiDigit(b[j]))
                {
                    j++;
                }

                int result = CompareDigits(a[si..i], b[sj..j]);
                if (result != 0)
                {
                    return result;
                }

                continue;
            }

            int c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
            if (c != 0)
            {
                return c;
            }

            i++;
            j++;
        }

        return (a.Length - i).CompareTo(b.Length - j);
    }

    private static int CompareDigits(string a, string b)
    {
        string x = a.TrimStart('0'), y = b.TrimStart('0');
        return x.Length != y.Length ? x.Length.CompareTo(y.Length) : string.CompareOrdinal(x, y);
    }
}
