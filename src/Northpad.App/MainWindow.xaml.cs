using System.Windows;
using Northpad.App.Shell;

namespace Northpad.App;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _shell;

    public MainWindow(ShellViewModel shell)
    {
        _shell = shell;
        InitializeComponent();
        DataContext = shell;
        Closing += (_, _) => _shell.FlushCurrent();
    }
}
