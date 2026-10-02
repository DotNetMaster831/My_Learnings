using System;
using System.Collections.Generic;
using System.Text;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// A module found on disk: its manifest and where it lives. Nothing has been loaded yet.
/// </summary>
/// <param name="Manifest">The validated manifest.</param>
/// <param name="DirectoryPath">The module's own directory.</param>
/// <param name="AssemblyPath">Full path to the module assembly.</param>
public sealed record DiscoveredModule(
    ModuleManifest Manifest,
    string DirectoryPath,
    string AssemblyPath);
