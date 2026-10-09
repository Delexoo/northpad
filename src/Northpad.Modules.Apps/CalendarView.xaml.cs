using System.Windows.Controls;
using Northpad.Core.Storage;

namespace Northpad.Modules.Apps;

public partial class CalendarView : UserControl
{
    public CalendarView()
    {
        InitializeComponent();
    }

    private void OnEventSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CalendarViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is EventRow row)
        {
            viewModel.Edit(row.Document);
        }
    }
}
