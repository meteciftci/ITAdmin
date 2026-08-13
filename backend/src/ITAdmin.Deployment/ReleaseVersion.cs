using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ITAdmin.Deployment;

/// <summary>
/// A release's version identity: <c>MAJOR.MINOR.PATCH</c> with an optional pre-release label.
///
/// Deliberately narrower than full SemVer. A release version is used for directory names, for
/// deciding whether an artifact is an upgrade, a rerun, or a downgrade, and for operator-facing
/// display — so it must round-trip exactly and must never produce a string that is unsafe as a
/// Windows path segment.
/// </summary>
public sealed class ReleaseVersion : IComparable<ReleaseVersion>, IEquatable<ReleaseVersion>
{
    private ReleaseVersion(int major, int minor, int patch, string? preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    /// <summary>Pre-release label without the leading hyphen, or null for a stable release.</summary>
    public string? PreRelease { get; }

    public bool IsPreRelease => PreRelease is not null;

    public static bool TryParse(string? value, [NotNullWhen(true)] out ReleaseVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();

        // A leading "v" is common in Git tags; accept it but never emit it.
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        var preReleaseSeparator = text.IndexOf('-');
        string? preRelease = null;
        if (preReleaseSeparator >= 0)
        {
            preRelease = text[(preReleaseSeparator + 1)..];
            text = text[..preReleaseSeparator];

            if (preRelease.Length == 0 || !preRelease.All(IsPreReleaseCharacter))
            {
                return false;
            }
        }

        var parts = text.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!TryParseComponent(parts[0], out var major)
            || !TryParseComponent(parts[1], out var minor)
            || !TryParseComponent(parts[2], out var patch))
        {
            return false;
        }

        version = new ReleaseVersion(major, minor, patch, preRelease);
        return true;
    }

    public static ReleaseVersion Parse(string value) =>
        TryParse(value, out var version)
            ? version
            : throw new FormatException($"'{value}' is not a valid ITAdmin release version (expected MAJOR.MINOR.PATCH[-prerelease]).");

    private static bool IsPreReleaseCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '.' or '-';

    private static bool TryParseComponent(string text, out int value)
    {
        value = 0;

        // Reject "+1", "1 ", "01" and other shapes that would not round-trip to the same string.
        if (text.Length == 0 || !text.All(char.IsAsciiDigit))
        {
            return false;
        }

        if (text.Length > 1 && text[0] == '0')
        {
            return false;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        // A stable release always outranks a pre-release of the same number (1.0.0 > 1.0.0-rc.1).
        return (PreRelease, other.PreRelease) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            var (left, right) => string.CompareOrdinal(left, right),
        };
    }

    public bool Equals(ReleaseVersion? other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is ReleaseVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease);

    public override string ToString() =>
        PreRelease is null
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Major}.{Minor}.{Patch}-{PreRelease}";
}
