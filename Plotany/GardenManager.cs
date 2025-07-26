using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;

namespace Plotany;

public class GardenManager
{
    private const string GardenLayerUri = "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/GardenLayers/FeatureServer/0";
    private ServiceFeatureTable _gardenFeatureTable;

    private string? _gardenName;
    public string? GardenName
    {
        get => _gardenName;
        set
        {
            if (_gardenName != value)
            {
                _gardenName = value;
                OnGardenNameChanged();
            }
        }
    }

    public event EventHandler? GardenIdSet;
    public event EventHandler? GardenNameChanged;

    public GardenManager(int? gardenId = null)
    {
        GardenName = null;
        _gardenFeatureTable = new ServiceFeatureTable(new Uri(GardenLayerUri));
    }

    protected virtual void OnGardenNameChanged()
    {
        GardenNameChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<MapPoint> GetGardenCenter()
    {
        if (GardenName == null)
        {
            throw new InvalidOperationException("Cannot get garden center point: garden name is not set.");
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
            throw new InvalidOperationException("Cannot get garden center point: garden has no geometry.");
        }
        else
        {
            return GeometryEngine.LabelPoint(record.Geometry as Polygon);
        }
    }
}