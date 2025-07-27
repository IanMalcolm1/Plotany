using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Mapping.Labeling;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Editing;
using System.ComponentModel;

namespace Plotany
{
    public partial class ViewGarden : ContentPage, INotifyPropertyChanged
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
        private string _gardenNameInput = String.Empty;
        private bool _showGardenNameInput = false;

        private GardenManager _gardenManager;

        public ViewGarden(GardenManager gardenManager)
        {
            InitializeComponent();
            Initialize();
            LoadSavedDrawingsAsync();

            BindingContext = this;

            _gardenManager = gardenManager;
            if (_gardenManager.GardenName == null)
            {
                Shell.SetTabBarIsVisible(this, false);
            }
            _gardenManager.GardenNameChanged += (s, e) => Shell.SetTabBarIsVisible(this, true);
        }

        private void Initialize()
        {
            GardenMapView.Map = new Esri.ArcGISRuntime.Mapping.Map(BasemapStyle.ArcGISImageryStandard);
            _gardenOverlay = new GraphicsOverlay();
            GardenMapView.GraphicsOverlays.Add(_gardenOverlay);
            _locator = new LocatorTask(new Uri("https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer"));
            _pointSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Red, 10);
            _polylineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Red, 3);
            _polygonSymbol = new SimpleFillSymbol(
                SimpleFillSymbolStyle.Solid,
                System.Drawing.Color.Transparent,
                new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.DarkGreen, 2));
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
                var geometry = _geometryEditor.Stop();
                if (geometry == null)
                {
                    await DisplayAlert("Nothing to save", "No polygon drawn.", "OK");
                    return;
                }
                if (geometry.GeometryType == GeometryType.Point)
                {
                    await SaveToArcGISOnlineAsync(geometry, gardenName, "my1st");
                    await LoadFromArcGISOnlineAsync();
                    await DisplayAlert("Success", "Mapped your plant", "OK");
                    ResetFromEditingSession();
                    return;
                }
                if (geometry.GeometryType == GeometryType.Polygon)
                {
                    await SaveToArcGISOnlineAsync(geometry, gardenName, "");
                    await LoadFromArcGISOnlineAsync();

                    await DisplayAlert("Success", "Polygon saved successfully!", "OK");
                    ResetFromEditingSession();
                }


                if (_gardenManager.GardenName == null)
                {
                    await _gardenManager.SetGardenName(gardenName);
                    await Shell.Current.GoToAsync("///PlantList");
                }
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
                // Clear existing graphics and layers
                _gardenOverlay.Graphics.Clear();
                GardenMapView.Map.OperationalLayers.Clear();

                // Define URLs for garden and plant feature layers
                string gardenLayerUrl = "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/GardenLayers/FeatureServer/0";
                string plantLayerUrl = "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/PlantedPlants/FeatureServer/0";

                // Create and load garden feature layer
                var gardenFeatureTable = new ServiceFeatureTable(new Uri(gardenLayerUrl));
                var gardenLayer = new FeatureLayer(gardenFeatureTable);
                await gardenLayer.LoadAsync();
                if (gardenLayer.LoadStatus != Esri.ArcGISRuntime.LoadStatus.Loaded)
                {
                    await DisplayAlert("Load Error", "Failed to load garden layer. Check API key or URL.", "OK");
                    EmptyStatePanel.IsVisible = true;
                    return;
                }
                GardenMapView.Map.OperationalLayers.Add(gardenLayer);

                // Apply renderer to garden layer
                gardenLayer.Renderer = new SimpleRenderer(_polygonSymbol);

                // Query for the garden named "sam"
                var gardenQuery = new QueryParameters
                {
                    WhereClause = "Name = 'sam'",
                    ReturnGeometry = true,
                    MaxFeatures = 1
                };
                var gardenResult = await gardenFeatureTable.QueryFeaturesAsync(gardenQuery);
                var gardenFeature = gardenResult.FirstOrDefault();
                if (gardenFeature == null)
                {
                    await DisplayAlert("Not Found", "No feature with name 'sam' found.", "OK");
                    EmptyStatePanel.IsVisible = true;
                    return;
                }

                // Zoom to garden extent
                var extent = gardenFeature.Geometry.Extent;
                var expandedExtent = new Envelope(
                    extent.XMin - extent.Width * 0.25,
                    extent.YMin - extent.Height * 0.25,
                    extent.XMax + extent.Width * 0.25,
                    extent.YMax + extent.Height * 0.25,
                    extent.SpatialReference);
                await GardenMapView.SetViewpointGeometryAsync(expandedExtent, 50);

                // Create and load plant feature layer
                var plantFeatureTable = new ServiceFeatureTable(new Uri(plantLayerUrl));
                var plantLayer = new FeatureLayer(plantFeatureTable);
                await plantLayer.LoadAsync();
                if (plantLayer.LoadStatus == Esri.ArcGISRuntime.LoadStatus.Loaded)
                {
                    plantLayer.Renderer = new SimpleRenderer(_pointSymbol);
                    GardenMapView.Map.OperationalLayers.Add(plantLayer);
                }

                // Apply label definitions
                _gardenOverlay.LabelDefinitions.Clear();
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

                EmptyStatePanel.IsVisible = false;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Load Error", $"Failed to load from ArcGIS Online: {ex.Message}", "OK");
                EmptyStatePanel.IsVisible = true;
            }
        }

        private async Task SaveToArcGISOnlineAsync(Geometry geometry, string gardenName, string plantName)
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
                    await GardenMapView.SetViewpointCenterAsync(new MapPoint(-117.1828359, 34.0383765, SpatialReferences.Wgs84), 100);
                }
                else
                {
                    var fallbackPoint = new MapPoint(-117.1828359, 34.0383765, SpatialReferences.Wgs84);
                    await GardenMapView.SetViewpointCenterAsync(fallbackPoint, 100);
                    await DisplayAlert("Location fallback", "Using simulated location (Hyderabad).", "OK");
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

        private void PointButton_Click(object sender, EventArgs e)
        {
            if (!_geometryEditor.IsStarted)
            {
                _geometryEditor.Start(GeometryType.Point);
            }
        }

        private void PolylineButton_Click(object sender, EventArgs e)
        {
            if (!_geometryEditor.IsStarted)
            {
                _geometryEditor.Start(GeometryType.Polyline);
            }
        }

        private void PolygonButton_Click(object sender, EventArgs e)
        {
            if (!_geometryEditor.IsStarted)
            {
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
                _geometryEditor.Start(_selectedGraphic.Geometry);
                _selectedGraphic.IsVisible = false;
            }
            catch (Exception ex)
            {
                await Application.Current.Windows[0].Page.DisplayAlert("Error editing", ex.Message, "OK");
                ResetFromEditingSession();
            }
        }

        private void ResetFromEditingSession()
        {
            if (_selectedGraphic != null)
            {
                _selectedGraphic.IsSelected = false;
                _selectedGraphic.IsVisible = true;
                _selectedGraphic = null;
            }
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
    }
}