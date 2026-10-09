using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northpad.App.Shell;
using Northpad.Core.Models;
using Northpad.Core.Modules;

namespace Northpad.App.Views;

public partial class HomeViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public HomeViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    public IReadOnlyList<ModuleDescriptor> Modules { get; } = KnownModules.All;

    [RelayCommand]
    private void OpenModule(string? moduleId)
    {
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            _navigation.OpenModule(moduleId);
        }
    }
}
