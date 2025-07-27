using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Mapping.Labeling;
using Esri.ArcGISRuntime.Mapping.Popups;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Editing;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Plotany
{
    public partial class ViewGarden : ContentPage, INotifyPropertyChanged
    {
        private GeometryEditor _geometryEditor;
        private Feature _selectedFeature;
        private GraphicsOverlay _gardenOverlay;
        private LocatorTask _locator;
        private Dictionary<GeometryType, Button> _geometryButtons;
        private Dictionary<string, GeometryEditorTool> _toolDictionary;
        private string _gardenNameInput = String.Empty;
        private bool _showGardenNameInput = false;
        private GardenManager _gardenManager;
        private ServiceFeatureTable _featureTable = new ServiceFeatureTable(new Uri("https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/GardenPlants/FeatureServer/0"));
        private FeatureLayer _gardenLayer;
        private FeatureLayer _plantLayer;
        private FeatureLayer _seedBagLayer;

        private BasemapStyle _currentBasemapStyle;
        public ICommand ItemTappedCommand { get; }

        public ViewGarden(GardenManager gardenManager)
        {
            InitializeComponent();
            InitializeAsync().GetAwaiter().GetResult();
            BindingContext = this;

            _gardenManager = gardenManager;
            if (_gardenManager.GardenName == null)
            {
                Shell.SetTabBarIsVisible(this, false);
            }
            _gardenManager.GardenNameChanged += async (s, e) =>
            {
                await LoadLayers();
                Shell.SetTabBarIsVisible(this, true);
            };
        }

        public string GardenNameInput
        {
            get => _gardenNameInput;
            set
            {
                if (_gardenNameInput != value)
                {
                    _gardenNameInput = value;
                    OnPropertyChanged(nameof(GardenNameInput));
                }
            }
        }

        public bool ShowGardenNameInput
        {
            get => _showGardenNameInput;
            set
            {
                if (_showGardenNameInput != value)
                {
                    _showGardenNameInput = value;
                    OnPropertyChanged(nameof(ShowGardenNameInput));
                }
            }
        }
        private async Task InitializeAsync()
        {
            Initialize();

            await Task.CompletedTask;
        }

        private async void Initialize()
        {
            try
            {
                // Initialize graphics overlay
                _gardenOverlay = new GraphicsOverlay();
                GardenMapView.GraphicsOverlays.Add(_gardenOverlay);

                // Initialize map with webmap
                string webmapId = "5319936041b145f083e58144986c91d5";
                var portal = await ArcGISPortal.CreateAsync(new Uri("https://www.arcgis.com"));
                var portalItem = await PortalItem.CreateAsync(portal, webmapId);
                var map = new Esri.ArcGISRuntime.Mapping.Map(portalItem);
                await map.LoadAsync();

                if (map.LoadStatus != Esri.ArcGISRuntime.LoadStatus.Loaded)
                {
                    //await DisplayAlert("Load Error", "Failed to load webmap. Check webmap ID or connection.", "OK");
                    return;
                }

                //_currentBasemapStyle = BasemapStyle.ArcGISImageryStandard;
                GardenMapView.Map = map;

                await Task.Delay(1000);

                _locator = new LocatorTask(new Uri("https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer"));

                _geometryEditor = new GeometryEditor();

                GardenMapView.GeometryEditor = _geometryEditor;

                _toolDictionary = new Dictionary<string, GeometryEditorTool>
                {
                    { "Vertex", new VertexTool() },
                    { "Freehand", new FreehandTool() },
                    { "ReticleVertex", new ReticleVertexTool() }
                };

                _geometryButtons = new Dictionary<GeometryType, Button>
                {
                    { GeometryType.Point, PointButton },
                    { GeometryType.Polygon, PolygonButton }
                };

                // Find garden and plant layers in the webmap
                _gardenLayer = map.OperationalLayers.FirstOrDefault(l => l.Name.Contains("Garden")) as FeatureLayer;
                _plantLayer = map.OperationalLayers.FirstOrDefault(l => l.Name.Contains("PlantedPlants")) as FeatureLayer;
                if (_gardenLayer == null || _plantLayer == null)
                {
                    // await DisplayAlert("Load Error", "Garden or Plant layer not found in webmap.", "OK");
                    return;
                }

                await LoadMap();
                if (_gardenManager.GardenName != null)
                {
                    await LoadLayers();
                }

                _seedBagLayer = new FeatureLayer(_featureTable);
            }
            catch (Exception ex)
            {
                // await DisplayAlert("Initialization Error", $"Failed to initialize: {ex.Message}", "OK");

                // Ensure _gardenOverlay is initialized even on failure
                if (_gardenOverlay == null)
                {
                    _gardenOverlay = new GraphicsOverlay();
                    GardenMapView.GraphicsOverlays.Add(_gardenOverlay);
                }
            }
        }

        //private async void OnSetupGardenClicked(object sender, EventArgs e)
        //{
        //    EmptyStatePanel.IsVisible = false;
        //    var granted = await RequestLocationPermission();
        //    if (!granted)
        //    {
        //        await DisplayAlert("Permission Required", "Location permission is required to set up your garden.", "OK");
        //        EmptyStatePanel.IsVisible = true;
        //        return;
        //    }
        //    await StartLocationTracking();
        //}

        private async Task<bool> RequestLocationPermission()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                return status == PermissionStatus.Granted;
            }
            catch
            {
                return false;
            }
        }

        private async void SaveButton_Click(object sender, EventArgs e)
        {
            string? gardenName = _gardenManager.GardenName;
            if (gardenName == null)
            {
                if (string.IsNullOrEmpty(GardenNameInput))
                {
                    ShowGardenNameInput = true;
                    return;
                }
                else
                {
                    gardenName = GardenNameInput;
                    ShowGardenNameInput = false;
                }
            }
            try
            {
                if (_geometryEditor == null || !_geometryEditor.IsStarted)
                {
                    //await DisplayAlert("Error", "No geometry is being edited.", "OK");
                    return;
                }

                var geometry = _geometryEditor.Stop();
                if (_currentBasemapStyle == BasemapStyle.ArcGISImagery || _currentBasemapStyle == BasemapStyle.ArcGISImageryStandard)
                {
                    GardenMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISLightGray);
                    _currentBasemapStyle = BasemapStyle.ArcGISLightGray;
                }
                if (geometry == null || geometry.IsEmpty)
                {
                    // await DisplayAlert("Error", "Invalid or empty geometry.", "OK");
                    return;
                }

                var featureTable = geometry.GeometryType == GeometryType.Point
                    ? _plantLayer?.FeatureTable as ServiceFeatureTable
                    : _gardenLayer?.FeatureTable as ServiceFeatureTable;

                if (featureTable == null)
                {
                    // await DisplayAlert("Error", "Feature table is not initialized.", "OK");
                    return;
                }

                var feature = featureTable.CreateFeature();
                feature.Geometry = geometry;

                if (geometry.GeometryType == GeometryType.Point)
                {
                    feature.Attributes["Name"] = gardenName;
                    feature.Attributes["PlantName"] = (PlantListView.SelectedItem as PlantItem)?.Name;
                }
                else
                {
                    feature.Attributes["Name"] = gardenName;
                }

                await featureTable.AddFeatureAsync(feature);
                await featureTable.ApplyEditsAsync();
                if (_gardenManager.GardenName == null)
                {
                    await _gardenManager.SetGardenName(gardenName); //this will trigger LoadLayers() elsewhere
                    await Task.Delay(2000);
                    await Shell.Current.GoToAsync("///PlantList");
                }
                else
                {
                    await LoadLayers();
                }
            }
            catch (Exception ex)
            {
                // await DisplayAlert("Save Error", $"Failed to save feature: {ex.Message}", "OK");
            }
        }

        private async Task LoadLayers()
        {
            try
            {
                if (_gardenLayer == null || _plantLayer == null)
                {
                    // await DisplayAlert("Load Error", "Garden or Plant layer not initialized.", "OK");
                    return;
                }

                await Task.WhenAll(_gardenLayer.LoadAsync(), _plantLayer.LoadAsync());
                if (_gardenLayer.LoadStatus != Esri.ArcGISRuntime.LoadStatus.Loaded ||
                    _plantLayer.LoadStatus != Esri.ArcGISRuntime.LoadStatus.Loaded)
                {
                    // await DisplayAlert("Load Error", "Failed to load layers. Check API key or URL.", "OK");
                    return;
                }

                var gardenTable = _gardenLayer.FeatureTable as ServiceFeatureTable;
                if (gardenTable == null)
                {
                    //await DisplayAlert("Load Error", "Garden layer feature table is null.", "OK");
                    return;
                }

                var gardenQuery = new QueryParameters
                {
                    WhereClause = $"Name = '{_gardenManager.GardenName}'",
                    ReturnGeometry = true,

                };
                var gardenResult = await gardenTable.QueryFeaturesAsync(gardenQuery);
                if (!gardenResult.Any())
                {
                    var allFeatures = await gardenTable.QueryFeaturesAsync(new QueryParameters());
                    var attributes = allFeatures.FirstOrDefault()?.Attributes.Keys;
                    var attributeList = attributes != null ? string.Join(", ", attributes) : "No attributes found";
                    //await DisplayAlert("Not Found", $"No garden named 'sam' found. Available attributes: {attributeList}", "OK");
                    return;
                }

                foreach (var gardenFeature in gardenResult)
                {

                    var gardenSymbol = _gardenLayer.Renderer?.GetSymbol(gardenFeature) ??
                        new SimpleFillSymbol(SimpleFillSymbolStyle.Solid, System.Drawing.Color.FromArgb(100, 0, 128, 0),
                            new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.DarkGreen, 2));
                    var gardenGraphic = new Graphic(gardenFeature.Geometry, gardenSymbol);
                    CopyAttributes(gardenGraphic, gardenFeature, "Name");
                    _gardenOverlay.Graphics.Add(gardenGraphic);
                }

                var plantTable = _plantLayer.FeatureTable as ServiceFeatureTable;
                if (plantTable == null)
                {
                    //await DisplayAlert("Load Error", "Plant layer feature table is null.", "OK");
                    return;
                }

                var plantQuery = new QueryParameters
                {
                    WhereClause = $"Name = '{_gardenManager.GardenName}'",
                    ReturnGeometry = true,

                };
                var plantResult = await plantTable.QueryFeaturesAsync(plantQuery);
                foreach (var plantFeature in plantResult)
                {
                    if (plantFeature.Geometry == null)
                    {
                        continue;
                    }
                    var plantSymbol = _plantLayer.Renderer?.GetSymbol(plantFeature) ??
                        new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Green, 10);
                    var plantGraphic = new Graphic(plantFeature.Geometry, plantSymbol);
                    CopyAttributes(plantGraphic, plantFeature, "Name", "PlantName");
                    _gardenOverlay.Graphics.Add(plantGraphic);
                }

                var firstGarden = gardenResult.FirstOrDefault();
                var extent = firstGarden?.Geometry?.Extent;
                if (extent == null)
                {
                    //await DisplayAlert("Load Error", "Garden feature extent is null.", "OK");
                    return;
                }
                var expandedExtent = new Envelope(
                    extent.XMin - extent.Width * 0.25,
                    extent.YMin - extent.Height * 0.25,
                    extent.XMax + extent.Width * 0.25,
                    extent.YMax + extent.Height * 0.25,
                    extent.SpatialReference);
                await GardenMapView.SetViewpointGeometryAsync(expandedExtent, 50);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Load Error", $"Failed to load from ArcGIS Online: {ex.Message}", "OK");
            }
        }

        private async Task LoadMap()
        {
            try
            {
                if (GardenMapView.Map == null)
                {
                    // await DisplayAlert("Load Error", "Map not initialized. Check webmap ID or connection.", "OK");
                    return;
                }
                await GardenMapView.Map.LoadAsync();

                if (_gardenOverlay == null)
                {
                    _gardenOverlay = new GraphicsOverlay();
                    GardenMapView.GraphicsOverlays.Add(_gardenOverlay);
                }

                _gardenOverlay.Graphics.Clear();
                _gardenOverlay.LabelDefinitions.Clear();
                GardenMapView.Map.OperationalLayers.Clear();

                if (_gardenLayer == null || _plantLayer == null)
                {
                    // await DisplayAlert("Load Error", "Garden or Plant layer not initialized.", "OK");
                    return;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Load Error", $"Failed to load map: {ex.Message}", "OK");
            }
        }

        private async Task SaveToArcGISOnlineAsync(Geometry geometry, string gardenName, string plantName)
        {
            try
            {
                string featureLayerUrl = geometry.GeometryType == GeometryType.Polygon
                    ? "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/GardenLayers/FeatureServer/0"
                    : "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/PlantedPlants/FeatureServer/0";
                var featureTable = new ServiceFeatureTable(new Uri(featureLayerUrl));
                await featureTable.LoadAsync();
                var feature = featureTable.CreateFeature();
                feature.Geometry = geometry;
                feature.Attributes["Name"] = gardenName;
                if (geometry.GeometryType == GeometryType.Point)
                {
                    feature.Attributes["PlantName"] = plantName;
                }
                await featureTable.AddFeatureAsync(feature);
                await featureTable.ApplyEditsAsync();
            }
            catch (Exception ex)
            {
                // await DisplayAlert("Save Error", $"Failed to save to ArcGIS Online: {ex.Message}", "OK");
            }
        }

        private async Task StartLocationTracking()
        {
            try
            {
                GardenMapView.LocationDisplay.AutoPanMode = LocationDisplayAutoPanMode.Off;
                var dataSource = GardenMapView.LocationDisplay.DataSource;
                await dataSource.StartAsync();
                GardenMapView.LocationDisplay.IsEnabled = true;
                var location = await WaitForLocationAsync(timeoutMillis: 10000);
                if (location != null)
                {
                    await GardenMapView.SetViewpointCenterAsync(location, 100);
                }
                else
                {
                    var fallbackPoint = new MapPoint(-117.1828359, 34.0383765, SpatialReferences.Wgs84);
                    await GardenMapView.SetViewpointCenterAsync(fallbackPoint, 100);
                    //await DisplayAlert("Location Fallback", "Using simulated location.", "OK");
                }
            }
            catch (Exception ex)
            {
                // await DisplayAlert("Location Error", $"Failed to start location tracking: {ex.Message}", "OK");
            }
            finally
            {
                await GardenMapView.LocationDisplay.DataSource?.StopAsync();
                GardenMapView.LocationDisplay.IsEnabled = false;
            }
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
        private Task<MapPoint?> WaitForLocationAsync(int timeoutMillis)
        {
            var tcs = new TaskCompletionSource<MapPoint?>();
            EventHandler<Esri.ArcGISRuntime.Location.Location> handler = null;
            handler = (s, location) =>
            {
                if (location?.Position != null)
                {
                    GardenMapView.LocationDisplay.LocationChanged -= handler;
                    tcs.TrySetResult(location.Position);
                }
            };
            GardenMapView.LocationDisplay.LocationChanged += handler;
            Task.Delay(timeoutMillis).ContinueWith(_ =>
            {
                GardenMapView.LocationDisplay.LocationChanged -= handler;
                tcs.TrySetResult(null);
            });
            return tcs.Task;
        }

        private async void ResetMapButton_Click(object sender, EventArgs e)
        {
            var confirm = await DisplayAlert("Confirm Reset", "This will clear your current garden map. Continue?", "Yes", "No");
            if (!confirm) return;
            try
            {
                var featureTable = _gardenLayer?.FeatureTable as ServiceFeatureTable;
                if (featureTable == null)
                {
                    await DisplayAlert("Error", "Garden layer not available.", "OK");
                    return;
                }
                await featureTable.LoadAsync();
                var query = new QueryParameters();
                var featureResult = await featureTable.QueryFeaturesAsync(query);
                var features = featureResult.ToList();
                if (features.Any())
                {
                    await featureTable.DeleteFeaturesAsync(features);
                    await featureTable.ApplyEditsAsync();
                }
                _gardenOverlay.Graphics.Clear();
                _gardenOverlay.LabelDefinitions.Clear();
                PointButton.IsEnabled = true;
                PolygonButton.IsEnabled = true;
                SaveButton.IsEnabled = true;
                DiscardButton.IsEnabled = true;
                await DisplayAlert("Success", "Map reset successfully!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to reset map: {ex.Message}", "OK");
            }
        }

        private async void PlantListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is PlantItem selectedPlant)
            {
                GardenMapView.IsVisible = true;
                if (!_geometryEditor.IsStarted)
                {
                    _selectedFeature = null;
                    _geometryEditor.Start(GeometryType.Point);
                }
                if (selectedPlant.Feature?.Geometry != null)
                {
                    await GardenMapView.SetViewpointGeometryAsync(selectedPlant.Feature.Geometry, 50);
                }
            }
            PlantListView.IsVisible = false;
        }

        private async void PointButton_Click(object sender, EventArgs e)
        {
            try
            {
                await _seedBagLayer.RetryLoadAsync();
                PlantListView.ItemsSource = null;
                var queryParams = new QueryParameters
                {
                    WhereClause = $"garden_name = '{_gardenManager.GardenName}'"
                };
                var queryResult = await _seedBagLayer.FeatureTable.QueryFeaturesAsync(queryParams);
                var plantItems = queryResult.Select(f => new PlantItem
                {
                    Name = f.Attributes["plant_name"]?.ToString(),
                    Feature = f
                }).ToList();
                PlantListView.ItemsSource = plantItems;
                PlantListView.IsVisible = true;
                if (plantItems.Count == 0)
                {
                    //await DisplayAlert("No Results", "No plants found for 'my1st'.", "OK");
                }
            }
            catch (Exception ex)
            {
                //await DisplayAlert("Error", $"Error querying plants: {ex.Message}", "OK");
            }
        }

        private void PolygonButton_Click(object sender, EventArgs e)
        {
            if (_geometryEditor == null)
            {
                return;
            }

            if (!_geometryEditor.IsStarted)
            {
                _selectedFeature = null;
                var geometryEditorStyle = new GeometryEditorStyle
                {
                    FillSymbol = new SimpleFillSymbol(SimpleFillSymbolStyle.Solid, System.Drawing.Color.Transparent, null),
                    VertexSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Red, 10),
                    SelectedVertexSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Green, 12)
                };
                var vertexTool = new VertexTool
                {
                    Style = geometryEditorStyle
                };
                _geometryEditor = new GeometryEditor
                {
                    Tool = vertexTool
                };
                GardenMapView.GeometryEditor = _geometryEditor;
                GardenMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISImageryStandard);
                _currentBasemapStyle = BasemapStyle.ArcGISImageryStandard;
                _geometryEditor.Start(GeometryType.Polygon);
            }
        }

        private void UndoButton_Click(object sender, EventArgs e)
        {
            _geometryEditor.Undo();
        }

        private void RedoButton_Click(object sender, EventArgs e)
        {
            _geometryEditor.Redo();
        }

        private void DeleteSelectedButton_Click(object sender, EventArgs e)
        {
            _geometryEditor.DeleteSelectedElement();
        }

        private void DiscardButton_Click(object sender, EventArgs e)
        {
            _geometryEditor.Stop();
            if (_currentBasemapStyle == BasemapStyle.ArcGISImagery || _currentBasemapStyle == BasemapStyle.ArcGISImageryStandard)
            {
                GardenMapView.Map.Basemap = new Basemap(BasemapStyle.ArcGISLightGray);
                _currentBasemapStyle = BasemapStyle.ArcGISLightGray;
            }

            ResetFromEditingSession();
        }

        private async void GardenMapView_GeoViewTapped(object sender, Esri.ArcGISRuntime.Maui.GeoViewInputEventArgs e)
        {
            if (_geometryEditor.IsStarted) return;
            PlantListView.IsVisible = false;
            try
            {
                if (_plantLayer == null || _plantLayer.FeatureTable == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", "Plant layer or table is not initialized.", "OK");
                    return;
                }

                if (_plantLayer.LoadStatus != Esri.ArcGISRuntime.LoadStatus.Loaded)
                {
                    await _plantLayer.LoadAsync();
                }

                var mapPoint = GardenMapView.ScreenToLocation(e.Position);

                // Define a small buffer to use as the spatial query envelope
                var tolerance = 5; // in pixels
                var mapTolerance = tolerance * GardenMapView.UnitsPerPixel;
                var envelope = new Envelope(
                    mapPoint.X - mapTolerance,
                    mapPoint.Y - mapTolerance,
                    mapPoint.X + mapTolerance,
                    mapPoint.Y + mapTolerance,
                    GardenMapView.Map.SpatialReference);

                var spatialQuery = new QueryParameters
                {
                    Geometry = envelope,
                    SpatialRelationship = SpatialRelationship.Intersects,
                    ReturnGeometry = true,
                    MaxFeatures = 1
                };

                // Execute query directly on the feature table
                var queryResult = await _plantLayer.FeatureTable.QueryFeaturesAsync(spatialQuery);

                var feature = queryResult.FirstOrDefault();
                if (feature == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Info", "No plant feature found at the tapped location.", "OK");
                    return;
                }

                _selectedFeature = feature;

                // Create popup manually from layer if PopupDefinition is set
                Popup popup = null;
                if (_plantLayer.PopupDefinition != null)
                {
                    popup = new Popup(feature, _plantLayer.PopupDefinition);
                }

                if (popup != null)
                {
                    popupViewer.Popup = popup;
                    popupPanel.IsVisible = true;
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Debug", "No popup configured for plant feature.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Error querying plant feature: {ex.Message}", "OK");
                ResetFromEditingSession();
            }
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
        private void ResetFromEditingSession()
        {
            _selectedFeature = null;
        }

        private Symbol GetSymbol(GeometryType geometryType)
        {
            return geometryType switch
            {
                GeometryType.Point => new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Red, 10),
                GeometryType.Multipoint => new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Red, 8),
                GeometryType.Polyline => new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Red, 3),
                GeometryType.Polygon => new SimpleFillSymbol(
                    SimpleFillSymbolStyle.Solid,
                    System.Drawing.Color.Transparent,
                    new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.DarkGreen, 2)),
                _ => null
            };
        }

        private void CopyAttributes(Graphic graphic, Feature feature, params string[] attributeNames)
        {
            foreach (var attr in attributeNames)
            {
                if (feature.Attributes.ContainsKey(attr) && feature.Attributes[attr] != null)
                {
                    graphic.Attributes[attr] = feature.Attributes[attr];
                }
            }
        }
    }

    public class PlantItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Feature Feature { get; set; }
    }

    // Converter for ListView visibility
    public class StringIsNotNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return !string.IsNullOrEmpty(value?.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}