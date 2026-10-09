using System.Windows.Controls;

namespace Northpad.Modules.Apps;

public partial class MailView : UserControl
{
    public MailView() => InitializeComponent();

    private void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MailViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is NamedRow row)
        {
            viewModel.Edit(row);
        }
    }
}

public partial class PasswordsView : UserControl
{
    public PasswordsView() => InitializeComponent();

    private void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PasswordsViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is NamedRow row)
        {
            viewModel.Edit(row);
        }
    }
}

public partial class WalletView : UserControl
{
    public WalletView() => InitializeComponent();

    private void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is WalletViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is NamedRow row)
        {
            viewModel.Edit(row);
        }
    }
}

public partial class SheetsView : UserControl
{
    public SheetsView() => InitializeComponent();

    private void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SheetsViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is NamedRow row)
        {
            viewModel.Edit(row);
        }
    }
}

public partial class TranslateView : UserControl
{
    public TranslateView() => InitializeComponent();
}

public partial class LibraryView : UserControl
{
    public LibraryView() => InitializeComponent();

    private void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is LibraryViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is Northpad.Core.Storage.StoredFile file)
        {
            viewModel.ChooseCommand.Execute(file);
        }
    }
}

public partial class MapsView : UserControl
{
    public MapsView() => InitializeComponent();
}

public partial class BrowserView : UserControl
{
    public BrowserView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Wire();
        Loaded += async (_, _) =>
        {
            Wire();
            if (DataContext is BrowserViewModel)
            {
                await Page.ShowHtml(WebPage.Start);
            }
        };
    }

    private void Wire()
    {
        if (DataContext is not BrowserViewModel viewModel)
        {
            return;
        }

        viewModel.NavigateRequested -= OnNavigate;
        viewModel.NavigateRequested += OnNavigate;
    }

    private async void OnNavigate(Uri uri) => await Page.Show(uri);
}

public partial class WebSearchView : UserControl
{
    public WebSearchView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Wire();
    }

    private void Wire()
    {
        if (DataContext is not WebSearchViewModel viewModel)
        {
            return;
        }

        viewModel.NavigateRequested -= OnNavigate;
        viewModel.NavigateRequested += OnNavigate;
    }

    private async void OnNavigate(Uri uri) => await Page.Show(uri);
}

public partial class VideoView : UserControl
{
    public VideoView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is VideoViewModel viewModel)
            {
                Page.UseAllowance(viewModel.AllowNavigation);
                viewModel.NavigateRequested -= OnNavigate;
                viewModel.NavigateRequested += OnNavigate;
            }
        };
    }

    private async void OnNavigate(Uri uri) => await Page.Show(uri);
}
