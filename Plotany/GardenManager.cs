using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;

namespace Plotany;

public class GardenManager
{
    private const string GardenLayerUri = "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/GardenLayers/FeatureServer/0";
    private ServiceFeatureTable _gardenFeatureTable;

    public string? GardenName { get; set; }

    public event EventHandler? GardenIdSet;

    public GardenManager(int? gardenId = null)
    {
        GardenName = null;
        _gardenFeatureTable = new ServiceFeatureTable(new Uri(GardenLayerUri));
    }

    public async Task<MapPoint?> GetGardenCenter()
    {
        if (GardenName == null)
        {
            return null;
        }

        var query = new QueryParameters
        {
            WhereClause = $"Name=\'{GardenName}\'",
            ReturnGeometry = true,
        };

        var result = await _gardenFeatureTable.QueryFeaturesAsync(query);
        var record = result.FirstOrDefault();

        if (record?.Geometry == null)
        {
            return null;
        }
        else
        {
            return GeometryEngine.LabelPoint(record.Geometry as Polygon);
        }
    }
}