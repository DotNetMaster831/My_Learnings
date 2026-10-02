using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ModularERPPlatform.Kernel.Contracts;

/// <summary>
/// A module's identity card, read from disk before any of its code executes.
/// Satisfies PC-002 and FR-002.
/// </summary>
public interface IModuleManifest
{
    /// <summary>Gets the module's stable identity.</summary>
    public ModuleId Id { get; }

    /// <summary>Gets the module's version.</summary>
    public Version Version { get; }

    /// <summary>Gets the name shown to users.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the file name of the assembly implementing <see cref="IModule"/>.</summary>
    public string AssemblyFile { get; }

    /// <summary>Gets the kernel versions this module can run against. Satisfies FR-007.</summary>
    public VersionRange RequiredKernelVersion { get; }

    /// <summary>Gets the other modules this module needs. Satisfies FR-005.</summary>
    public IReadOnlyList<ModuleDependency> Dependencies { get; }

    /// <summary>Gets the capabilities this module declares it requires.</summary>
    public IReadOnlyList<Permission> RequiredPermissions { get; }
}
