using System;
using System.Collections.Generic;
using System.Text;

namespace ModularERPPlatform.Kernel.Contracts;

/// <summary>Coarse health verdict for a module.</summary>
public enum HealthStatus
{
    /// <summary>The module is operating normally.</summary>
    Healthy = 0,

    /// <summary>The module works but something is wrong.</summary>
    Degraded = 1,

    /// <summary>The module cannot serve requests.</summary>
    Unhealthy = 2,
}
/// <summary>
/// The outcome of a health check. Polled by the kernel from Phase 1.1 onward.
/// </summary>
/// <param name="Status">The verdict.</param>
/// <param name="Description">A short human-readable explanation.</param>
/// <param name="Error">The exception behind an unhealthy verdict, when there is one.</param>
public sealed record HealthResult(HealthStatus Status,string Description,Exception? Error = null)
{
    /// <summary>Creates a healthy result.</summary>
    /// <param name="description">Optional explanation.</param>
    /// <returns>A healthy result.</returns>
    public static HealthResult Healthy(string description = "Healthy") => new(HealthStatus.Healthy, description);

    /// <summary>Creates a degraded result.</summary>
    /// <param name="description">What is wrong.</param>
    /// <returns>A degraded result.</returns>
    public static HealthResult Degraded(string description) => new(HealthStatus.Degraded, description);

    /// <summary>Creates an unhealthy result.</summary>
    /// <param name="description">What failed.</param>
    /// <param name="error">The exception behind the failure, when there is one.</param>
    /// <returns>An unhealthy result.</returns>
    public static HealthResult Unhealthy(string description, Exception? error = null) => new(HealthStatus.Unhealthy, description, error);
}
