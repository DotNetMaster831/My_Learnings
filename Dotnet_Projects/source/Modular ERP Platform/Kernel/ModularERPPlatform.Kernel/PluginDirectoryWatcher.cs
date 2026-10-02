using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// Watches the plugin directory and raises an event once file activity settles,
/// so a module copied in at runtime is noticed without a restart. Satisfies FR-003.
/// </summary>
public sealed class PluginDirectoryWatcher : IDisposable
{
    private static readonly Action<ILogger, string, Exception?> _logWatching =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(PluginDirectoryWatcher)),
            "Watching {PluginDirectory} for module changes.");

    private static readonly Action<ILogger, Exception?> _logWatcherFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(2, nameof(PluginDirectoryWatcher)),
            "Plugin directory watcher failed.");

    private static readonly Action<ILogger, Exception?> _logHandlerThrew =
        LoggerMessage.Define(LogLevel.Error, new EventId(3, nameof(PluginDirectoryWatcher)),
            "A plugin directory change handler threw.");

    private readonly KernelOptions _options;
    private readonly ILogger<PluginDirectoryWatcher> _logger;
    private readonly Lock _sync = new();

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private bool _disposed;

    /// <summary>Initialises the watcher.</summary>
    /// <param name="options">Kernel options.</param>
    /// <param name="logger">Logger.</param>
    public PluginDirectoryWatcher(
        IOptions<KernelOptions> options,
        ILogger<PluginDirectoryWatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Raised after file activity in the plugin directory has settled.</summary>
    public event EventHandler? DirectoryChanged;

    /// <summary>Begins watching the plugin directory.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_watcher is not null)
        {
            return;
        }

        Directory.CreateDirectory(_options.PluginDirectory);

        _watcher = new FileSystemWatcher(_options.PluginDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size,
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnChanged;
        _watcher.Error += OnError;
        _watcher.EnableRaisingEvents = true;

        _logWatching(_logger, _options.PluginDirectory, null);
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => ScheduleNotification();

    private void OnError(object sender, ErrorEventArgs e) => _logWatcherFailed(_logger, e.GetException());

    private void ScheduleNotification()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _debounceTimer ??= new Timer(_ => Raise(), null, Timeout.Infinite, Timeout.Infinite);
            _debounceTimer.Change(_options.WatchDebounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void Raise()
    {
        try
        {
            DirectoryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { _logHandlerThrew(_logger, ex); }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnChanged;
            _watcher.Changed -= OnChanged;
            _watcher.Deleted -= OnChanged;
            _watcher.Renamed -= OnChanged;
            _watcher.Error -= OnError;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
        DirectoryChanged = null;
    }
}
