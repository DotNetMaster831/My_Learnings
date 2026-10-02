using System;
using System.Collections.Generic;
using System.Text;

namespace ModularERPPlatform.Kernel.Contracts;


/// <summary>
/// A named capability a module declares it requires. The kernel enforces these
/// deny-by-default from Phase 1.4 onward (ADR-005). Phase 1.0 records them only.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Permission is the correct domain term")]
public readonly record struct Permission
{
    private readonly string? _name;

    private Permission(string name) => _name = name;

    /// <summary>Gets the permission name, for example <c>Finance.Post</c>.</summary>
    /// <exception cref="InvalidOperationException">The permission was never initialised.</exception>
    public string Name => _name
        ?? throw new InvalidOperationException("Permission was not initialised. Use Permission.Create.");

    /// <summary>Creates a permission from raw text.</summary>
    /// <param name="name">The permission name.</param>
    /// <returns>The permission.</returns>
    /// <exception cref="ArgumentException">The name is empty or whitespace.</exception>
    public static Permission Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Permission name must not be empty.", nameof(name));
        }

        return new Permission(name.Trim());
    }

    /// <inheritdoc />
    public override string ToString() => _name ?? "<uninitialised>";
}
