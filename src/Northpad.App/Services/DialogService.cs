using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Northpad.Core;

namespace Northpad.App.Services;

public sealed class DialogService : IUserConfirmation, IFilePicker
{
    public bool Confirm(string title, string message) =>
        ConfirmChecked(title, message, "Delete this item from this computer", "Delete");

    public bool ConfirmChecked(string title, string message, string checkLabel, string confirmText)
    {
        var accepted = false;
        var check = new CheckBox
        {
            Content = checkLabel,
            Margin = new Thickness(0, 16, 0, 0),
        };
        var confirm = new Button
        {
            Content = confirmText,
            Style = FindStyle("DangerButton"),
            IsEnabled = false,
            MinWidth = 96,
            Margin = new Thickness(8, 0, 0, 0),
        };
        check.Checked += (_, _) => confirm.IsEnabled = true;
        check.Unchecked += (_, _) => confirm.IsEnabled = false;
        var window = CreateWindow(title, Body(message, check, CancelButton(null), confirm));
        confirm.Click += (_, _) =>
        {
            accepted = true;
            window.DialogResult = true;
        };
        window.ShowDialog();
        return accepted;
    }

    public void ShowMessage(string title, string message)
    {
        var close = new Button
        {
            Content = "Close",
            Style = FindStyle("PrimaryButton"),
            IsDefault = true,
            MinWidth = 96,
        };
        var window = CreateWindow(title, Body(message, null, null, close));
        close.Click += (_, _) => window.Close();
        window.ShowDialog();
    }

    public string? AskForNewPassphrase()
    {
        var first = Password();
        var second = Password();
        var error = ErrorText();
        string? result = null;
        var window = CreateWindow("Set a passphrase", Form(
            "The passphrase wraps the vault key on this computer. Northpad does not keep a copy of it.",
            Labeled("New passphrase", first),
            Labeled("Confirm passphrase", second),
            error));
        AddActions(window, "Continue", () =>
        {
            if (!string.Equals(first.Password, second.Password, StringComparison.Ordinal))
            {
                error.Text = "The passphrases do not match.";
                return false;
            }

            if (first.Password.Length < 8)
            {
                error.Text = "Use a passphrase of at least 8 characters.";
                return false;
            }

            result = first.Password;
            return true;
        });
        window.ShowDialog();
        first.Clear();
        second.Clear();
        return result;
    }

    public (string Current, string Next)? AskToChangePassphrase()
    {
        var current = Password();
        var next = Password();
        var confirm = Password();
        var error = ErrorText();
        (string Current, string Next)? result = null;
        var window = CreateWindow("Change passphrase", Form(
            "The notes and tasks stay encrypted with the same data key. Only the passphrase wrap changes.",
            Labeled("Current passphrase", current),
            Labeled("New passphrase", next),
            Labeled("Confirm new passphrase", confirm),
            error));
        AddActions(window, "Change", () =>
        {
            if (!string.Equals(next.Password, confirm.Password, StringComparison.Ordinal))
            {
                error.Text = "The new passphrases do not match.";
                return false;
            }

            if (next.Password.Length < 8)
            {
                error.Text = "Use a passphrase of at least 8 characters.";
                return false;
            }

            result = (current.Password, next.Password);
            return true;
        });
        window.ShowDialog();
        current.Clear();
        next.Clear();
        confirm.Clear();
        return result;
    }

    public string? AskForCurrentPassphrase(string title, string message)
    {
        var current = Password();
        var error = ErrorText();
        string? result = null;
        var window = CreateWindow(title, Form(message, Labeled("Passphrase", current), error));
        AddActions(window, "Continue", () =>
        {
            if (current.Password.Length == 0)
            {
                error.Text = "Enter the current passphrase.";
                return false;
            }

            result = current.Password;
            return true;
        });
        window.ShowDialog();
        var copy = result;
        current.Clear();
        return copy;
    }

