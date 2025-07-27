using Android.Gms.Common;
using AndroidX.Camera.Video;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using System.Collections.ObjectModel;
using System.Net.Mail;
using System.Text;
using static Android.Renderscripts.Script;

namespace Plotany;

public partial class PlantList : ContentPage
{
    private ObservableCollection<string> plantCollectionItems = new ObservableCollection<string>();

    Dictionary<string, int> plantDict = new Dictionary<string, int>();

    bool hasLoaded = false;

    public PlantList()
    {
        InitializeComponent();
        PlantCollection.ItemsSource = plantCollectionItems;

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

    public async Task<string> queryDataAtMapPoint(string url, string field, double y, double x)
    {
        var serviceFeatureTable = new ServiceFeatureTable(new Uri(url));

        var mapPoint = new MapPoint(x, y, SpatialReferences.Wgs84);

        // Create query parameters
        var queryParams = new QueryParameters
        {
            Geometry = mapPoint,
            SpatialRelationship = SpatialRelationship.Intersects,
            MaxFeatures = 1
        };

        var result = await serviceFeatureTable.QueryFeaturesAsync(queryParams, QueryFeatureFields.LoadAll);
        var feature = result.FirstOrDefault();

        if (feature.Attributes.TryGetValue(field, out var value) && value != null)
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
        var table = new ServiceFeatureTable(new Uri("https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/Plant_data/FeatureServer/0"));

        await table.LoadAsync(); // Required to access schema

        string whereClause = $"Esri_Symbology = '{soilType}' AND Climate = '{climateType}'";

        var queryParams = new QueryParameters
        {
            WhereClause = whereClause,
            MaxFeatures = 1000
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

    public async Task<string> GetSoil()
    {
        return await queryDataAtMapPoint("https://landscape11.arcgis.com/arcgis/rest/services/USA_Soils_Map_Units/featureserver/0", "esrisymbology", 34.061032307283796, -117.20523623544922);
    }

    public async Task<string> GetClimate()
    {
        return await queryDataAtMapPoint("https://services7.arcgis.com/oF9CDB4lUYF7Um9q/arcgis/rest/services/NA_Climate_Zones/FeatureServer/5", "Climate", 34.061032307283796, -117.20523623544922);
    }

    public async void GetPlant(object sender, EventArgs e)
    {
        //runs on initial page load
        string userSoilType = await GetSoil();
        string userClimate = await GetClimate();
        await GetPlantList(userSoilType, userClimate);
    }

    private async void AddPlantToGarden(object sender, EventArgs e)
    {
        var table = new ServiceFeatureTable(new Uri("https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/GardenPlants/FeatureServer/0"));
        await table.LoadAsync();
        var newFeature = table.CreateFeature();
        var clickedButton = (Button)sender;

        var queryParams = new QueryParameters
        {
            WhereClause = $"plant_database_id = {plantDict[clickedButton.Text]}", // use no quotes if ID is a number
            MaxFeatures = 1
        };
        var results = await table.QueryFeaturesAsync(queryParams, QueryFeatureFields.LoadAll);


        if(results.Any())
        {
            await DisplayAlert("Alert!", "You already have that seed in your bank!", "OK");
        }
        else
        {
            newFeature.Attributes["garden_name"] = "My Garden";
            newFeature.Attributes["plant_name"] = clickedButton.Text;
            newFeature.Attributes["plant_database_id"] = plantDict[clickedButton.Text];
            await table.AddFeatureAsync(newFeature);

            // Save updated text to preferences
            Preferences.Set("ButtonText", clickedButton.Text);
            await DisplayAlert("Success", "Plant added to your seed bank!", "OK");
        }





    }
}