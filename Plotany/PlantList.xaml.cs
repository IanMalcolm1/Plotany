
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using System.Collections.ObjectModel;

namespace Plotany;

public partial class PlantList : ContentPage
{
    private const string PLANTS_TABLE_URL = "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/Plant_data_v3/FeatureServer/0";
    private const string MY_SEEDS_TABLE_URL = "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/GardenPlants/FeatureServer/0";
    private const string SOIL_LAYER_URL = "https://landscape11.arcgis.com/arcgis/rest/services/USA_Soils_Map_Units/featureserver/0";
    private const string CLIMATE_LAYER_URL = "https://services7.arcgis.com/oF9CDB4lUYF7Um9q/arcgis/rest/services/NA_Climate_Zones/FeatureServer/5";

    private GardenManager _gardenManager;

    private ObservableCollection<string> plantCollectionItems = new ObservableCollection<string>();

    Dictionary<string, int> plantDict = new Dictionary<string, int>();

    bool hasLoaded = false;

    public PlantList(GardenManager gardenManager)
    {
        InitializeComponent();
        PlantCollection.ItemsSource = plantCollectionItems;

        _gardenManager = gardenManager;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!hasLoaded)
        {
            GetPlant(null, null);
            hasLoaded = true;
        }
    }

    public async Task<string> QueryDataAtMapPoint(string url, string field, MapPoint point)
    {
        var serviceFeatureTable = new ServiceFeatureTable(new Uri(url));
        await serviceFeatureTable.LoadAsync();

        // Create query parameters
        var queryParams = new QueryParameters
        {
            WhereClause = "1=1",
            Geometry = point,
            SpatialRelationship = SpatialRelationship.Intersects,
            ReturnGeometry = false
        };

        var result = await serviceFeatureTable.QueryFeaturesAsync(queryParams, QueryFeatureFields.LoadAll);
        var feature = result.FirstOrDefault();

        if (feature != null && feature.Attributes.TryGetValue(field, out var value))
        {
            //await DisplayAlert("Info", $"Found: {value}", "OK");
            return value.ToString();
        }
        else
        {
            //await DisplayAlert("Info", "Not Found: no data", "OK");
            return "Not Found: no data";
        }

    }

    private async Task GetPlantList(string soilType, string climateType)
    {
        var table = new ServiceFeatureTable(new Uri(PLANTS_TABLE_URL));

        var queryParams = new QueryParameters
        {
            WhereClause = $"Esri_Symbology = '{soilType}' AND Climate = '{climateType}'"
        };

        // Query all features and include all fields
        var result = await table.QueryFeaturesAsync(queryParams, QueryFeatureFields.LoadAll);

        soilInfo.Text = ($"We found this soil in your garden:\n {soilType}");
        soilBox.IsVisible = true;

        climateInfo.Text = ($"You live in this climate:\n {climateType}");
        climateBox.IsVisible = true;

        if (!result.Any())
        {
            plantCollectionItems.Add("Found no plants that can grow in your soil and climate :(");
            return;
        }
        else
        {
            plantBox.IsVisible = true;
        }

        foreach (var feature in result)
        {
            string ID = feature.Attributes.TryGetValue("ID", out var mk) ? mk?.ToString() ?? "N/A" : "N/A";
            string commonName = feature.Attributes.TryGetValue("Common_Name", out var cn) ? cn?.ToString() ?? "N/A" : "N/A";
            plantDict.Add(commonName, Convert.ToInt32(ID));
            string formatted = commonName;
            plantCollectionItems.Add(formatted);
        }
    }

    public async void GetPlant(object sender, EventArgs e)
    {
        var centerPoint = await _gardenManager.GetGardenCenter();
        //runs on initial page load
        var soilType = await QueryDataAtMapPoint(SOIL_LAYER_URL, "esrisymbology", centerPoint);
        var climateType = await QueryDataAtMapPoint(CLIMATE_LAYER_URL, "Climate", centerPoint);

        /*await Task.WhenAll(soilTask, climateTask);

        var soilType = soilTask.Result;
        var climateType = climateTask.Result;*/

        await GetPlantList(soilType, climateType);
    }

    private async void AddPlantToGarden(object sender, EventArgs e)
    {
        var table = new ServiceFeatureTable(new Uri(MY_SEEDS_TABLE_URL));
        await table.LoadAsync();
        var newFeature = table.CreateFeature();
        var clickedButton = (Button)sender;

        var queryParams = new QueryParameters
        {
            WhereClause = $"garden_name = '{_gardenManager.GardenName}' AND plant_database_id = {plantDict[clickedButton.Text]}", // use no quotes if ID is a number
            MaxFeatures = 1
        };
        var results = await table.QueryFeaturesAsync(queryParams, QueryFeatureFields.LoadAll);


        if (results.Any())
        {
            await DisplayAlert("Alert!", "You already have that seed in your bank!", "OK");
        }
        else
        {
            newFeature.Attributes["garden_name"] = _gardenManager.GardenName;
            newFeature.Attributes["plant_name"] = clickedButton.Text;
            newFeature.Attributes["plant_database_id"] = plantDict[clickedButton.Text];
            await table.AddFeatureAsync(newFeature);
            await table.ApplyEditsAsync();

            // Save updated text to preferences
            Preferences.Set("ButtonText", clickedButton.Text);
            await DisplayAlert("Success", "Plant added to your seed bank!", "OK");
        }
    }
}