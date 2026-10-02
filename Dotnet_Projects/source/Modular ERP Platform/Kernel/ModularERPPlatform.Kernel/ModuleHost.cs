using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// Drives the kernel: discover, resolve, load, initialise, activate — and unload.
/// This is the only type the shell talks to.
/// </summary>
public sealed class ModuleHost : IAsyncDisposable
{
    private static readonly Action<ILogger, string, Exception?> _logCannotUnload =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, nameof(ModuleHost)),
            "Cannot unload module {ModuleId}: not registered.");

    private static readonly Action<ILogger, Exception?> _logRescanFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(2, nameof(ModuleHost)),
            "Rescan after a plugin directory change failed.");

    private readonly KernelOptions _options;
    private readonly PluginDiscovery _discovery;
    private readonly DependencyResolver _resolver;
    private readonly LifecycleManager _lifecycle;
    private readonly ModuleRegistry _registry;
    private readonly PluginDirectoryWatcher _watcher;
    private readonly ILogger<ModuleHost> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _disposed;

    /// <summary>Initialises the module host.</summary>
    /// <param name="options">Kernel options.</param>
    /// <param name="discovery">Plugin discovery.</param>
    /// <param name="resolver">Dependency resolver.</param>
    /// <param name="lifecycle">Lifecycle manager.</param>
    /// <param name="registry">Module registry.</param>
    /// <param name="watcher">Plugin directory watcher.</param>
    /// <param name="logger">Logger.</param>
    public ModuleHost(
        IOptions<KernelOptions> options,
        PluginDiscovery discovery,
        DependencyResolver resolver,
        LifecycleManager lifecycle,
        ModuleRegistry registry,
        PluginDirectoryWatcher watcher,
        ILogger<ModuleHost> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Gets the registry, so callers can inspect module state.</summary>
    public ModuleRegistry Registry => _registry;

    /// <summary>Discovers, resolves and activates every module in the plugin directory.</summary>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>A task that completes when every loadable module is active or faulted.</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await ScanAndLoadAsync(cancellationToken).ConfigureAwait(false);

        if (_options.WatchPluginDirectory)
        {
            _watcher.DirectoryChanged += OnPluginDirectoryChanged;
            _watcher.Start();
        }
    }

    /// <summary>
    /// Rescans the plugin directory and brings any newly present module up to Active.
    /// Modules already registered are left alone.
    /// </summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>A task that completes when the scan is finished.</returns>
    public async Task ScanAndLoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            IReadOnlyList<DiscoveredModule> discovered = await _discovery
                .DiscoverAsync(cancellationToken)
                .ConfigureAwait(false);

            ResolutionResult resolution = _resolver.Resolve(discovered);

            foreach ((ModuleId id, string reason) in resolution.Blocked)
            {
                DiscoveredModule? module = discovered.FirstOrDefault(d => d.Manifest.Id == id);

                if (module is null)
                {
                    continue;
                }

                ModuleRegistration blockedRegistration = _registry.GetOrAdd(module);

                if (blockedRegistration.State is ModuleState.Discovered or ModuleState.Blocked)
                {
                    _lifecycle.Block(blockedRegistration, reason);
                }
            }

            foreach (DiscoveredModule module in resolution.LoadOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ModuleRegistration registration = _registry.GetOrAdd(module);

                if (registration.State != ModuleState.Discovered)
                {
                    continue;
                }

                bool loaded = await _lifecycle.LoadAsync(registration, cancellationToken)
                    .ConfigureAwait(false);

                if (!loaded)
                {
                    continue;
                }

                bool initialized = await _lifecycle.InitializeAsync(registration, cancellationToken)
                    .ConfigureAwait(false);

                if (!initialized)
                {
                    continue;
                }

                await _lifecycle.ActivateAsync(registration, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Unloads one module and removes it from the registry.</summary>
    /// <param name="moduleId">The module to unload.</param>
    /// <param name="cancellationToken">Cancels the unload.</param>
    /// <returns>
    /// A weak reference to the discarded load context, for a memory assertion, or
    /// <see langword="null"/> when the module was not loaded.
    /// </returns>
    public async Task<WeakReference?> UnloadAsync(
        ModuleId moduleId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!_registry.TryGet(moduleId, out ModuleRegistration? registration) || registration is null)
            {
                _logCannotUnload(_logger, moduleId.Value, null);
                return null;
            }

            if (registration.State == ModuleState.Active)
            {
                await _lifecycle.DeactivateAsync(registration, cancellationToken).ConfigureAwait(false);
            }

            WeakReference? reference = await _lifecycle
                .UnloadAsync(registration, cancellationToken)
                .ConfigureAwait(false);

            _registry.Remove(moduleId);

            return reference;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Deactivates and unloads every module.</summary>
    /// <param name="cancellationToken">Cancels the shutdown.</param>
    /// <returns>A task that completes when every module is unloaded.</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _watcher.DirectoryChanged -= OnPluginDirectoryChanged;

        foreach (ModuleRegistration registration in _registry.GetAll())
        {
            await UnloadAsync(registration.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnPluginDirectoryChanged(object? sender, EventArgs e)
        => _ = RescanSafelyAsync();

    private async Task RescanSafelyAsync()
    {
        try
        {
            await ScanAndLoadAsync(CancellationToken.None).ConfigureAwait(false);
        }
            catch (Exception ex)
            {
                _logRescanFailed(_logger, ex);
            }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await StopAsync(CancellationToken.None).ConfigureAwait(false);

        _watcher.Dispose();
        _gate.Dispose();
    }
}
