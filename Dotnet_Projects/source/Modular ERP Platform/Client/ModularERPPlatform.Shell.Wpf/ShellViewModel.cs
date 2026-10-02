using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using ModularERPPlatform.Kernel;

namespace ModularERPPlatform.Shell.Wpf;

/// <summary>
/// Read-only view of the module registry. The catalogue proper arrives in Phase 1.1.
/// </summary>
public sealed class ShellViewModel : INotifyPropertyChanged
{
    private readonly ModuleRegistry _registry;
    private string _status = "Starting kernel…";

    /// <summary>Initialises the shell view model.</summary>
    /// <param name="registry">The module registry.</param>
    public ShellViewModel(ModuleRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the modules currently known to the kernel.</summary>
    public ObservableCollection<ModuleSummary> Modules { get; } = [];

    /// <summary>Gets the status line shown at the bottom of the shell.</summary>
    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Re-reads the registry and updates the bound collection.</summary>
    public void Refresh()
    {
        Modules.Clear();

        foreach (ModuleRegistration registration in _registry.GetAll())
        {
            Modules.Add(new ModuleSummary(
                registration.Manifest.DisplayName,
                registration.Id.Value,
                registration.Manifest.Version.ToString(),
                registration.State.ToString(),
                registration.LastError ?? string.Empty));
        }

        Status = Modules.Count == 0
            ? "Kernel running. No modules present."
            : $"Kernel running. {Modules.Count} module(s) registered.";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// One row in the shell's module list.
/// </summary>
/// <param name="DisplayName">The module's display name.</param>
/// <param name="Id">The module's identity.</param>
/// <param name="Version">The module's version.</param>
/// <param name="State">The module's current lifecycle state.</param>
/// <param name="LastError">The most recent error, or an empty string.</param>
public sealed record ModuleSummary(
    string DisplayName,
    string Id,
    string Version,
    string State,
    string LastError);
