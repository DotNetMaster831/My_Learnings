using System;
using System.Collections.Generic;
using System.Text;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// Everything the kernel knows about one module at runtime.
/// </summary>
public sealed class ModuleRegistration
{
    private readonly object _sync = new();
    private ModuleState _state;
    private string? _lastError;

    internal ModuleRegistration(ModuleManifest manifest, string directoryPath, string assemblyPath)
    {
        Manifest = manifest;
        DirectoryPath = directoryPath;
        AssemblyPath = assemblyPath;
        _state = ModuleState.Discovered;
    }

    /// <summary>Gets the module's manifest.</summary>
    public ModuleManifest Manifest { get; }

    /// <summary>Gets the module's directory on disk.</summary>
    public string DirectoryPath { get; }

    /// <summary>Gets the full path of the module assembly.</summary>
    public string AssemblyPath { get; }

    /// <summary>Gets the module's identity.</summary>
    public ModuleId Id => Manifest.Id;

    /// <summary>Gets the module's current lifecycle state.</summary>
    public ModuleState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    /// <summary>Gets the most recent error message, when the module has faulted or been blocked.</summary>
    public string? LastError
    {
        get
        {
            lock (_sync)
            {
                return _lastError;
            }
        }
    }

    /// <summary>Gets the time of the most recent state transition.</summary>
    public DateTimeOffset LastTransitionUtc { get; private set; } = DateTimeOffset.UtcNow;

    internal IModule? Instance { get; set; }

    internal ModuleLoadContext? LoadContext { get; set; }

    internal ModuleContext? Context { get; set; }

    internal void SetState(ModuleState state, string? error = null)
    {
        lock (_sync)
        {
            _state = state;
            _lastError = error;
            LastTransitionUtc = DateTimeOffset.UtcNow;
        }
    }
}
