using System;
using System.Collections.Generic;
using System.Text;

namespace ModularERPPlatform.Kernel.Contracts;

/// <summary>
/// Stable identity of a module, independent of its version or file location.
/// Values are normalised to lower-case invariant, so comparison is case-insensitive.
/// </summary>
public readonly record struct ModuleId
{
    private readonly string? _value;

    private ModuleId(string value) => _value = value;

    /// <summary>Gets the normalised identifier text.</summary>
    /// <exception cref="InvalidOperationException">The identifier was never initialised.</exception>
    public string Value => _value ?? throw new InvalidOperationException("ModuleId was not initialised. Use ModuleId.Create or ModuleId.TryCreate.");

    /// <summary>Gets a value indicating whether this identifier holds no value.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(_value);

    /// <summary>Creates a module identifier from raw text.</summary>
    /// <param name="value">Identifier text. Letters, digits, dot, dash and underscore only.</param>
    /// <returns>The normalised identifier.</returns>
    /// <exception cref="ArgumentException">The text is empty or contains illegal characters.</exception>
    public static ModuleId Create(string value)
    {
        if (!TryCreate(value, out ModuleId id, out string? error))
        {
            throw new ArgumentException(error, nameof(value));
        }

        return id;
    }

    /// <summary>Attempts to create a module identifier from raw text.</summary>
    /// <param name="value">Identifier text.</param>
    /// <param name="id">The normalised identifier when parsing succeeds.</param>
    /// <returns><see langword="true"/> when the text is a legal identifier.</returns>
    public static bool TryCreate(string? value, out ModuleId id) => TryCreate(value, out id, out _);

    /// <summary>Attempts to create a module identifier, reporting why it failed.</summary>
    /// <param name="value">Identifier text.</param>
    /// <param name="id">The normalised identifier when parsing succeeds.</param>
    /// <param name="error">A human-readable reason when parsing fails.</param>
    /// <returns><see langword="true"/> when the text is a legal identifier.</returns>
    public static bool TryCreate(string? value, out ModuleId id, out string? error)
    {
        id = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Module id must not be empty.";
            return false;
        }

        string trimmed = value.Trim();

        foreach (char c in trimmed)
        {
            bool legal = char.IsLetterOrDigit(c) || c is '.' or '-' or '_';
            if (!legal)
            {
                error = $"Module id '{trimmed}' contains illegal character '{c}'. Allowed: letters, digits, '.', '-', '_'.";
                return false;
            }
        }

        id = new ModuleId(trimmed.ToLowerInvariant());
        error = null;
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => _value ?? "<uninitialised>";
}
