using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel.UnitTests;

[TestFixture]
public sealed class ModuleIdTests
{
    [Test]
    public void Comparison_ignores_case()
    {
        ModuleId.Create("Finance").Should().Be(ModuleId.Create("finance"));
    }

    [Test]
    public void Illegal_characters_are_rejected()
    {
        bool created = ModuleId.TryCreate("finance module", out _, out string? error);

        created.Should().BeFalse();
        error.Should().Contain("illegal character");
    }

    [Test]
    public void Empty_id_is_rejected()
    {
        ModuleId.TryCreate("   ", out _).Should().BeFalse();
    }

    [Test]
    public void Uninitialised_id_reports_empty()
    {
        ModuleId id = default;

        id.IsEmpty.Should().BeTrue();
    }
}
