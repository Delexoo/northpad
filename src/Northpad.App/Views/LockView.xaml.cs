using System.Windows.Controls;
using System.Windows.Input;

namespace Northpad.App.Views;

public partial class LockView : UserControl
{
    private readonly LockViewModel _viewModel;

    public LockView(LockViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnUnlock(object sender, System.Windows.RoutedEventArgs e) => await SubmitAsync();

    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SubmitAsync();
        }
    }

    private async Task SubmitAsync()
    {
        var secret = _viewModel.UseRecoveryKey ? RecoveryBox.Text : SecretBox.Password;
        SecretBox.Clear();
        RecoveryBox.Clear();
        await _viewModel.UnlockAsync(secret);
    }

    private void OnToggleMode(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.UseRecoveryKey = !_viewModel.UseRecoveryKey;
        var recovery = _viewModel.UseRecoveryKey;
        SecretBox.Visibility = recovery ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        RecoveryBox.Visibility = recovery ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        SecretLabel.Text = recovery ? "Recovery key" : "Passphrase";
        ModeButton.Content = recovery ? "Use a passphrase" : "Use a recovery key";
        _viewModel.Error = string.Empty;
    }
}
