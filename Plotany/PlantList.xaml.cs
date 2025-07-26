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
    private ObservableCollection<string> resultItems = new ObservableCollection<string>();
    public PlantList()
	{
		InitializeComponent();
        ResultsCollection.ItemsSource = resultItems;
    }

    public async Task<string> queryDataAtMapPoint(string url, string field, double x, double y)
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
            await DisplayAlert("Info", $"Found: {value}", "OK");
            return value.ToString();

        }
        else
        {
            await DisplayAlert("Info", "Not Found: no data", "OK");
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

        if (!result.Any())
        {
            Console.WriteLine("No matching records found.");
            return;
        }

        foreach (var feature in result)
        {
            string ID = feature.Attributes.TryGetValue("ID", out var mk) ? mk?.ToString() ?? "N/A" : "N/A";
            string commonName = feature.Attributes.TryGetValue("Common_Name", out var cn) ? cn?.ToString() ?? "N/A" : "N/A";
            string formatted = $"ID: {ID}, Common Name: {commonName}";
            resultItems.Add(formatted);
        }
    }

    private async void GetSoil(object sender, EventArgs e)
    {
        await queryDataAtMapPoint("https://landscape11.arcgis.com/arcgis/rest/services/USA_Soils_Map_Units/featureserver/0", "esrisymbology", -118.805000, 34.027000);
    }

    private async void GetClimate(object sender, EventArgs e)
    {
        await queryDataAtMapPoint("https://services7.arcgis.com/oF9CDB4lUYF7Um9q/arcgis/rest/services/NA_Climate_Zones/FeatureServer/5", "Climate", -118.805000, 34.027000);
    }

    private async void GetPlant(object sender, EventArgs e)
    {
        await GetPlantList("Aridisols", "Hot-Summer Mediterranean Climate");
    }
}