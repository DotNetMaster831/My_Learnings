using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// The in-memory source of truth for which modules exist and what state each is in.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly ConcurrentDictionary<ModuleId, ModuleRegistration> _registrations = new();

    /// <summary>Gets every registration currently known.</summary>
    /// <returns>A snapshot of the registrations.</returns>
    public IReadOnlyCollection<ModuleRegistration> GetAll() => _registrations.Values.ToArray();

    /// <summary>Finds one registration.</summary>
    /// <param name="moduleId">The module to find.</param>
    /// <param name="registration">The registration when present.</param>
    /// <returns><see langword="true"/> when the module is registered.</returns>
    public bool TryGet(ModuleId moduleId, out ModuleRegistration? registration)
        => _registrations.TryGetValue(moduleId, out registration);

    /// <summary>Gets every registration currently in a given state.</summary>
    /// <param name="state">The state to filter by.</param>
    /// <returns>Matching registrations.</returns>
    public IReadOnlyCollection<ModuleRegistration> GetByState(ModuleState state)
        => _registrations.Values.Where(r => r.State == state).ToArray();

    internal ModuleRegistration GetOrAdd(DiscoveredModule module)
        => _registrations.GetOrAdd(
            module.Manifest.Id,
            static (_, m) => new ModuleRegistration(m.Manifest, m.DirectoryPath, m.AssemblyPath),
            module);

    internal bool Remove(ModuleId moduleId) => _registrations.TryRemove(moduleId, out _);

    internal bool Contains(ModuleId moduleId) => _registrations.ContainsKey(moduleId);
}
