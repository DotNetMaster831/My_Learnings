using System;
using System.Collections.Generic;
using System.Text;

namespace ModularERPPlatform.Kernel.Contracts;

/// <summary>
/// Optional contract a module implements to report its own health. Satisfies PC-006.
/// The kernel begins polling this in Phase 1.1.
/// </summary>
public interface IHealthCheck
{
    /// <summary>Reports current health.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The health verdict.</returns>
    public Task<HealthResult> CheckAsync(CancellationToken cancellationToken = default);
}
