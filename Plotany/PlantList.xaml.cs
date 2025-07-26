using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using System.Text;

namespace Plotany;

public partial class PlantList : ContentPage
{
	public PlantList()
	{
		InitializeComponent();
	}

	public async Task querySoilAtMapPoint()
	{
		string url = "https://landscape11.arcgis.com/arcgis/rest/services/USA_Soils_Map_Units/featureserver/0";
        var serviceFeatureTable = new ServiceFeatureTable(new Uri(url));

        var mapPoint = new MapPoint(-118.805000, 34.027000, SpatialReferences.Wgs84); 

        // Create query parameters
        var queryParams = new QueryParameters
        {
            Geometry = mapPoint,
            SpatialRelationship = SpatialRelationship.Intersects,
            MaxFeatures = 1
        };

        var result = await serviceFeatureTable.QueryFeaturesAsync(queryParams, QueryFeatureFields.LoadAll);
        var feature = result.FirstOrDefault();


        if (feature.Attributes.TryGetValue("esrisymbology", out var value) && value != null)
        {
            await DisplayAlert("Soil Info", $"Soil Found: {value}", "OK");
        }
        else
        {
            await DisplayAlert("Soil Info", "Not Found: no soil", "OK");
        }

    }

    private async void GetSoilOnClick(object sender, EventArgs e)
    {
        await querySoilAtMapPoint();
    }
}