using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// A collectible <see cref="AssemblyLoadContext"/> holding exactly one module and its
/// private dependencies. Discarding this context is what makes FR-004 possible (ADR-002).
/// </summary>
/// <remarks>
/// Assemblies shared with the host are deliberately <em>not</em> loaded here. If
/// <c>ModularERP.Kernel.Contracts</c> were loaded into this context, the module's
/// <c>IModule</c> would be a different type from the host's <c>IModule</c> and no cast
/// would ever succeed. Returning <see langword="null"/> from <see cref="Load"/> defers to
/// the default context, giving one shared type identity.
/// </remarks>
public sealed class ModuleLoadContext : AssemblyLoadContext
{
    private static readonly string[] SharedAssemblyPrefixes =
    [
        "ModularERP.Kernel.Contracts",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "System.",
        "Microsoft.CSharp",
        "netstandard",
        "mscorlib",
    ];

    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>Initialises a load context for one module.</summary>
    /// <param name="name">A name identifying this context in diagnostics.</param>
    /// <param name="modulePath">Full path to the module assembly.</param>
    public ModuleLoadContext(string name, string modulePath)
        : base(name, isCollectible: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);

        ModulePath = modulePath;
        _resolver = new AssemblyDependencyResolver(modulePath);
    }

    /// <summary>Gets the path of the module assembly this context was built for.</summary>
    public string ModulePath { get; }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);

        if (IsShared(assemblyName))
        {
            return null;
        }

        string? path = _resolver.ResolveAssemblyToPath(assemblyName);

        return path is null ? null : LoadFromAssemblyPath(path);
    }

    /// <inheritdoc />
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        return path is null
            ? IntPtr.Zero
            : LoadUnmanagedDllFromPath(path);
    }

    private static bool IsShared(AssemblyName assemblyName)
    {
        string? name = assemblyName.Name;

        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (string prefix in SharedAssemblyPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
