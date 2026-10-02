using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// Scans the plugin directory and returns the modules found there.
/// Reads manifests only; loading happens later. Satisfies FR-001 and FR-002.
/// </summary>
public sealed class PluginDiscovery
{
    private static readonly Action<ILogger, string, Exception?> _logPluginDirectoryCreated =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(PluginDiscovery)),
            "Plugin directory {PluginDirectory} does not exist; creating it.");

    private static readonly Action<ILogger, int, string, Exception?> _logDiscoveredSummary =
        LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(2, nameof(PluginDiscovery)),
            "Discovered {ModuleCount} module(s) in {PluginDirectory}.");

    private static readonly Action<ILogger, string, string, Exception?> _logDirectoryMissingManifest =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(3, nameof(PluginDiscovery)),
            "Directory {Directory} has no {ManifestFileName}; ignored.");

    private static readonly Action<ILogger, string, Exception?> _logModuleRejected =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, nameof(PluginDiscovery)),
            "Module rejected. {Reason}");

    private static readonly Action<ILogger, string, string, string, Exception?> _logAssemblyMissing =
        LoggerMessage.Define<string, string, string>(LogLevel.Error, new EventId(5, nameof(PluginDiscovery)),
            "Module {ModuleId} declares assembly {AssemblyFile} which is not present in {Directory}; rejected.");

    private static readonly Action<ILogger, string, string, string, Exception?> _logModuleDiscovered =
        LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(6, nameof(PluginDiscovery)),
            "Discovered module {ModuleId} v{ModuleVersion} at {Directory}.");

    private readonly KernelOptions _options;
    private readonly ManifestReader _manifestReader;
    private readonly ILogger<PluginDiscovery> _logger;

    /// <summary>Initialises plugin discovery.</summary>
    /// <param name="options">Kernel options.</param>
    /// <param name="manifestReader">Manifest reader.</param>
    /// <param name="logger">Logger.</param>
    public PluginDiscovery(
        IOptions<KernelOptions> options,
        ManifestReader manifestReader,
        ILogger<PluginDiscovery> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _manifestReader = manifestReader ?? throw new ArgumentNullException(nameof(manifestReader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Scans the whole plugin directory.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>Every module whose manifest validated.</returns>
    public async Task<IReadOnlyList<DiscoveredModule>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_options.PluginDirectory))
        {
            Directory.CreateDirectory(_options.PluginDirectory);

            _logPluginDirectoryCreated(_logger, _options.PluginDirectory, null);

            return Array.Empty<DiscoveredModule>();
        }

        List<DiscoveredModule> discovered = new();

        foreach (string directory in Directory.EnumerateDirectories(_options.PluginDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            DiscoveredModule? module = await DiscoverDirectoryAsync(directory, cancellationToken)
                .ConfigureAwait(false);

            if (module is not null)
            {
                discovered.Add(module);
            }
        }

        _logDiscoveredSummary(_logger, discovered.Count, _options.PluginDirectory, null);

        return discovered;
    }

    /// <summary>Scans one plugin directory.</summary>
    /// <param name="directoryPath">The directory to inspect.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The module found there, or <see langword="null"/> when it was rejected.</returns>
    public async Task<DiscoveredModule?> DiscoverDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        string manifestPath = Path.Combine(directoryPath, _options.ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            _logDirectoryMissingManifest(_logger, directoryPath, _options.ManifestFileName, null);

            return null;
        }

        ManifestReadResult result = await ManifestReader.ReadAsync(manifestPath, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsAccepted)
        {
            _logModuleRejected(_logger, result.RejectionReason ?? string.Empty, null);
            return null;
        }

        ModuleManifest manifest = result.Manifest!;
        string assemblyPath = Path.Combine(directoryPath, manifest.AssemblyFile);

        if (!File.Exists(assemblyPath))
        {
            _logAssemblyMissing(_logger, manifest.Id.Value, manifest.AssemblyFile, directoryPath, null);

            return null;
        }

        _logModuleDiscovered(_logger, manifest.Id.Value, manifest.Version.ToString(), directoryPath, null);

        return new DiscoveredModule(manifest, directoryPath, assemblyPath);
    }
}
