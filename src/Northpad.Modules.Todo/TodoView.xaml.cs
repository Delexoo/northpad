using System.Windows.Controls;

namespace Northpad.Modules.Todo;

public partial class TodoView : UserControl
{
    public TodoView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is TodoViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is TaskListItem item)
        {
            viewModel.Select(item);
        }
    }
}
