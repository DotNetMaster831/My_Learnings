using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularERPPlatform.Kernel;

namespace ModularERPPlatform.Shell.Wpf;

/// <summary>
/// Composition root. Builds the container, starts the kernel, shows the shell.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private ModuleHost? _moduleHost;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        ServiceCollection services = new();

        services.AddLogging(static builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddModuleKernel(static options =>
        {
            options.PluginDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins");
            options.ConfigurationDirectory = Path.Combine(AppContext.BaseDirectory, "ModuleConfig");
            options.KernelVersion = new Version(1, 0, 0);
            options.WatchPluginDirectory = true;
        });

        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<ShellWindow>();

        _services = services.BuildServiceProvider();

        _moduleHost = _services.GetRequiredService<ModuleHost>();

        ShellWindow window = _services.GetRequiredService<ShellWindow>();
        window.Show();

        // Kernel startup is awaited, never blocked on. NFR-006.
        _ = StartKernelAsync();

        _services.GetRequiredService<ShellViewModel>().Refresh();
    }

    private static readonly Action<ILogger, Exception?> _logKernelStartupFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(2, nameof(App)), "Kernel startup failed.");

    private static readonly Action<ILogger, Exception?> _logKernelShutdownFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(3, nameof(App)), "Exception during kernel shutdown.");

    private async Task StartKernelAsync()
    {
        try
        {
            if (_moduleHost is not null)
            {
                await _moduleHost.StartAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            ILogger<App>? logger = _services?.GetService<ILogger<App>>();
            if (logger is not null)
            {
                _logKernelStartupFailed(logger, ex);
            }

            MessageBox.Show(ex.Message, "ModularERP Shell", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _ = ShutdownKernelAsync();

        base.OnExit(e);
    }

    private async Task ShutdownKernelAsync()
    {
        try
        {
            if (_moduleHost is not null)
            {
                await _moduleHost.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            ILogger<App>? logger = _services?.GetService<ILogger<App>>();
            if (logger is not null)
            {
                _logKernelShutdownFailed(logger, ex);
            }
        }

        if (_services is not null)
        {
            await _services.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static readonly Action<ILogger, Exception?> _logUnhandled =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, nameof(App)), "Unhandled exception on the dispatcher thread.");

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        ILogger<App>? logger = _services?.GetService<ILogger<App>>();
        if (logger is not null)
        {
            _logUnhandled(logger, e.Exception);
        }

        MessageBox.Show(
            e.Exception.Message,
            "ModularERP Shell",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}

