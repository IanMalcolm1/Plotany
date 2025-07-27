using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;

using Map = Esri.ArcGISRuntime.Mapping.Map;

namespace Plotany;

public partial class QuarantinesTabViewModel : ObservableObject
{
    [ObservableProperty]
    private Map? _quarantineMap;

    private readonly GardenManager _gardenManager;

    public QuarantinesTabViewModel(GardenManager gardenManager)
    {
        _ = SetUpMap();
        _gardenManager = gardenManager;
    }

    private async Task SetUpMap()
    {
        ArcGISPortal portal = await ArcGISPortal.CreateAsync();
        PortalItem mapItem = await PortalItem.CreateAsync(portal, "0d086c72f8bb49429fc8be8b42fc5b92");

        QuarantineMap = new Map(mapItem);
        await QuarantineMap.LoadAsync();

        if (_gardenManager.GardenName != null)
        {
            var gardenLayer = QuarantineMap.OperationalLayers.FirstOrDefault((a) => a.Name == "My Garden") as FeatureLayer;
            gardenLayer.DefinitionExpression = $"Name='{_gardenManager.GardenName}'";
        }
        else
        {
            _gardenManager.GardenNameChanged += (sender, args) =>
            {
                var gardenLayer = QuarantineMap.OperationalLayers.FirstOrDefault((a) => a.Name == "My Garden") as FeatureLayer;
                gardenLayer.DefinitionExpression = $"Name='{_gardenManager.GardenName}'";
            };
        }
    }

    [RelayCommand]
    private async Task OpenSurvey()
    {
        try
        {
            Uri uri = new Uri("https://arcg.is/PTrXW0");
            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            // An unexpected error occurred. No browser may be installed on the device.
        }
    }
}
