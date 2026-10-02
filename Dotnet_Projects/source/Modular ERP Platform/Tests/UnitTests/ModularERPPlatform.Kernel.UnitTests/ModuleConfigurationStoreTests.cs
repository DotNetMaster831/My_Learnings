using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel.UnitTests;

[TestFixture]
public sealed class ModuleConfigurationStoreTests
{
    private string _directory = string.Empty;
    private ModuleConfigurationStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"module-config-{Guid.NewGuid():N}");

        _store = new ModuleConfigurationStore(Options.Create(new KernelOptions
        {
            ConfigurationDirectory = _directory,
        }));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public async Task Value_round_trips()
    {
        IModuleConfiguration config = _store.For(ModuleId.Create("finance"));

        await config.SetAsync("threshold", 5000);

        (await config.GetAsync<int>("threshold")).Should().Be(5000);
    }

    [Test]
    public async Task Absent_key_returns_default()
    {
        IModuleConfiguration config = _store.For(ModuleId.Create("finance"));

        (await config.GetAsync<string>("nothing")).Should().BeNull();
    }

    [Test]
    public async Task One_module_cannot_read_another_modules_value()
    {
        IModuleConfiguration finance = _store.For(ModuleId.Create("finance"));
        IModuleConfiguration hr = _store.For(ModuleId.Create("hr"));

        await finance.SetAsync("secret", "finance-only");

        (await hr.GetAsync<string>("secret")).Should().BeNull();
        (await hr.GetKeysAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task Removal_reports_whether_anything_was_removed()
    {
        IModuleConfiguration config = _store.For(ModuleId.Create("finance"));

        await config.SetAsync("temp", "x");

        (await config.RemoveAsync("temp")).Should().BeTrue();
        (await config.RemoveAsync("temp")).Should().BeFalse();
    }

    [Test]
    public void Store_reports_its_owner()
    {
        _store.For(ModuleId.Create("finance")).Owner.Should().Be(ModuleId.Create("finance"));
    }

    [Test]
    public void Uninitialised_module_id_is_refused()
    {
        Action act = () => _store.For(default);

        act.Should().Throw<ArgumentException>();
    }
}
