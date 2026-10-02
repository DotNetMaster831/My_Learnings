using System;
using System.Collections.Generic;
using System.Text;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// Concrete manifest produced by <see cref="ManifestReader"/>.
/// </summary>
public sealed class ModuleManifest : IModuleManifest
{
    /// <summary>Initialises a new manifest.</summary>
    /// <param name="id">Module identity.</param>
    /// <param name="version">Module version.</param>
    /// <param name="displayName">Name shown to users.</param>
    /// <param name="assemblyFile">File name of the module assembly.</param>
    /// <param name="requiredKernelVersion">Acceptable kernel versions.</param>
    /// <param name="dependencies">Declared module dependencies.</param>
    /// <param name="requiredPermissions">Declared permissions.</param>
    public ModuleManifest(ModuleId id, Version version, string displayName, string assemblyFile, VersionRange requiredKernelVersion, IReadOnlyList<ModuleDependency> dependencies, IReadOnlyList<Permission> requiredPermissions)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyFile);
        ArgumentNullException.ThrowIfNull(requiredKernelVersion);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(requiredPermissions);

        Id = id;
        Version = version;
        DisplayName = displayName;
        AssemblyFile = assemblyFile;
        RequiredKernelVersion = requiredKernelVersion;
        Dependencies = dependencies;
        RequiredPermissions = requiredPermissions;
    }

    /// <inheritdoc />
    public ModuleId Id { get; }

    /// <inheritdoc />
    public Version Version { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string AssemblyFile { get; }

    /// <inheritdoc />
    public VersionRange RequiredKernelVersion { get; }

    /// <inheritdoc />
    public IReadOnlyList<ModuleDependency> Dependencies { get; }

    /// <inheritdoc />
    public IReadOnlyList<Permission> RequiredPermissions { get; }
}
