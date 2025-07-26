using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esri.ArcGISRuntime.Portal;

using Map = Esri.ArcGISRuntime.Mapping.Map;

namespace Plotany;

public partial class QuarantinesTabViewModel : ObservableObject
{
    [ObservableProperty]
    private Map? _quarantineMap;

    public QuarantinesTabViewModel()
    {
        _ = SetUpMap();
    }

    private async Task SetUpMap()
    {
        ArcGISPortal portal = await ArcGISPortal.CreateAsync();
        PortalItem mapItem = await PortalItem.CreateAsync(portal, "0d086c72f8bb49429fc8be8b42fc5b92");

        QuarantineMap = new Map(mapItem);
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
