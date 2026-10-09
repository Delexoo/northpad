using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Northpad.App.Shell;
using Northpad.Core.Vault;

namespace Northpad.App.Views;

public partial class LockViewModel : ObservableObject
{
    private readonly IVaultService _vault;
    private readonly INavigationService _navigation;
    private readonly ILogger<LockViewModel> _logger;

    public LockViewModel(IVaultService vault, INavigationService navigation, ILogger<LockViewModel> logger)
    {
        _vault = vault;
        _navigation = navigation;
        _logger = logger;
    }

    [ObservableProperty]
    private string _error = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _useRecoveryKey;

    public async Task UnlockAsync(string secret)
    {
        if (IsBusy)
        {
            return;
        }

        Error = string.Empty;
        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                if (UseRecoveryKey)
                {
                    _vault.UnlockWithRecoveryKey(secret);
                }
                else
                {
                    _vault.UnlockWithPassphrase(secret);
                }
            });
            _navigation.CompleteUnlock();
        }
        catch (VaultUnlockFailedException exception)
        {
            Error = exception.Message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unlock failed.");
            Error = "The workspace could not be unlocked.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
