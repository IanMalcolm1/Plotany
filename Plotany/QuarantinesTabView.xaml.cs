using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Mapping.Popups;
using System.Diagnostics;

namespace Plotany;

public partial class QuarantinesTabView : ContentPage
{
    private QuarantinesTabViewModel _viewModel;
    public QuarantinesTabView(GardenManager gardenManager)
    {
        InitializeComponent();

        _viewModel = new QuarantinesTabViewModel(gardenManager);
        this.BindingContext = _viewModel;
    }

    private async void mapView_GeoViewTapped(object? sender, GeoViewInputEventArgs e)
    {
        Exception? error = null;
        try
        {
            var result = await QuarantineMapView.IdentifyLayersAsync(e.Position, 3, false);

            // Retrieves or builds Popup from IdentifyLayerResult
            var popup = GetPopup(result);

            if (popup != null)
            {
                popupViewer.Popup = popup;
                popupPanel.IsVisible = true;
            }
            else
            {
                popupPanel.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            error = ex;

        }
        if (error != null)
            await DisplayAlert(error.GetType().Name, error.Message, "OK");
    }

    private Popup? GetPopup(IdentifyLayerResult result)
    {
        if (result == null)
        {
            return null;
        }

        var popup = result.Popups.FirstOrDefault();
        if (popup != null)
        {
            return popup;
        }

        var geoElement = result.GeoElements.FirstOrDefault();
        if (geoElement != null)
        {
            if (result.LayerContent is IPopupSource)
            {
                var popupDefinition = ((IPopupSource)result.LayerContent).PopupDefinition;
                if (popupDefinition != null)
                {
                    return new Popup(geoElement, popupDefinition);
                }
            }

            return Popup.FromGeoElement(geoElement);
        }

        return null;
    }

    private Popup? GetPopup(IEnumerable<IdentifyLayerResult> results)
    {
        if (results == null)
        {
            return null;
        }
        foreach (var result in results)
        {
            var popup = GetPopup(result);
            if (popup != null)
            {
                return popup;
            }

            foreach (var subResult in result.SublayerResults)
            {
                popup = GetPopup(subResult);
                if (popup != null)
                {
                    return popup;
                }
            }
        }

        return null;
    }

    private void CloseButton_Click(object sender, EventArgs e)
    {
        popupPanel.IsVisible = false;
    }

    private void popupViewer_PopupAttachmentClicked(object sender, Esri.ArcGISRuntime.Toolkit.Maui.PopupAttachmentClickedEventArgs e)
    {
        e.Handled = true; // Prevent default launch action
                          // Share file:
                          // _ = Share.Default.RequestAsync(new ShareFileRequest(new ReadOnlyFile(e.Attachment.Filename!, e.Attachment.ContentType)));

        // Open default file handler
        _ = Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(
             new Microsoft.Maui.ApplicationModel.OpenFileRequest(e.Attachment.Name, new ReadOnlyFile(e.Attachment.Filename!, e.Attachment.ContentType)));
    }

    private void popupViewer_HyperlinkClicked(object sender, Esri.ArcGISRuntime.Toolkit.Maui.HyperlinkClickedEventArgs e)
    {
        // Include below line if you want to prevent the default action
        // e.Handled = true;

        // Perform custom action when a link is clicked
        Debug.WriteLine(e.Uri);
    }
}