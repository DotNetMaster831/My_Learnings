using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ModularERPPlatform.Kernel.Contracts;

/// <summary>
/// A semantic version constraint such as <c>&gt;=1.0.0</c> or <c>&gt;=1.0.0 &lt;2.0.0</c>.
/// Used to enfore plugin and kernel version compatibility. Satisfies FR-007
/// </summary>
public sealed record VersionRange
{
    private VersionRange(Version? minimum, bool minimumInclusive, Version? maximum, bool maximumInclusive)
    {
        Minimum = minimum;
        MinimumInclusive = minimumInclusive;
        Maximum = maximum;
        MaximumInclusive = maximumInclusive;
    }

    /// <summary>
    /// Gets range that accepts every version.
    /// </summary>
    public static VersionRange Any { get; } = new(null, false, null, false);

    /// <summary>
    /// Gets the lower bound, or <see langword="null"/> when unbounded below.
    /// </summary>
    public Version? Minimum { get; }

    /// <summary>Gets a value indicating whether <see cref="Minimum"/> itself is accepted.</summary>
    public bool MinimumInclusive { get; }

    /// <summary>Gets the upper bound, or <see langword="null"/> when unbounded above.</summary>
    public Version? Maximum { get; }

    /// <summary>Gets a value indicating whether <see cref="Maximum"/> itself is accepted.</summary>
    public bool MaximumInclusive { get; }


    /// <summary>
    /// Parses a version range expression.
    /// </summary>
    /// <param name="expression">
    /// Whitespace-separated comparators. Supported: <c>&gt;=</c>, <c>&gt;</c>, <c>&lt;=</c>,
    /// <c>&lt;</c>, <c>=</c>. A bare version means exact match.
    /// </param>
    /// <returns>The parsed range.</returns>
    /// <exception cref="FormatException">The expression is not a legal range.</exception>
    public static VersionRange Parse(string expression)
    {
        if(!TryParse(expression, out VersionRange? range, out string? error))
        {
            throw new FormatException(error);
        }
        return range;
    }

    /// <summary>
    /// Attempts to parse a range expression.
    /// </summary>
    /// <param name="expression">The expression to parse.</param>
    /// <param name="range">The parsed range when parsing succeeds.</param>
    /// <param name="error">A human-readable reason when parsing fails.</param>
    /// <returns><see langword="true"/>When the expression is legal range.</returns>
    public static bool TryParse(string? expression, out VersionRange range, out string? error)
    {
        range = Any;
        error = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            return true;
        }

        Version? min = null;
        Version? max = null;
        bool minInclusive = false;
        bool maxInclusive = false;

        string[] tokens = expression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach(string token in tokens)
        {
            string op;
            string versionText;

            if(token.StartsWith(">=", StringComparison.Ordinal) || token.StartsWith("<=", StringComparison.Ordinal))
            {
                op = token[..2];
                versionText = token[2..];
            }
            else if (token.StartsWith('>') || token.StartsWith('<') || token.StartsWith('='))
            {
                op = token[..1];
                versionText = token[1..];
            }
            else
            {
                op = "=";
                versionText = token;
            }

            if (!Version.TryParse(versionText.Trim(), out Version? parsed))
            {
                error = $"'{versionText}' in range '{expression}' is not a valid version.";
                return false;
            }

            switch (op)
            {
                case ">=":
                    min = parsed;
                    minInclusive = true;
                    break;
                case ">":
                    min = parsed;
                    minInclusive = false;
                    break;
                case "<=":
                    max = parsed;
                    maxInclusive = true;
                    break;
                case "<":
                    max = parsed;
                    maxInclusive = false;
                    break;
                case "=":
                    min = parsed;
                    max = parsed;
                    minInclusive = true;
                    maxInclusive = true;
                    break;
                default:
                    error = $"Unsupported comparator '{op}' in range '{expression}'.";
                    return false;
            }
        }

        if (min is not null && max is not null && min > max)
        {
            error = $"Range '{expression}' is unsatisfiable: lower bound exceeds upper bound.";
            return false;
        }

        range = new VersionRange(min, minInclusive, max, maxInclusive);
        return true;
    }

    /// <summary>Determines whether a version falls inside this range.</summary>
    /// <param name="version">The version to test.</param>
    /// <returns><see langword="true"/> when the version satisfies every bound.</returns>
    public bool Satisfies(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if(Minimum is not null)
        {
            int compare = version.CompareTo(Minimum);
            if (compare < 0 || (compare == 0 && !MinimumInclusive))
            {
                return false;
            }
        }
        if (Maximum is not null)
        {
            int compare = version.CompareTo(Maximum);
            if (compare > 0 || (compare == 0 && !MaximumInclusive))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        if (Minimum is null && Maximum is null)
        {
            return "*";
        }

        if (Minimum is not null && Minimum == Maximum && MinimumInclusive && MaximumInclusive)
        {
            return Minimum.ToString();
        }

        List<string> parts = new(2);

        if (Minimum is not null)
        {
            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{(MinimumInclusive ? ">=" : ">")}{Minimum}"));
        }

        if (Maximum is not null)
        {
            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{(MaximumInclusive ? "<=" : "<")}{Maximum}"));
        }

        return string.Join(' ', parts);
    }
}
