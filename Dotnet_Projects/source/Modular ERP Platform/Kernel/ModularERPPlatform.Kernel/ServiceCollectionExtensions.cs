using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// Registers the kernel with a dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds the module kernel and its collaborators.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration of <see cref="KernelOptions"/>.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddModuleKernel(
        this IServiceCollection services,
        Action<KernelOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<KernelOptions>(static _ => { });
        }

        services.AddSingleton<ManifestReader>();
        services.AddSingleton<PluginDiscovery>();
        services.AddSingleton<DependencyResolver>();
        services.AddSingleton<ModuleConfigurationStore>();
        services.AddSingleton<ModuleRegistry>();
        services.AddSingleton<LifecycleManager>();
        services.AddSingleton<PluginDirectoryWatcher>();
        services.AddSingleton<ModuleHost>();

        return services;
    }
}
