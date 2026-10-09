using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Northpad.App.Services;
using Northpad.App.Shell;
using Northpad.App.Themes;
using Northpad.App.Views;
using Northpad.Core;
using Northpad.Core.Cryptography;
using Northpad.Core.Logging;
using Northpad.Core.Storage;
using Northpad.Core.Vault;
using Northpad.Modules.Notes;
using Northpad.Modules.Todo;

namespace Northpad.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private Mutex? _instance;
    private ILogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _instance = new Mutex(false, @"Local\Northpad.SingleInstance");
        try
        {
            if (!_instance.WaitOne(0))
            {
                MessageBox.Show("Northpad is already running.", "northpad");
                Shutdown();
                return;
            }
        }
        catch (AbandonedMutexException)
        {
        }

        var ready = Stopwatch.StartNew();
        var paths = AppPaths.ForDefaultLocation();
        paths.EnsureDirectories();

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new SafeFileLoggerProvider(paths.LogPath));
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddSingleton(paths);
        services.AddSingleton(sp => new SqliteDatabase(sp.GetRequiredService<AppPaths>().DatabasePath));
        services.AddSingleton<VaultService>();
        services.AddSingleton<IVaultService>(sp => sp.GetRequiredService<VaultService>());
        services.AddSingleton<IContentProtector>(sp => sp.GetRequiredService<VaultService>().Protector);
        services.AddSingleton<INoteRepository, NoteRepository>();
        services.AddSingleton<ITaskRepository, TaskRepository>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<IUserConfirmation>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<IThemeService, ThemeManager>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<ShellViewModel>());
        services.AddTransient<HomeViewModel>();
        services.AddTransient<HomeView>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<SearchView>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsView>();
        services.AddTransient<LockViewModel>();
        services.AddTransient<LockView>();
        services.AddTransient<NotesViewModel>();
        services.AddTransient<NotesView>();
        services.AddTransient<TodoViewModel>();
        services.AddTransient<TodoView>();
        services.AddTransient<MainWindow>();

        _services = services.BuildServiceProvider();
        _logger = _services.GetRequiredService<ILogger<App>>();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        var shell = _services.GetRequiredService<ShellViewModel>();
        shell.Start();
        window.Show();
        _logger.LogInformation("Application ready in {ElapsedMs} ms.", ready.Elapsed.TotalMilliseconds.ToString("F0"));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_services is not null)
        {
            _services.GetService<ShellViewModel>()?.FlushCurrent();
            _services.GetService<VaultService>()?.Dispose();
            _services.GetService<SqliteDatabase>()?.Dispose();
            _services.GetService<IThemeService>()?.Dispose();
            _services.Dispose();
        }

        if (_instance is not null)
        {
            try
            {
                _instance.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _instance.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled interface error.");
        MessageBox.Show(
            "Northpad hit an unexpected error. A local log entry was written without note or task text.",
            "northpad");
        e.Handled = true;
    }
}
