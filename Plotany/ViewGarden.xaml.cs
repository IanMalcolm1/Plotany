using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Mapping.Labeling;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Editing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Plotany
{
    public partial class ViewGarden : ContentPage
    {
        private GeometryEditor _geometryEditor;
        private Graphic _selectedGraphic;
        private GraphicsOverlay _gardenOverlay;
        private LocatorTask _locator;
        private SimpleFillSymbol _polygonSymbol;
        private SimpleLineSymbol _polylineSymbol;
        private SimpleMarkerSymbol _pointSymbol, _multiPointSymbol;
        private Dictionary<GeometryType, Button> _geometryButtons;
        private Dictionary<string, GeometryEditorTool> _toolDictionary;

        public ViewGarden()
        {
            InitializeComponent();
            Initialize();
            LoadSavedDrawingsAsync();
        }

        private void Initialize()
        {
            GardenMapView.Map = new Esri.ArcGISRuntime.Mapping.Map(BasemapStyle.ArcGISImageryStandard);
            _gardenOverlay = new GraphicsOverlay();
            GardenMapView.GraphicsOverlays.Add(_gardenOverlay);
            _locator = new LocatorTask(new Uri("https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer"));

            _pointSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Green, 10);
            _multiPointSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Green, 8);
            _polylineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Green, 3);
            _polygonSymbol = new SimpleFillSymbol(
                SimpleFillSymbolStyle.Solid,
                System.Drawing.Color.FromArgb(100, 0, 128, 0),
                new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.DarkGreen, 2));

            _geometryEditor = new GeometryEditor();
            GardenMapView.GeometryEditor = _geometryEditor;

            _toolDictionary = new Dictionary<string, GeometryEditorTool>
            {
                { "Vertex", new VertexTool() },
                { "Freehand", new FreehandTool() },
                { "ReticleVertex", new ReticleVertexTool() }
            };

            ToolPicker.ItemsSource = _toolDictionary.Keys.ToList();
            ToolPicker.SelectedIndex = 0;

            _geometryButtons = new Dictionary<GeometryType, Button>
            {
                { GeometryType.Point, PointButton },
                { GeometryType.Multipoint, MultipointButton },
                { GeometryType.Polyline, PolylineButton },
                { GeometryType.Polygon, PolygonButton }
            };
        }

        private async void OnSetupGardenClicked(object sender, EventArgs e)
        {
            EmptyStatePanel.IsVisible = false;
            var granted = await RequestLocationPermission();
            if (!granted)
            {
                await DisplayAlert("Permission Required", "Location permission is required to set up your garden.", "OK");
                EmptyStatePanel.IsVisible = true;
                return;
            }

            await StartLocationTracking();
            await Task.Delay(1500);
        }

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
            try
            {
                var geometry = _geometryEditor.Stop();
                if (geometry == null)
                {
                    await DisplayAlert("Nothing to save", "No polygon drawn.", "OK");
                    return;
                }

                if (geometry.GeometryType != GeometryType.Polygon)
                {
                    await DisplayAlert("Error", "Only polygon geometries are supported.", "OK");
                    return;
                }

                string gardenName = await DisplayPromptAsync("Garden Name", "Enter a name for this garden:", "OK", "Cancel", "My Garden");
                if (string.IsNullOrEmpty(gardenName)) return;

                await SaveToArcGISOnlineAsync(geometry, gardenName);
                await LoadFromArcGISOnlineAsync(); // Refresh UI with latest ArcGIS Online data

                await DisplayAlert("Success", "Polygon saved successfully!", "OK");
                ResetFromEditingSession();
                DisableEditingTools();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save polygon: {ex.Message}", "OK");
            }
        }
        private async void LoadSavedDrawingsAsync()
        {
            await LoadFromArcGISOnlineAsync();
        }

        private async Task LoadFromArcGISOnlineAsync()
        {
            try
            {
                string featureLayerUrl = "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/GardenLayers/FeatureServer/0";

                var featureTable = new ServiceFeatureTable(new Uri(featureLayerUrl));
                await featureTable.LoadAsync();
                if (featureTable.LoadStatus != Esri.ArcGISRuntime.LoadStatus.Loaded)
                {
                    await DisplayAlert("Load Error", "Failed to load feature table. Check API key or URL.", "OK");
                    EmptyStatePanel.IsVisible = true;
                    return;
                }

                var query = new QueryParameters
                {
                    WhereClause = "Name IS NOT NULL", // Filter for features with a Name
                    ReturnGeometry = true,
                    MaxFeatures = 1000 // Limit to 1000 features; adjust or paginate if needed
                };

                var featureResult = await featureTable.QueryFeaturesAsync(query);
                if (featureResult == null)
                {
                    await DisplayAlert("Load Error", "No features returned from query.", "OK");
                    EmptyStatePanel.IsVisible = true;
                    return;
                }

                _gardenOverlay.Graphics.Clear();
                foreach (var feature in featureResult)
                {
                    try
                    {
                        var geometry = feature.Geometry;
                        if (geometry == null)
                        {
                            await DisplayAlert("Warning", $"Skipping feature with null geometry: {feature.Attributes["Name"]}", "OK");
                            continue;
                        }

                        var symbol = GetSymbol(geometry.GeometryType);
                        if (symbol != null)
                        {
                            var graphic = new Graphic(geometry, symbol);
                            graphic.Attributes["Name"] = feature.Attributes["Name"]?.ToString();
                            _gardenOverlay.Graphics.Add(graphic);
                        }
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Warning", $"Error loading feature: {ex.Message}", "OK");
                        continue;
                    }
                }

                var labelDefinition = new LabelDefinition(
                    new SimpleLabelExpression("[Name]"),
                    new TextSymbol
                    {
                        Color = System.Drawing.Color.White,
                        Size = 12,
                        HaloColor = System.Drawing.Color.Black,
                        HaloWidth = 2
                    })
                {
                    LabelOverlapStrategy = LabelOverlapStrategy.Automatic
                };
                _gardenOverlay.LabelDefinitions.Add(labelDefinition);
                _gardenOverlay.LabelsEnabled = true;

                EmptyStatePanel.IsVisible = !featureResult.Any();
                if (featureResult.Any())
                {
                    DisableEditingTools();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Load Error", $"Failed to load from ArcGIS Online: {ex.Message}", "OK");
                EmptyStatePanel.IsVisible = true;
            }
        }

        private async Task SaveToArcGISOnlineAsync(Geometry geometry, string gardenName)
        {
            string featureLayerUrl = "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/GardenLayers/FeatureServer/0"; // Added layer ID

            var featureTable = new ServiceFeatureTable(new Uri(featureLayerUrl));
            await featureTable.LoadAsync();

            var feature = featureTable.CreateFeature();
            feature.Geometry = geometry;
            feature.Attributes["Name"] = gardenName;

            await featureTable.AddFeatureAsync(feature);
            await featureTable.ApplyEditsAsync();
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
                    await DisplayAlert("Timeout", "Could not get your location.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Location Error", ex.Message, "OK");
            }
            finally
            {
                GardenMapView.LocationDisplay.DataSource?.StopAsync().GetAwaiter().GetResult();
                GardenMapView.LocationDisplay.IsEnabled = false;
            }
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
                string featureLayerUrl = "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/GardenLayers/FeatureServer/0";

                var featureTable = new ServiceFeatureTable(new Uri(featureLayerUrl));
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
                EmptyStatePanel.IsVisible = true;

                PointButton.IsEnabled = true;
                MultipointButton.IsEnabled = true;
                PolylineButton.IsEnabled = true;
                PolygonButton.IsEnabled = true;
                ToolPicker.IsEnabled = true;
                UniformScaleCheckBox.IsEnabled = true;
                SaveButton.IsEnabled = true;
                DiscardButton.IsEnabled = true;
                DeleteSelectedButton.IsEnabled = true;
                ToggleGeometryEditorPanelButton.IsEnabled = true;

                await DisplayAlert("Success", "Map reset successfully!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to reset map: {ex.Message}", "OK");
            }
        }

        private async void ShareMapButton_Click(object sender, EventArgs e)
        {
            await Launcher.OpenAsync("https://www.arcgis.com/home/webmap/viewer.html?webmap=<your-map-id>");
        }

        private void DisableEditingTools()
        {
            PointButton.IsEnabled = false;
            MultipointButton.IsEnabled = false;
            PolylineButton.IsEnabled = false;
            PolygonButton.IsEnabled = false;
            ToolPicker.IsEnabled = false;
            UniformScaleCheckBox.IsEnabled = false;
            SaveButton.IsEnabled = false;
            DiscardButton.IsEnabled = false;
            DeleteSelectedButton.IsEnabled = false;
            ToggleGeometryEditorPanelButton.IsEnabled = false;
        }

        private void PointButton_Click(object sender, EventArgs e)
        {
            if (!_geometryEditor.IsStarted)
            {
                DisableOtherGeometryButtons(PointButton);
                ToolPicker.IsEnabled = false;
                UniformScaleCheckBox.IsEnabled = false;
                _geometryEditor.Start(GeometryType.Point);
            }
        }

        private void MultipointButton_Click(object sender, EventArgs e)
        {
            if (!_geometryEditor.IsStarted)
            {
                DisableOtherGeometryButtons(MultipointButton);
                ToolPicker.IsEnabled = false;
                _geometryEditor.Start(GeometryType.Multipoint);
            }
        }

        private void PolylineButton_Click(object sender, EventArgs e)
        {
            if (!_geometryEditor.IsStarted)
            {
                DisableOtherGeometryButtons(PolylineButton);
                _geometryEditor.Start(GeometryType.Polyline);
            }
        }

        private void PolygonButton_Click(object sender, EventArgs e)
        {
            if (!_geometryEditor.IsStarted)
            {
                DisableOtherGeometryButtons(PolygonButton);
                _geometryEditor.Start(GeometryType.Polygon);
            }
        }

        private void ToolPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ToolPicker.SelectedItem == null) return;

            var tool = _toolDictionary[ToolPicker.SelectedItem.ToString()];
            _geometryEditor.Tool = tool;

            PointButton.IsEnabled = MultipointButton.IsEnabled =
                !_geometryEditor.IsStarted && (tool is VertexTool || tool is ReticleVertexTool);
            UniformScaleCheckBox.IsEnabled = !(tool is ReticleVertexTool);
        }

        private void CheckBox_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            var scaleMode = UniformScaleCheckBox.IsChecked ?
                GeometryEditorScaleMode.Uniform :
                GeometryEditorScaleMode.Stretch;

            foreach (var tool in _toolDictionary.Values)
            {
                if (tool is FreehandTool freehandTool)
                    freehandTool.Configuration.ScaleMode = scaleMode;
                else if (tool is VertexTool vertexTool)
                    vertexTool.Configuration.ScaleMode = scaleMode;
                else if (tool is ShapeTool shapeTool)
                    shapeTool.Configuration.ScaleMode = scaleMode;
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
            ResetFromEditingSession();
        }

        private async void GardenMapView_GeoViewTapped(object sender, Esri.ArcGISRuntime.Maui.GeoViewInputEventArgs e)
        {
            if (_geometryEditor.IsStarted) return;

            try
            {
                var results = await GardenMapView.IdentifyGraphicsOverlaysAsync(e.Position, 5, false);
                _selectedGraphic = results.FirstOrDefault()?.Graphics?.FirstOrDefault();

                if (_selectedGraphic == null) return;

                _selectedGraphic.IsSelected = true;
                var geometryType = _selectedGraphic.Geometry.GeometryType;

                if (geometryType == GeometryType.Point || geometryType == GeometryType.Multipoint)
                {
                    ToolPicker.SelectedIndex = 0;
                    UniformScaleCheckBox.IsEnabled = geometryType != GeometryType.Point;
                }

                DisableOtherGeometryButtons(_geometryButtons[geometryType]);
                _geometryEditor.Start(_selectedGraphic.Geometry);
                _selectedGraphic.IsVisible = false;
            }
            catch (Exception ex)
            {
                await Application.Current.Windows[0].Page.DisplayAlert("Error editing", ex.Message, "OK");
                ResetFromEditingSession();
            }
        }

        private void ToggleGeometryEditorPanelButton_Pressed(object sender, EventArgs e)
        {
            GeometryEditorPanel.IsVisible = !GeometryEditorPanel.IsVisible;
            ToggleGeometryEditorPanelButton.Text = GeometryEditorPanel.IsVisible ? "Hide UI" : "Show UI";
        }

        private void ResetFromEditingSession()
        {
            if (_selectedGraphic != null)
            {
                _selectedGraphic.IsSelected = false;
                _selectedGraphic.IsVisible = true;
                _selectedGraphic = null;
            }

            PointButton.IsEnabled = MultipointButton.IsEnabled =
                _geometryEditor.Tool is VertexTool || _geometryEditor.Tool is ReticleVertexTool;
            PolylineButton.IsEnabled = PolygonButton.IsEnabled = true;
            ToolPicker.IsEnabled = true;
            UniformScaleCheckBox.IsEnabled = true;
        }

        private Symbol GetSymbol(GeometryType geometryType)
        {
            return geometryType switch
            {
                GeometryType.Point => _pointSymbol,
                GeometryType.Multipoint => _multiPointSymbol,
                GeometryType.Polyline => _polylineSymbol,
                GeometryType.Polygon => _polygonSymbol,
                _ => null
            };
        }

        private void DisableOtherGeometryButtons(Button keepEnabled)
        {
            foreach (var button in _geometryButtons.Values)
            {
                button.IsEnabled = button == keepEnabled;
            }
        }
    }
}