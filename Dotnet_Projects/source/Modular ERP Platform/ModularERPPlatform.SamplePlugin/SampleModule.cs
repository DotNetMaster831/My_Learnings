using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERP.SamplePlugin;

/// <summary>
/// A minimal module used to exercise discovery, load, lifecycle and unload.
/// Deliberately holds nothing static, so its load context stays collectible.
/// </summary>
public sealed class SampleModule : IModule, IHealthCheck
{
    private static readonly Action<ILogger, string, Exception?> _logInitialising =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(SampleModule)),
            "Sample module initialising against kernel {KernelVersion}.");

    private static readonly Action<ILogger, Exception?> _logActive =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, nameof(SampleModule)),
            "Sample module active.");

    private static readonly Action<ILogger, Exception?> _logDeactivated =
        LoggerMessage.Define(LogLevel.Information, new EventId(3, nameof(SampleModule)),
            "Sample module deactivated.");

    private static readonly Action<ILogger, Exception?> _logShuttingDown =
        LoggerMessage.Define(LogLevel.Information, new EventId(4, nameof(SampleModule)),
            "Sample module shutting down.");

    private IModuleContext? _context;

    /// <inheritdoc />
    public ModuleId Id { get; } = ModuleId.Create("sample");

    /// <inheritdoc />
    public Version Version { get; } = new(1, 0, 0);

    /// <inheritdoc />
    public Task InitializeAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        _logInitialising(_context.Logger, context.KernelVersion.ToString(), null);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ActivateAsync(CancellationToken cancellationToken)
    {
        if (_context is not null)
        {
            _logActive(_context.Logger, null);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeactivateAsync(CancellationToken cancellationToken)
    {
        if (_context is not null)
        {
            _logDeactivated(_context.Logger, null);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShutdownAsync(CancellationToken cancellationToken)
    {
        if (_context is not null)
        {
            _logShuttingDown(_context.Logger, null);
        }

        // Dropping the context reference is what lets the load context be collected.
        _context = null;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<HealthResult> CheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(HealthResult.Healthy("Sample module has nothing to report."));
}
