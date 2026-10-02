using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// Owns every module state transition and refuses illegal ones. Satisfies FR-008.
/// </summary>
public sealed class LifecycleManager
{
    private static readonly Action<ILogger, string, string, Exception?> _logCannotLoadTransition =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1, nameof(LifecycleManager)),
            "Cannot load module {ModuleId}: transition {From} -> Loaded is not legal.");

    private static readonly Action<ILogger, string, string, Exception?> _logModuleLoaded =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(2, nameof(LifecycleManager)),
            "Module {ModuleId} v{ModuleVersion} loaded into its own load context.");

    private static readonly Action<ILogger, string, string, Exception?> _logCannotInitialiseTransition =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(3, nameof(LifecycleManager)),
            "Cannot initialise module {ModuleId}: transition {From} -> Initialized is not legal.");

    private static readonly Action<ILogger, string, Exception?> _logModuleInitialised =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, nameof(LifecycleManager)),
            "Module {ModuleId} initialised.");

    private static readonly Action<ILogger, string, string, Exception?> _logCannotActivateTransition =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(5, nameof(LifecycleManager)),
            "Cannot activate module {ModuleId}: transition {From} -> Active is not legal.");

    private static readonly Action<ILogger, string, Exception?> _logModuleActivated =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, nameof(LifecycleManager)),
            "Module {ModuleId} activated.");

    private static readonly Action<ILogger, string, string, Exception?> _logCannotDeactivateTransition =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(7, nameof(LifecycleManager)),
            "Cannot deactivate module {ModuleId}: transition {From} -> Deactivated is not legal.");

    private static readonly Action<ILogger, string, Exception?> _logModuleDeactivated =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(8, nameof(LifecycleManager)),
            "Module {ModuleId} deactivated.");

    private static readonly Action<ILogger, string, Exception?> _logModuleShutdownThrew =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(9, nameof(LifecycleManager)),
            "Module {ModuleId} threw during shutdown; unloading anyway.");

    private static readonly Action<ILogger, string, Exception?> _logModuleUnloaded =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(10, nameof(LifecycleManager)),
            "Module {ModuleId} unloaded; load context discarded.");

    private static readonly Action<ILogger, string, string, Exception?> _logModuleBlocked =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(11, nameof(LifecycleManager)),
            "Module {ModuleId} blocked. {Reason}");

    private static readonly Action<ILogger, string, string, Exception?> _logModuleFaulted =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(12, nameof(LifecycleManager)),
            "Module {ModuleId} faulted. {Reason}");

    private static readonly Dictionary<ModuleState, ModuleState[]> LegalTransitions = new()
    {
        [ModuleState.Discovered] = [ModuleState.Loaded, ModuleState.Blocked, ModuleState.Faulted, ModuleState.Unloaded],
        [ModuleState.Blocked] = [ModuleState.Discovered, ModuleState.Unloaded],
        [ModuleState.Loaded] = [ModuleState.Initialized, ModuleState.Faulted, ModuleState.Unloaded],
        [ModuleState.Initialized] = [ModuleState.Active, ModuleState.Faulted, ModuleState.Unloaded],
        [ModuleState.Active] = [ModuleState.Deactivated, ModuleState.Faulted],
        [ModuleState.Deactivated] = [ModuleState.Active, ModuleState.Faulted, ModuleState.Unloaded],
        [ModuleState.Faulted] = [ModuleState.Unloaded],
        [ModuleState.Unloaded] = [],
    };

    private readonly KernelOptions _options;
    private readonly IServiceProvider _services;
    private readonly ModuleConfigurationStore _configurationStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<LifecycleManager> _logger;

    /// <summary>Initialises the lifecycle manager.</summary>
    /// <param name="options">Kernel options.</param>
    /// <param name="services">Root service provider, used to create per-module scopes.</param>
    /// <param name="configurationStore">Per-module configuration store.</param>
    /// <param name="loggerFactory">Factory used to build per-module loggers.</param>
    /// <param name="logger">Logger.</param>
    public LifecycleManager(
        IOptions<KernelOptions> options,
        IServiceProvider services,
        ModuleConfigurationStore configurationStore,
        ILoggerFactory loggerFactory,
        ILogger<LifecycleManager> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Determines whether a transition between two states is legal.</summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The proposed state.</param>
    /// <returns><see langword="true"/> when the transition is permitted.</returns>
    public static bool IsLegalTransition(ModuleState from, ModuleState to)
        => LegalTransitions.TryGetValue(from, out ModuleState[]? allowed) && allowed.Contains(to);

    /// <summary>Loads a module's assembly into its own collectible context.</summary>
    /// <param name="registration">The module to load.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns><see langword="true"/> when the module reached <see cref="ModuleState.Loaded"/>.</returns>
    public Task<bool> LoadAsync(
        ModuleRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!IsLegalTransition(registration.State, ModuleState.Loaded))
        {
            _logCannotLoadTransition(_logger, registration.Id.Value, registration.State.ToString(), null);

            return Task.FromResult(false);
        }

        try
        {
            ModuleLoadContext loadContext = new(
                $"ModuleLoadContext:{registration.Id}",
                registration.AssemblyPath);

            System.Reflection.Assembly assembly =
                loadContext.LoadFromAssemblyPath(registration.AssemblyPath);

            Type? moduleType = assembly
                .GetTypes()
                .FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t)
                                     && t is { IsAbstract: false, IsInterface: false });

            if (moduleType is null)
            {
                loadContext.Unload();
                Fault(registration, $"Assembly '{registration.Manifest.AssemblyFile}' contains no IModule implementation.");
                return Task.FromResult(false);
            }

            if (Activator.CreateInstance(moduleType) is not IModule instance)
            {
                loadContext.Unload();
                Fault(registration, $"Type '{moduleType.FullName}' could not be constructed as an IModule.");
                return Task.FromResult(false);
            }

            if (instance.Id != registration.Manifest.Id)
            {
                loadContext.Unload();
                Fault(
                    registration,
                    $"Module reports id '{instance.Id}' but its manifest declares '{registration.Manifest.Id}'.");
                return Task.FromResult(false);
            }

            if (instance.Version != registration.Manifest.Version)
            {
                loadContext.Unload();
                Fault(
                    registration,
                    $"Module reports version '{instance.Version}' but its manifest declares '{registration.Manifest.Version}'.");
                return Task.FromResult(false);
            }

            registration.LoadContext = loadContext;
            registration.Instance = instance;
            registration.SetState(ModuleState.Loaded);

            _logModuleLoaded(_logger, registration.Id.Value, registration.Manifest.Version.ToString(), null);

            return Task.FromResult(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Fault(registration, $"Load failed: {ex.Message}", ex);
            return Task.FromResult(false);
        }
    }

    /// <summary>Initialises a loaded module, handing it its context.</summary>
    /// <param name="registration">The module to initialise.</param>
    /// <param name="cancellationToken">Cancels initialisation.</param>
    /// <returns><see langword="true"/> when the module reached <see cref="ModuleState.Initialized"/>.</returns>
    public async Task<bool> InitializeAsync(
        ModuleRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!IsLegalTransition(registration.State, ModuleState.Initialized))
        {
            _logCannotInitialiseTransition(_logger, registration.Id.Value, registration.State.ToString(), null);

            return false;
        }

        IModule instance = registration.Instance
            ?? throw new InvalidOperationException($"Module '{registration.Id}' is Loaded but has no instance.");

        try
        {
            IServiceScope scope = _services.CreateScope();

            ModuleContext context = new(
                registration.Id,
                _options.KernelVersion,
                scope,
                _configurationStore.For(registration.Id),
                _loggerFactory.CreateLogger($"Module.{registration.Id}"));

            registration.Context = context;

            using CancellationTokenSource timeout = CreateTimeout(cancellationToken);

            await instance.InitializeAsync(context, timeout.Token).ConfigureAwait(false);

            registration.SetState(ModuleState.Initialized);

            _logModuleInitialised(_logger, registration.Id.Value, null);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || cancellationToken.IsCancellationRequested)
        {
            Fault(registration, $"Initialize failed: {ex.Message}", ex);
            return false;
        }
        catch (OperationCanceledException)
        {
            Fault(registration, $"Initialize exceeded {_options.LifecycleTimeout.TotalSeconds:N0}s and was cancelled.");
            return false;
        }
    }

    /// <summary>Activates an initialised or deactivated module.</summary>
    /// <param name="registration">The module to activate.</param>
    /// <param name="cancellationToken">Cancels activation.</param>
    /// <returns><see langword="true"/> when the module reached <see cref="ModuleState.Active"/>.</returns>
    public async Task<bool> ActivateAsync(
        ModuleRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!IsLegalTransition(registration.State, ModuleState.Active))
        {
            _logCannotActivateTransition(_logger, registration.Id.Value, registration.State.ToString(), null);

            return false;
        }

        IModule instance = registration.Instance
            ?? throw new InvalidOperationException($"Module '{registration.Id}' has no instance.");

        try
        {
            using CancellationTokenSource timeout = CreateTimeout(cancellationToken);

            await instance.ActivateAsync(timeout.Token).ConfigureAwait(false);

            registration.SetState(ModuleState.Active);

            _logModuleActivated(_logger, registration.Id.Value, null);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || cancellationToken.IsCancellationRequested)
        {
            Fault(registration, $"Activate failed: {ex.Message}", ex);
            return false;
        }
        catch (OperationCanceledException)
        {
            Fault(registration, $"Activate exceeded {_options.LifecycleTimeout.TotalSeconds:N0}s and was cancelled.");
            return false;
        }
    }

    /// <summary>Deactivates an active module, leaving it loaded.</summary>
    /// <param name="registration">The module to deactivate.</param>
    /// <param name="cancellationToken">Cancels deactivation.</param>
    /// <returns><see langword="true"/> when the module reached <see cref="ModuleState.Deactivated"/>.</returns>
    public async Task<bool> DeactivateAsync(
        ModuleRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!IsLegalTransition(registration.State, ModuleState.Deactivated))
        {
            _logCannotDeactivateTransition(_logger, registration.Id.Value, registration.State.ToString(), null);

            return false;
        }

        IModule instance = registration.Instance
            ?? throw new InvalidOperationException($"Module '{registration.Id}' has no instance.");

        try
        {
            using CancellationTokenSource timeout = CreateTimeout(cancellationToken);

            await instance.DeactivateAsync(timeout.Token).ConfigureAwait(false);

            registration.SetState(ModuleState.Deactivated);

            _logModuleDeactivated(_logger, registration.Id.Value, null);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || cancellationToken.IsCancellationRequested)
        {
            Fault(registration, $"Deactivate failed: {ex.Message}", ex);
            return false;
        }
        catch (OperationCanceledException)
        {
            Fault(registration, $"Deactivate exceeded {_options.LifecycleTimeout.TotalSeconds:N0}s and was cancelled.");
            return false;
        }
    }

    /// <summary>
    /// Shuts a module down and discards its load context, so the runtime can reclaim
    /// its memory. Satisfies FR-004.
    /// </summary>
    /// <param name="registration">The module to unload.</param>
    /// <param name="cancellationToken">Cancels shutdown.</param>
    /// <returns>
    /// A weak reference to the discarded load context. The caller collects and then asserts
    /// this reference is dead; a live reference means something outside the context still
    /// holds a module type, which is risk R1.
    /// </returns>
    public async Task<WeakReference?> UnloadAsync(
        ModuleRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (registration.State == ModuleState.Unloaded)
        {
            return null;
        }

        IModule? instance = registration.Instance;

        if (instance is not null && registration.State is not (ModuleState.Discovered or ModuleState.Blocked))
        {
            try
            {
                using CancellationTokenSource timeout = CreateTimeout(cancellationToken);
                await instance.ShutdownAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logModuleShutdownThrew(_logger, registration.Id.Value, ex);
            }
        }

        if (registration.Context is not null)
        {
            await registration.Context.DisposeAsync().ConfigureAwait(false);
            registration.Context = null;
        }

        ModuleLoadContext? loadContext = registration.LoadContext;

        registration.Instance = null;
        registration.LoadContext = null;
        registration.SetState(ModuleState.Unloaded);

        if (loadContext is null)
        {
            return null;
        }

        WeakReference reference = new(loadContext, trackResurrection: false);
        loadContext.Unload();

        _logModuleUnloaded(_logger, registration.Id.Value, null);

        return reference;
    }

    /// <summary>Moves a module to <see cref="ModuleState.Blocked"/> with a stated reason.</summary>
    /// <param name="registration">The module to block.</param>
    /// <param name="reason">Why it cannot load.</param>
    public void Block(ModuleRegistration registration, string reason)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        registration.SetState(ModuleState.Blocked, reason);

        _logModuleBlocked(_logger, registration.Id.Value, reason, null);
    }

    private CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(_options.LifecycleTimeout);
        return source;
    }

    private void Fault(ModuleRegistration registration, string reason, Exception? exception = null)
    {
        registration.SetState(ModuleState.Faulted, reason);

        if (exception is null)
        {
            _logModuleFaulted(_logger, registration.Id.Value, reason, null);
        }
        else
        {
            _logModuleFaulted(_logger, registration.Id.Value, reason, exception);
        }
    }
}
