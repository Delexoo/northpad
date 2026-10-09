using System.Windows.Controls;

namespace Northpad.Modules.Notes;

public partial class NotesView : UserControl
{
    public NotesView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is NotesViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is NoteListItem item)
        {
            viewModel.Select(item);
        }
    }
}
