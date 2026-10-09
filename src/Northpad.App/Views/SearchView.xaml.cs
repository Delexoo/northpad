using System.Windows.Controls;

namespace Northpad.App.Views;

public partial class SearchView : UserControl
{
    public SearchView(SearchViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SearchViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is SearchRow row)
        {
            Dispatcher.BeginInvoke(() => viewModel.OpenCommand.Execute(row));
        }
    }
}
