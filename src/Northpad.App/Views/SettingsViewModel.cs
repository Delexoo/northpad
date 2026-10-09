using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northpad.App.Services;
using Northpad.App.Shell;
using Northpad.App.Themes;
using Northpad.Core.Storage;
using Northpad.Core.Vault;

namespace Northpad.App.Views;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IVaultService _vault;
    private readonly IThemeService _themeService;
    private readonly AppPaths _paths;
    private readonly DialogService _dialogs;
    private readonly INavigationService _navigation;

    public SettingsViewModel(
        IVaultService vault,
        IThemeService themeService,
        AppPaths paths,
        DialogService dialogs,
        INavigationService navigation)
    {
        _vault = vault;
        _themeService = themeService;
        _paths = paths;
        _dialogs = dialogs;
        _navigation = navigation;
        VersionText = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        RuntimeText = RuntimeInformation.FrameworkDescription;
    }

    public string VersionText { get; }

    public string RuntimeText { get; }

    [ObservableProperty]
    private string _theme = "system";

    [ObservableProperty]
    private bool _passphraseEnabled;

    [ObservableProperty]
    private string _protectionSummary = string.Empty;

    [ObservableProperty]
    private string _storagePath = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    public void Refresh()
    {
        Theme = _themeService.Preference switch
        {
            ThemePreference.Light => "light",
            ThemePreference.Dark => "dark",
            _ => "system",
        };
        PassphraseEnabled = _vault.Protection == VaultProtection.Passphrase;
        StoragePath = _paths.Root;
        ProtectionSummary = PassphraseEnabled
            ? "A passphrase wraps the vault key. While Northpad is unlocked, that key is in memory. The recovery key is the only other way to unwrap it."
            : "The vault key is protected by this Windows account. Another Windows user cannot unwrap it. A program running as you can. A passphrase adds a lock screen.";
        Status = string.Empty;
    }

    [RelayCommand]
    private void ChooseTheme(string? theme)
    {
        var preference = theme switch
        {
            "light" => ThemePreference.Light,
            "dark" => ThemePreference.Dark,
            _ => ThemePreference.System,
        };
        _themeService.Set(preference);
        Theme = theme ?? "system";
    }

    [RelayCommand]
    private void SetPassphrase()
    {
        var passphrase = _dialogs.AskForNewPassphrase();
        if (passphrase is null)
        {
            return;
        }

        try
        {
            var recovery = _vault.EnablePassphrase(passphrase);
            _dialogs.ShowRecoveryKey(recovery);
            Refresh();
            Status = "Passphrase protection is on.";
        }
        catch (Exception exception)
        {
            Status = Safe(exception);
        }
    }

    [RelayCommand]
    private void ChangePassphrase()
    {
        var prompt = _dialogs.AskToChangePassphrase();
        if (prompt is null)
        {
            return;
        }

        try
        {
            _vault.ChangePassphrase(prompt.Value.Current, prompt.Value.Next);
            Refresh();
            Status = "Passphrase changed. The previous recovery key still works.";
        }
        catch (Exception exception)
        {
            Status = Safe(exception);
        }
    }

    [RelayCommand]
    private void RemovePassphrase()
    {
        var current = _dialogs.AskForCurrentPassphrase(
            "Remove passphrase",
            "The vault key will again be protected only by this Windows account.");
        if (current is null)
        {
            return;
        }

        try
        {
            _vault.DisablePassphrase(current);
            Refresh();
            Status = "Passphrase removed.";
        }
        catch (Exception exception)
        {
            Status = Safe(exception);
        }
    }

    [RelayCommand]
    private void RotateRecoveryKey()
    {
        var current = _dialogs.AskForCurrentPassphrase(
            "New recovery key",
            "The previous recovery key will stop working.");
        if (current is null)
        {
            return;
        }

        try
        {
            var recovery = _vault.RotateRecoveryKey(current);
            _dialogs.ShowRecoveryKey(recovery);
            Status = "A new recovery key was created.";
        }
        catch (Exception exception)
        {
            Status = Safe(exception);
        }
    }

    [RelayCommand]
    private void Lock() => _navigation.LockWorkspace();

    [RelayCommand]
    private void OpenFolder()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{_paths.Root}\"",
            UseShellExecute = true,
        });
    }

    [RelayCommand]
    private void ExportBackup()
    {
        var folder = _dialogs.PickFolder("Choose a folder for the backup");
        if (folder is null)
        {
            return;
        }

        var destination = Path.Combine(folder, "NorthpadBackup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        try
        {
            _vault.ExportBackup(destination);
            _dialogs.ShowMessage("Backup saved", "The backup is in " + destination + ". It contains the database and the wrapped key, not an unwrapped copy of your notes.");
        }
        catch (Exception exception)
        {
            Status = Safe(exception);
        }
    }

    [RelayCommand]
    private void DeleteWorkspace()
    {
        if (!_dialogs.ConfirmChecked(
                "Delete local data",
                "This permanently deletes notes, tasks, and the vault key on this computer. Backups you already exported are not deleted.",
                "Delete the Northpad data on this computer",
                "Delete"))
        {
            return;
        }

        try
        {
            _vault.DeleteAllData();
            _navigation.CompleteUnlock();
        }
        catch (Exception exception)
        {
            Status = Safe(exception);
        }
    }

    private static string Safe(Exception exception) => exception switch
    {
        VaultStateException or VaultUnlockFailedException or VaultLockedException or StorageException => exception.Message,
        _ => "The setting could not be changed.",
    };
}
