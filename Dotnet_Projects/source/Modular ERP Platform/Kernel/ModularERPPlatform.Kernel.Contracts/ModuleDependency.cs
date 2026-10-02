using System;
using System.Collections.Generic;
using System.Text;

namespace ModularERPPlatform.Kernel.Contracts;

/// <summary>
/// A declared dependency of one module on another, with an acceptable version range.
/// </summary>
/// <param name="Id">The module depended upon.</param>
/// <param name="VersionRange">The versions of that module which are acceptable.</param>
public sealed record ModuleDependency(ModuleId Id, VersionRange VersionRange)
{
    /// <inheritdoc />
    public override string ToString() => $"{Id} {VersionRange}";
}
