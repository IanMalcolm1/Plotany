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

	public async Task queryMapPoint()
	{
		string url = "https://landscape11.arcgis.com/arcgis/rest/services/USA_Soils_Map_Units/featureserver/0";
        var serviceFeatureTable = new ServiceFeatureTable(new Uri(url));

        double tolerance = 0.0001; // meters
        var mapPoint = new MapPoint(-118.805000, 34.027000, SpatialReferences.Wgs84); 

        // Buffer the point slightly to make spatial query practical
        var envelope = new Envelope(
            mapPoint.X - tolerance,
            mapPoint.Y - tolerance,
            mapPoint.X + tolerance,
            mapPoint.Y + tolerance,
            mapPoint.SpatialReference
        );

        // Create query parameters
        var queryParams = new QueryParameters
        {
            Geometry = envelope,
            SpatialRelationship = SpatialRelationship.Intersects,
            MaxFeatures = 1
        };

        var result = await serviceFeatureTable.QueryFeaturesAsync(queryParams);
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

    private async void OnQueryButtonClicked(object sender, EventArgs e)
    {
        await queryMapPoint();
    }
}