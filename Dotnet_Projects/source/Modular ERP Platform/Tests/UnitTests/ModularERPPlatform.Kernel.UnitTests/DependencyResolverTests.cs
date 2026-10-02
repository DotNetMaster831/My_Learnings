using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel.UnitTests;

[TestFixture]
public sealed class DependencyResolverTests
{
    private static DependencyResolver CreateResolver(Version? kernelVersion = null)
        => new(
            Options.Create(new KernelOptions
            {
                KernelVersion = kernelVersion ?? new Version(1, 0, 0),
            }),
            NullLogger<DependencyResolver>.Instance);

    private static DiscoveredModule Module(
        string id,
        string version = "1.0.0",
        string kernelRange = "*",
        params (string Id, string Range)[] dependencies)
    {
        ModuleManifest manifest = new(
            ModuleId.Create(id),
            Version.Parse(version),
            id,
            $"{id}.dll",
            kernelRange == "*" ? VersionRange.Any : VersionRange.Parse(kernelRange),
            dependencies
                .Select(d => new ModuleDependency(ModuleId.Create(d.Id), VersionRange.Parse(d.Range)))
                .ToArray(),
            []);

        return new DiscoveredModule(manifest, $@"C:\Plugins\{id}", $@"C:\Plugins\{id}\{id}.dll");
    }

    [Test]
    public void Dependencies_load_before_dependents()
    {
        DiscoveredModule a = Module("a");
        DiscoveredModule b = Module("b", dependencies: ("a", ">=1.0.0"));

        ResolutionResult result = CreateResolver().Resolve([b, a]);

        result.Blocked.Should().BeEmpty();
        result.LoadOrder.Select(m => m.Manifest.Id.Value)
            .Should().ContainInOrder("a", "b");
    }

    [Test]
    public void Circular_dependency_blocks_every_member_and_names_the_cycle()
    {
        DiscoveredModule a = Module("a", dependencies: ("b", ">=1.0.0"));
        DiscoveredModule b = Module("b", dependencies: ("a", ">=1.0.0"));

        ResolutionResult result = CreateResolver().Resolve([a, b]);

        result.LoadOrder.Should().BeEmpty();
        result.Blocked.Should().HaveCount(2);
        result.Blocked[ModuleId.Create("a")].Should().Contain("Circular dependency");
        result.Blocked[ModuleId.Create("a")].Should().Contain("->");
    }

    [Test]
    public void Missing_dependency_blocks_the_dependent()
    {
        DiscoveredModule b = Module("b", dependencies: ("a", ">=1.0.0"));

        ResolutionResult result = CreateResolver().Resolve([b]);

        result.LoadOrder.Should().BeEmpty();
        result.Blocked[ModuleId.Create("b")].Should().Contain("was not found");
    }

    [Test]
    public void Dependency_version_outside_range_blocks_the_dependent()
    {
        DiscoveredModule a = Module("a", version: "1.0.0");
        DiscoveredModule b = Module("b", dependencies: ("a", ">=2.0.0"));

        ResolutionResult result = CreateResolver().Resolve([a, b]);

        result.Blocked[ModuleId.Create("b")].Should().Contain("available version is 1.0.0");
        result.LoadOrder.Select(m => m.Manifest.Id.Value).Should().ContainSingle().Which.Should().Be("a");
    }

    [Test]
    public void Kernel_version_constraint_is_enforced()
    {
        DiscoveredModule needsKernel2 = Module("a", kernelRange: ">=2.0.0");

        ResolutionResult result = CreateResolver(new Version(1, 9, 0)).Resolve([needsKernel2]);

        result.LoadOrder.Should().BeEmpty();
        result.Blocked[ModuleId.Create("a")].Should().Contain("running kernel is 1.9");
    }

    [Test]
    public void Blocking_propagates_to_dependents()
    {
        DiscoveredModule a = Module("a", kernelRange: ">=9.0.0");
        DiscoveredModule b = Module("b", dependencies: ("a", ">=1.0.0"));

        ResolutionResult result = CreateResolver().Resolve([a, b]);

        result.LoadOrder.Should().BeEmpty();
        result.Blocked[ModuleId.Create("b")].Should().Contain("dependency 'a' is blocked");
    }

    [Test]
    public void Duplicate_module_ids_are_blocked()
    {
        DiscoveredModule first = Module("a");
        DiscoveredModule second = Module("a");

        ResolutionResult result = CreateResolver().Resolve([first, second]);

        result.Blocked[ModuleId.Create("a")].Should().Contain("declared twice");
    }
}
