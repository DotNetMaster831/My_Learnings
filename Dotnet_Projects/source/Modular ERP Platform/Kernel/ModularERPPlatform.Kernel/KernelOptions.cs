using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// Host-supplied settings for the module kernel.
/// </summary>
public sealed class KernelOptions
{
    /// <summary>Gets or sets the directory scanned for plugins. One subdirectory per module.</summary>
    public string PluginDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Plugins");

    /// <summary>Gets or sets the directory holding per-module configuration files.</summary>
    public string ConfigurationDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "ModuleConfig");

    /// <summary>
    /// Gets or sets the kernel version modules are validated against. Satisfies FR-007.
    /// </summary>
    public Version KernelVersion { get; set; } = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    /// <summary>
    /// Gets or sets a value indicating whether the plugin directory is watched for
    /// drop-in modules after startup. Satisfies FR-003.
    /// </summary>
    public bool WatchPluginDirectory { get; set; } = true;

    /// <summary>
    /// Gets or sets how long the watcher waits for file activity to settle before
    /// reading a manifest. A copied plugin arrives as several file events.
    /// </summary>
    public TimeSpan WatchDebounce { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the file name of a module manifest inside a plugin directory.</summary>
    public string ManifestFileName { get; set; } = "module.manifest.json";

    /// <summary>Gets or sets how long a single lifecycle call may run before it is cancelled.</summary>
    public TimeSpan LifecycleTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
