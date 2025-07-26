using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;

namespace Plotany;

public partial class PlantList : ContentPage
{
	public PlantList()
	{
		InitializeComponent();
	}

	public async Task queryMapPoint()
	{
		string url = "https://landscape11.arcgis.com/arcgis/rest/services/USA_Soils_Map_Units/featureserver";
        var serviceFeatureTable = new ServiceFeatureTable(new Uri(url));

        double tolerance = 10; // meters
        var mapPoint = new MapPoint(-118.805, 34.027, SpatialReferences.Wgs84);

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

        await serviceFeatureTable.LoadAsync(); // Ensure the table is loaded
        var result = await serviceFeatureTable.QueryFeaturesAsync(queryParams);
        var feature = result.FirstOrDefault();

        if (feature != null)
        {
            // Access attributes
            foreach (var kvp in feature.Attributes)
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
            }
        }
        else
        {
            Console.WriteLine("No features found at point.");
        }
    }

    private async void OnQueryButtonClicked(object sender, EventArgs e)
    {
        await queryMapPoint();
    }
}