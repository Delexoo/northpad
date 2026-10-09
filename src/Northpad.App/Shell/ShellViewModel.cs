using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Northpad.Core;
using Northpad.Core.Vault;
using Northpad.Modules.Apps;

namespace Northpad.App.Shell;

public partial class ShellViewModel : ObservableObject, INavigationService
{
    private readonly IServiceProvider _services;
    private readonly IVaultService _vault;
    private readonly ReminderMonitor _reminders;
    private readonly ILogger<ShellViewModel> _logger;
    private object? _current;

    public ShellViewModel(IServiceProvider services, IVaultService vault, ReminderMonitor reminders, ILogger<ShellViewModel> logger)
    {
        _services = services;
        _vault = vault;
        _reminders = reminders;
        _logger = logger;
    }

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _activeDestination = "home";

    [ObservableProperty]
    private bool _isUnlocked;

    public void Start()
    {
        ModuleRegistry.EnsureMatchesCatalog();
        try
        {
            _vault.EnsureCreated();
            _services.GetRequiredService<Themes.IThemeService>().Initialize();
            if (_vault.Protection == VaultProtection.WindowsAccount)
            {
                _vault.UnlockWithWindowsAccount();
                IsUnlocked = true;
                GoHome();
            }
            else
            {
                IsUnlocked = false;
                ShowLock();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError("Startup failed. exceptionType={ExceptionType} detail={Detail}", exception.GetType().Name, exception.Message);
            ShowMessage(UserMessage(exception));
        }
    }

    public void FlushCurrent()
    {
        if (_current is not IFlushable flushable)
        {
            return;
        }

        try
        {
            flushable.Flush();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Saving the open workspace failed.");
        }
    }

    [RelayCommand(CanExecute = nameof(IsUnlocked))]
    public void GoHome()
    {
        if (!IsUnlocked)
        {
            return;
        }

        Show(_services.GetRequiredService<Views.HomeView>());
        ActiveDestination = "home";
    }

    [RelayCommand(CanExecute = nameof(IsUnlocked))]
    public void OpenSearch()
    {
        if (!IsUnlocked)
        {
            return;
        }

        Show(_services.GetRequiredService<Views.SearchView>());
        ActiveDestination = "search";
    }

    [RelayCommand]
    public void OpenSettings()
    {
        Show(_services.GetRequiredService<Views.SettingsView>());
        ActiveDestination = "settings";
    }

    public void OpenModule(string moduleId, Guid? entityId = null)
    {
        if (!IsUnlocked)
        {
            return;
        }

        Show(ModuleRegistry.Create(_services, moduleId, entityId));
        ActiveDestination = string.Empty;
    }

    public void LockWorkspace()
    {
        if (_vault.Protection != VaultProtection.Passphrase)
        {
            return;
        }

        ReleaseCurrent();
        _reminders.Stop();
        _vault.Lock();
        IsUnlocked = false;
        ShowLock();
    }

    public void CompleteUnlock()
    {
        IsUnlocked = _vault.IsUnlocked;
        if (IsUnlocked)
        {
            _reminders.Start();
            GoHome();
        }
    }

    partial void OnIsUnlockedChanged(bool value)
    {
        GoHomeCommand.NotifyCanExecuteChanged();
        OpenSearchCommand.NotifyCanExecuteChanged();
    }

    private void ShowLock()
    {
        Show(_services.GetRequiredService<Views.LockView>());
        ActiveDestination = string.Empty;
    }

    private void Show(FrameworkElement view)
    {
        ReleaseCurrent();
        _current = view.DataContext;
        CurrentView = view;
    }

    private void ReleaseCurrent()
    {
        FlushCurrent();
        if (_current is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _current = null;
    }

    private void ShowMessage(string message)
    {
        CurrentView = new TextBlock
        {
            Text = message,
            Margin = new Thickness(32),
            FontSize = 16,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    private static string UserMessage(Exception exception) => exception switch
    {
        VaultStateException or VaultUnlockFailedException or StorageException => exception.Message,
        _ => "Northpad could not open the local workspace.",
    };
}
