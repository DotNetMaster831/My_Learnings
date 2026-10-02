using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// The kernel's implementation of the narrow surface a module sees.
/// </summary>
internal sealed class ModuleContext : IModuleContext, IAsyncDisposable
{
    private readonly IServiceScope _scope;

    internal ModuleContext(
        ModuleId moduleId,
        Version kernelVersion,
        IServiceScope scope,
        IModuleConfiguration configuration,
        ILogger logger)
    {
        ModuleId = moduleId;
        KernelVersion = kernelVersion;
        _scope = scope;
        Configuration = configuration;
        Logger = logger;
    }

    public ModuleId ModuleId { get; }

    public Version KernelVersion { get; }

    public IServiceProvider Services => _scope.ServiceProvider;

    public IModuleConfiguration Configuration { get; }

    public ILogger Logger { get; }

    public async ValueTask DisposeAsync()
    {
        if (_scope is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _scope.Dispose();
        }
    }
}
