using AndroidX.Camera.Video;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using System.Collections.ObjectModel;
using System.Text;
using static Android.Renderscripts.Script;

namespace Plotany;

public partial class PlantList : ContentPage
{
    private ObservableCollection<string> plantCollectionItems = new ObservableCollection<string>();
    public PlantList()
	{
		InitializeComponent();
        PlantCollection.ItemsSource = plantCollectionItems;
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
        var table = new ServiceFeatureTable(new Uri("https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/Plant_data_v3/FeatureServer/0"));
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
            string formatted = commonName;
            plantCollectionItems.Add(formatted);
        }
    }

    private async Task<string> GetSoil()
    {
        return await queryDataAtMapPoint("https://landscape11.arcgis.com/arcgis/rest/services/USA_Soils_Map_Units/featureserver/0", "esrisymbology", 34.061032307283796, -117.20523623544922);
    }

    private async Task<string> GetClimate()
    {
        return await queryDataAtMapPoint("https://services7.arcgis.com/oF9CDB4lUYF7Um9q/arcgis/rest/services/NA_Climate_Zones/FeatureServer/5", "Climate", 34.061032307283796, -117.20523623544922);
    }

    private async void GetPlant(object sender, EventArgs e)
    {
        startButton.IsVisible = false;
        string userSoilType = await GetSoil();
        string userClimate = await GetClimate();
        await GetPlantList(userSoilType, userClimate);
        //await GetPlantList("Entisols", "Hot-Summer Mediterranean Climate");
    }
}