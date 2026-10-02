using System;
using System.Collections.Generic;
using System.Text;

namespace ModularERPPlatform.Kernel.Contracts;

/// <summary>
/// Lifecycle states a module passes through. Satisfies FR-008.
/// </summary>
public enum ModuleState
{
    /// <summary>A manifest was found on disk. Nothing has been loaded.</summary>
    Discovered = 0,

    /// <summary>Dependencies or version constraints are unmet. The module will not load. Satisfies FR-005.</summary>
    Blocked = 1,

    /// <summary>The assembly is loaded into its own collectible context.</summary>
    Loaded = 2,

    /// <summary>The module accepted its context and completed initialisation.</summary>
    Initialized = 3,

    /// <summary>The module is running and may serve requests.</summary>
    Active = 4,

    /// <summary>The module was stopped but remains loaded and may be reactivated.</summary>
    Deactivated = 5,

    /// <summary>The module threw during a lifecycle transition. Reachable from any state. Satisfies FR-009.</summary>
    Faulted = 6,

    /// <summary>The module's load context was discarded. Terminal.</summary>
    Unloaded = 7,
}
