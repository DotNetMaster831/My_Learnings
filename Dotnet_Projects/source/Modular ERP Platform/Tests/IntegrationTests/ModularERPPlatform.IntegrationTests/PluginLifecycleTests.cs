using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ModularERPPlatform.Kernel;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.IntegrationTests;

/// <summary>
/// Exercises the real load path against the real sample plugin on disk.
/// The sample plugin is referenced by the test project only so it builds; every
/// interaction below goes through file paths and <see cref="IModule"/>, never through
/// a direct type reference, because a direct reference would pin the assembly in the
/// default load context and make the unload assertion meaningless.
/// </summary>
[TestFixture]
public sealed class PluginLifecycleTests
{
    private string _root = string.Empty;
    private string _pluginDirectory = string.Empty;
    private ServiceProvider _services = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"kernel-it-{Guid.NewGuid():N}");
        _pluginDirectory = Path.Combine(_root, "Plugins");

        Directory.CreateDirectory(_pluginDirectory);

        ServiceCollection services = new();
        //services.AddLogging(static b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddModuleKernel(options =>
        {
            options.PluginDirectory = _pluginDirectory;
            options.ConfigurationDirectory = Path.Combine(_root, "ModuleConfig");
            options.KernelVersion = new Version(1, 0, 0);
            options.WatchPluginDirectory = false;
        });

        _services = services.BuildServiceProvider();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _services.DisposeAsync();

        if (Directory.Exists(_root))
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                // A just-unloaded assembly file can stay locked briefly. Not a test failure.
            }
        }
    }

    private void StageSamplePlugin(string folderName = "sample")
    {
        string source = Path.GetDirectoryName(typeof(PluginLifecycleTests).Assembly.Location)!;
        string destination = Path.Combine(_pluginDirectory, folderName);

        Directory.CreateDirectory(destination);

        foreach (string file in new[]
                 {
                     "ModularERP.SamplePlugin.dll",
                     "ModularERP.SamplePlugin.deps.json",
                     "module.manifest.json",
                 })
        {
            string from = Path.Combine(source, file);

            if (File.Exists(from))
            {
                File.Copy(from, Path.Combine(destination, file), overwrite: true);
            }
        }
    }

    [Test]
    public async Task Module_on_disk_is_discovered_loaded_and_activated()
    {
        StageSamplePlugin();

        ModuleHost host = _services.GetRequiredService<ModuleHost>();
        await host.StartAsync();

        ModuleRegistration registration = host.Registry.GetAll().Should().ContainSingle().Subject;

        registration.Id.Value.Should().Be("sample");
        registration.State.Should().Be(ModuleState.Active);
        registration.LastError.Should().BeNull();
    }

    [Test]
    public async Task Unloading_a_module_releases_its_load_context()
    {
        StageSamplePlugin();

        ModuleHost host = _services.GetRequiredService<ModuleHost>();
        await host.StartAsync();

        WeakReference? contextReference = await host.UnloadAsync(ModuleId.Create("sample"));

        contextReference.Should().NotBeNull();

        for (int attempt = 0; attempt < 10 && contextReference!.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        contextReference!.IsAlive.Should().BeFalse(
            "the module's AssemblyLoadContext must be collectable after unload (FR-004, risk R1). " +
            "A live reference means something outside the context still holds a module type — " +
            "most often a static event subscription made across the boundary.");
    }

    [Test]
    public async Task Module_requiring_a_newer_kernel_is_blocked_with_a_stated_reason()
    {
        StageSamplePlugin();

        string manifestPath = Path.Combine(_pluginDirectory, "sample", "module.manifest.json");
        string manifest = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(
            manifestPath,
            manifest.Replace(">=1.0.0 <2.0.0", ">=2.0.0", StringComparison.Ordinal));

        ModuleHost host = _services.GetRequiredService<ModuleHost>();
        await host.StartAsync();

        ModuleRegistration registration = host.Registry.GetAll().Should().ContainSingle().Subject;

        registration.State.Should().Be(ModuleState.Blocked);
        registration.LastError.Should().Contain("requires kernel");
    }

    [Test]
    public async Task Module_directory_without_a_manifest_is_ignored()
    {
        Directory.CreateDirectory(Path.Combine(_pluginDirectory, "not-a-module"));

        ModuleHost host = _services.GetRequiredService<ModuleHost>();
        await host.StartAsync();

        host.Registry.GetAll().Should().BeEmpty();
    }

    [Test]
    public async Task Manifest_declaring_a_missing_assembly_is_rejected()
    {
        StageSamplePlugin();

        File.Delete(Path.Combine(_pluginDirectory, "sample", "ModularERP.SamplePlugin.dll"));

        ModuleHost host = _services.GetRequiredService<ModuleHost>();
        await host.StartAsync();

        host.Registry.GetAll().Should().BeEmpty();
    }

    [Test]
    public async Task Module_receives_configuration_scoped_to_itself()
    {
        StageSamplePlugin();

        ModuleHost host = _services.GetRequiredService<ModuleHost>();
        await host.StartAsync();

        ModuleConfigurationStore store = _services.GetRequiredService<ModuleConfigurationStore>();
        IModuleConfiguration config = store.For(ModuleId.Create("sample"));

        await config.SetAsync("probe", 42);

        (await config.GetAsync<int>("probe")).Should().Be(42);
        (await store.For(ModuleId.Create("other")).GetAsync<int?>("probe")).Should().BeNull();
    }
}
