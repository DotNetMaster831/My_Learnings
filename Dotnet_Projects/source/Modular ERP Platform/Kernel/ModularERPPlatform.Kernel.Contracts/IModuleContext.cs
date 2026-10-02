using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ModularERPPlatform.Kernel.Contracts;

/// <summary>
/// Everything a module may reach of the host. Deliberately small: a module can touch
/// nothing that is not exposed here, which is what keeps ADR-001 honest.
/// </summary>
public interface IModuleContext
{
    /// <summary>Gets the module this context belongs to.</summary>
    public ModuleId ModuleId { get; }

    /// <summary>Gets the kernel version currently hosting the module.</summary>
    public Version KernelVersion { get; }

    /// <summary>Gets services available to the module, scoped to its lifetime.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Gets configuration scoped to this module alone.</summary>
    public IModuleConfiguration Configuration { get; }

    /// <summary>Gets a logger already tagged with this module's identity.</summary>
    public ILogger Logger { get; }
}