    public void ShowRecoveryKey(string recoveryKey)
    {
        var box = new TextBox
        {
            Text = recoveryKey,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Style = FindStyle("InputText"),
            Margin = new Thickness(0, 12, 0, 0),
        };
        var check = new CheckBox
        {
            Content = "I have saved this recovery key outside Northpad",
            Margin = new Thickness(0, 16, 0, 0),
        };
        var button = new Button
        {
            Content = "Continue",
            Style = FindStyle("PrimaryButton"),
            IsEnabled = false,
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
        };
        check.Checked += (_, _) => button.IsEnabled = true;
        check.Unchecked += (_, _) => button.IsEnabled = false;
        var acknowledged = false;
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "Save this recovery key now. Northpad cannot unlock a passphrase-protected workspace without the passphrase or this key. It is not shown again.",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(box);
        panel.Children.Add(check);
        panel.Children.Add(button);
        var window = CreateWindow("Save the recovery key", panel);
        button.Click += (_, _) =>
        {
            acknowledged = true;
            window.DialogResult = true;
        };
        window.Closing += (_, args) => args.Cancel = !acknowledged;
        window.ShowDialog();
    }

    public void Notify(string title, string message) => ShowMessage(title, message);

    public string? PickOpen(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSave(string title, string suggestedFileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = suggestedFileName,
            Filter = filter,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private static void AddActions(Window window, string confirmText, Func<bool> accept)
    {
        if (window.Content is not StackPanel panel)
        {
            return;
        }

        var confirm = new Button
        {
            Content = confirmText,
            Style = FindStyle("PrimaryButton"),
            MinWidth = 110,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = true,
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
        };
        var cancel = CancelButton(window);
        confirm.Click += (_, _) =>
        {
            if (accept())
            {
                window.DialogResult = true;
            }
        };
        row.Children.Add(cancel);
        row.Children.Add(confirm);
        panel.Children.Add(row);
    }

    private static Button CancelButton(Window? window)
    {
        var cancel = new Button
        {
            Content = "Cancel",
            Style = FindStyle("QuietButton"),
            MinWidth = 96,
            IsCancel = true,
        };
        if (window is not null)
        {
            cancel.Click += (_, _) => window.Close();
        }

        return cancel;
    }

    private static UIElement Body(string message, CheckBox? check, Button? cancel, Button confirm)
    {
        var panel = new DockPanel { LastChildFill = false };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
        };
        DockPanel.SetDock(row, Dock.Bottom);
        if (cancel is not null)
        {
            row.Children.Add(cancel);
        }

        row.Children.Add(confirm);
        panel.Children.Add(row);
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        if (check is not null)
        {
            stack.Children.Add(check);
        }

        panel.Children.Add(stack);
        return panel;
    }

    private static StackPanel Form(string message, params UIElement[] fields)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        foreach (var field in fields)
        {
            panel.Children.Add(field);
        }

        return panel;
    }

    private static UIElement Labeled(string label, PasswordBox box)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(box);
        return panel;
    }

    private static PasswordBox Password() => new()
    {
        Style = FindStyle("InputPassword"),
    };

    private static TextBlock ErrorText() => new()
    {
        Foreground = FindBrush("Brush.Danger"),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 10, 0, 0),
    };

    private static Window CreateWindow(string title, UIElement content)
    {
        if (content is FrameworkElement element)
        {
            element.Margin = new Thickness(24);
        }

        var window = new Window
        {
            Title = title,
            Content = content,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Background = FindBrush("Brush.Window"),
            Foreground = FindBrush("Brush.Text"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 14,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        if (Application.Current?.MainWindow is { IsLoaded: true } owner)
        {
            window.Owner = owner;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return window;
    }

    private static Style FindStyle(string key) => (Style)Application.Current.FindResource(key);

    private static System.Windows.Media.Brush FindBrush(string key) => (System.Windows.Media.Brush)Application.Current.FindResource(key);
}
