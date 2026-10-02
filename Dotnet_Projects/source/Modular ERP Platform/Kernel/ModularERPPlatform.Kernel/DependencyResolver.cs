using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// Orders modules so every dependency loads before its dependents, and refuses
/// anything it cannot order. Satisfies FR-005, FR-006 and FR-007.
/// </summary>
public sealed class DependencyResolver
{
    private static readonly Action<ILogger, string, string, Exception?> _logModuleBlocked =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1, nameof(DependencyResolver)),
            "Module {ModuleId} is blocked. {Reason}");

    private static readonly Action<ILogger, int, int, Exception?> _logResolvedSummary =
        LoggerMessage.Define<int, int>(LogLevel.Information, new EventId(2, nameof(DependencyResolver)),
            "Resolved {LoadableCount} loadable module(s); {BlockedCount} blocked.");

    private readonly KernelOptions _options;
    private readonly ILogger<DependencyResolver> _logger;

    /// <summary>Initialises the resolver.</summary>
    /// <param name="options">Kernel options, which carry the kernel version.</param>
    /// <param name="logger">Logger.</param>
    public DependencyResolver(IOptions<KernelOptions> options, ILogger<DependencyResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Resolves a load order for the supplied modules.</summary>
    /// <param name="modules">Modules discovered on disk.</param>
    /// <returns>An ordered load list plus every module that was blocked, with reasons.</returns>
    public ResolutionResult Resolve(IReadOnlyCollection<DiscoveredModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        Dictionary<ModuleId, DiscoveredModule> byId = new();
        Dictionary<ModuleId, string> blocked = new();

        foreach (DiscoveredModule module in modules)
        {
            if (byId.TryGetValue(module.Manifest.Id, out DiscoveredModule? existing))
            {
                blocked[module.Manifest.Id] =
                    $"Module '{module.Manifest.Id}' is declared twice: " +
                    $"'{existing.DirectoryPath}' and '{module.DirectoryPath}'.";
                continue;
            }

            byId[module.Manifest.Id] = module;
        }

        foreach ((ModuleId id, DiscoveredModule module) in byId)
        {
            if (blocked.ContainsKey(id))
            {
                continue;
            }

            if (!module.Manifest.RequiredKernelVersion.Satisfies(_options.KernelVersion))
            {
                blocked[id] =
                    $"Module '{id}' requires kernel {module.Manifest.RequiredKernelVersion} " +
                    $"but the running kernel is {_options.KernelVersion}.";
            }
        }

        foreach ((ModuleId id, DiscoveredModule module) in byId)
        {
            if (blocked.ContainsKey(id))
            {
                continue;
            }

            foreach (ModuleDependency dependency in module.Manifest.Dependencies)
            {
                if (!byId.TryGetValue(dependency.Id, out DiscoveredModule? target))
                {
                    blocked[id] =
                        $"Module '{id}' depends on '{dependency.Id}' {dependency.VersionRange}, " +
                        "which was not found.";
                    break;
                }

                if (!dependency.VersionRange.Satisfies(target.Manifest.Version))
                {
                    blocked[id] =
                        $"Module '{id}' requires '{dependency.Id}' {dependency.VersionRange} " +
                        $"but the available version is {target.Manifest.Version}.";
                    break;
                }
            }
        }

        DetectCycles(byId, blocked);
        PropagateBlocking(byId, blocked);

        List<DiscoveredModule> ordered = TopologicalSort(byId, blocked);

        foreach ((ModuleId id, string reason) in blocked)
        {
            _logModuleBlocked(_logger, id.Value, reason, null);
        }

        _logResolvedSummary(_logger, ordered.Count, blocked.Count, null);

        return new ResolutionResult(ordered, blocked);
    }

    private static void DetectCycles(
        Dictionary<ModuleId, DiscoveredModule> byId,
        Dictionary<ModuleId, string> blocked)
    {
        HashSet<ModuleId> settled = new();
        List<ModuleId> path = new();
        HashSet<ModuleId> onPath = new();

        foreach (ModuleId id in byId.Keys)
        {
            Visit(id);
        }

        void Visit(ModuleId id)
        {
            if (settled.Contains(id) || blocked.ContainsKey(id))
            {
                return;
            }

            if (onPath.Contains(id))
            {
                int start = path.IndexOf(id);
                IEnumerable<ModuleId> cycle = path.Skip(start).Append(id);
                string description = string.Join(" -> ", cycle);

                foreach (ModuleId member in path.Skip(start))
                {
                    blocked[member] = $"Circular dependency: {description}.";
                }

                return;
            }

            if (!byId.TryGetValue(id, out DiscoveredModule? module))
            {
                return;
            }

            onPath.Add(id);
            path.Add(id);

            foreach (ModuleDependency dependency in module.Manifest.Dependencies)
            {
                Visit(dependency.Id);
            }

            path.RemoveAt(path.Count - 1);
            onPath.Remove(id);
            settled.Add(id);
        }
    }

    private static void PropagateBlocking(
        Dictionary<ModuleId, DiscoveredModule> byId,
        Dictionary<ModuleId, string> blocked)
    {
        bool changed = true;

        while (changed)
        {
            changed = false;

            foreach ((ModuleId id, DiscoveredModule module) in byId)
            {
                if (blocked.ContainsKey(id))
                {
                    continue;
                }

                foreach (ModuleDependency dependency in module.Manifest.Dependencies)
                {
                    if (blocked.ContainsKey(dependency.Id))
                    {
                        blocked[id] =
                            $"Module '{id}' cannot load because its dependency '{dependency.Id}' is blocked.";
                        changed = true;
                        break;
                    }
                }
            }
        }
    }

    private static List<DiscoveredModule> TopologicalSort(
        Dictionary<ModuleId, DiscoveredModule> byId,
        Dictionary<ModuleId, string> blocked)
    {
        List<DiscoveredModule> ordered = new();
        HashSet<ModuleId> emitted = new();

        foreach (ModuleId id in byId.Keys.OrderBy(static i => i.Value, StringComparer.Ordinal))
        {
            Emit(id);
        }

        return ordered;

        void Emit(ModuleId id)
        {
            if (emitted.Contains(id) || blocked.ContainsKey(id))
            {
                return;
            }

            if (!byId.TryGetValue(id, out DiscoveredModule? module))
            {
                return;
            }

            emitted.Add(id);

            foreach (ModuleDependency dependency in module.Manifest.Dependencies)
            {
                Emit(dependency.Id);
            }

            ordered.Add(module);
        }
    }
}

/// <summary>
/// The outcome of dependency resolution.
/// </summary>
/// <param name="LoadOrder">Modules in an order that satisfies every dependency.</param>
/// <param name="Blocked">Modules that will not load, keyed by id, with the reason.</param>
public sealed record ResolutionResult(
    IReadOnlyList<DiscoveredModule> LoadOrder,
    IReadOnlyDictionary<ModuleId, string> Blocked);
