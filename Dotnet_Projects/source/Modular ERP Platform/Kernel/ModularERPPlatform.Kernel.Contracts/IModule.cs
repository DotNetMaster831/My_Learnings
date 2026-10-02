using System;
using System.Collections.Generic;
using System.Text;

namespace ModularERPPlatform.Kernel.Contracts;

/// <summary>
/// The contract every plugin implements. Satisfies PC-001.
/// </summary>
/// <remarks>
/// Implementations must not capture the <see cref="IModuleContext"/> in any static field,
/// and must not subscribe to any static event on a host type. Either one keeps the module's
/// <see cref="System.Runtime.Loader.AssemblyLoadContext"/> alive after unload and defeats
/// FR-004. This is risk R1.
/// </remarks>
public interface IModule
{
    /// <summary>Gets the module's stable identity. Must match its manifest.</summary>
    public ModuleId Id { get; }

    /// <summary>Gets the module's version. Must match its manifest.</summary>
    public Version Version { get; }

    /// <summary>Accepts the host context and prepares the module. Called once.</summary>
    /// <param name="context">The module's view of the host.</param>
    /// <param name="cancellationToken">Cancels initialisation.</param>
    /// <returns>A task that completes when the module is ready to activate.</returns>
    public Task InitializeAsync(IModuleContext context, CancellationToken cancellationToken);

    /// <summary>Starts the module's work. May be called again after a deactivation.</summary>
    /// <param name="cancellationToken">Cancels activation.</param>
    /// <returns>A task that completes when the module is running.</returns>
    public Task ActivateAsync(CancellationToken cancellationToken);

    /// <summary>Stops the module's work while leaving it loaded.</summary>
    /// <param name="cancellationToken">Cancels deactivation.</param>
    /// <returns>A task that completes when the module is idle.</returns>
    public Task DeactivateAsync(CancellationToken cancellationToken);

    /// <summary>Releases every resource the module holds, ahead of unload.</summary>
    /// <param name="cancellationToken">Cancels shutdown.</param>
    /// <returns>A task that completes when the module holds nothing.</returns>
    public Task ShutdownAsync(CancellationToken cancellationToken);
}
