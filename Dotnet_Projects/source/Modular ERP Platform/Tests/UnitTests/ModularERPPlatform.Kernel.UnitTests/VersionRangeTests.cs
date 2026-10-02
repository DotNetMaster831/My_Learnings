using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel.UnitTests;

[TestFixture]
public sealed class VersionRangeTests
{
    [Test]
    public void Any_accepts_every_version()
    {
        VersionRange.Any.Satisfies(new Version(0, 0, 1)).Should().BeTrue();
        VersionRange.Any.Satisfies(new Version(99, 0, 0)).Should().BeTrue();
    }

    [Test]
    public void Lower_bound_is_inclusive_for_greater_or_equal()
    {
        VersionRange range = VersionRange.Parse(">=2.0.0");

        range.Satisfies(new Version(2, 0, 0)).Should().BeTrue();
        range.Satisfies(new Version(1, 9, 9)).Should().BeFalse();
    }

    [Test]
    public void Upper_bound_is_exclusive_for_less_than()
    {
        VersionRange range = VersionRange.Parse("<2.0.0");

        range.Satisfies(new Version(1, 9, 9)).Should().BeTrue();
        range.Satisfies(new Version(2, 0, 0)).Should().BeFalse();
    }

    [Test]
    public void Both_bounds_are_applied()
    {
        VersionRange range = VersionRange.Parse(">=1.0.0 <2.0.0");

        range.Satisfies(new Version(1, 5, 0)).Should().BeTrue();
        range.Satisfies(new Version(0, 9, 0)).Should().BeFalse();
        range.Satisfies(new Version(2, 0, 0)).Should().BeFalse();
    }

    [Test]
    public void Bare_version_means_exact_match()
    {
        VersionRange range = VersionRange.Parse("1.2.3");

        range.Satisfies(new Version(1, 2, 3)).Should().BeTrue();
        range.Satisfies(new Version(1, 2, 4)).Should().BeFalse();
    }

    [Test]
    public void Unsatisfiable_range_is_rejected()
    {
        bool parsed = VersionRange.TryParse(">=3.0.0 <2.0.0", out _, out string? error);

        parsed.Should().BeFalse();
        error.Should().Contain("unsatisfiable");
    }

    [Test]
    public void Non_version_text_is_rejected()
    {
        bool parsed = VersionRange.TryParse(">=banana", out _, out string? error);

        parsed.Should().BeFalse();
        error.Should().Contain("not a valid version");
    }
}
