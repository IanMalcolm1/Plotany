using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Plotany;

/// <summary>
/// Provides map data to an application
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    private GardenManager _gardenManager;

    [ObservableProperty] private bool _settingName = true;
    [ObservableProperty] private string _gardenName = String.Empty;

    public MainPageViewModel(GardenManager gardenManager)
    {
        _gardenManager = gardenManager;
    }

    [RelayCommand]
    private async Task SetName()
    {
        _gardenManager.GardenName = GardenName;
        SettingName = false;
    }
    [RelayCommand]
    private async Task NavigateMakeGarden()
    {
        await Shell.Current.GoToAsync("///ViewGarden");
    }
}
