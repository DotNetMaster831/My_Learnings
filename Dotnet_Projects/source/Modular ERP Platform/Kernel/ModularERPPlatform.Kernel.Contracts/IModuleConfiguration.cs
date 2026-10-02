using System;
using System.Collections.Generic;
using System.Text;

namespace ModularERPPlatform.Kernel.Contracts;

/// <summary>
/// Configuration scoped to a single module. A module receives only its own instance
/// and has no way to name another module's store. Satisfies FR-010 and PC-004.
/// </summary>
public interface IModuleConfiguration
{
    /// <summary>Gets the module that owns this store.</summary>
    public ModuleId Owner { get; }

    /// <summary>Reads a setting.</summary>
    /// <typeparam name="T">The stored value's type.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored value, or <see langword="default"/> when the key is absent.</returns>
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a setting.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the value is durable.</returns>
    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>Removes a setting.</summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns><see langword="true"/> when a value was removed.</returns>
    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Lists the keys currently stored for this module.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored keys.</returns>
    public Task<IReadOnlyCollection<string>> GetKeysAsync(CancellationToken cancellationToken = default);
}
