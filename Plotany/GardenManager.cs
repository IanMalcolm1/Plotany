using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;

namespace Plotany;

public class GardenManager
{
    private const string GardenLayerUri = "https://services8.arcgis.com/LLNIdHmmdjO2qQ5q/arcgis/rest/services/GardenLayers/FeatureServer/0";
    private ServiceFeatureTable _gardenFeatureTable;

    private string? _gardenName;
    public string? GardenName { get => _gardenName; }
    private Feature? _gardenFeature;

    public event EventHandler? GardenIdSet;
    public event EventHandler? GardenNameChanged;

    public GardenManager(int? gardenId = null)
    {
        _gardenName = null;
        _gardenFeature = null;
        _gardenFeatureTable = new ServiceFeatureTable(new Uri(GardenLayerUri));
    }

    protected virtual void OnGardenNameChanged()
    {
        GardenNameChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetGardenName(string name)
    {
        if (_gardenName == name)
        {
            return;
        }

        var fallback = _gardenName;
        try
        {
            _gardenName = name;
            await RefreshGardenData();
            OnGardenNameChanged();
        }
        catch
        {
            _gardenName = fallback;
            throw InvalidOperationException("No records were found for the given garden name");
        }
    }

    private Exception InvalidOperationException(string v)
    {
        throw new NotImplementedException();
    }

    public async Task<MapPoint> GetGardenCenter()
    {
        if (_gardenFeature?.Geometry == null)
        {
            throw new InvalidOperationException("Cannot get garden center point: no garden geometry data stored.");
        }
        else
        {
            return GeometryEngine.LabelPoint(_gardenFeature.Geometry as Polygon);
        }
    }

    public async Task RefreshGardenData()
    {
        if (_gardenName == null)
        {
            throw new InvalidOperationException("Cannot get garden data: garden name is not set.");
        }

        var query = new QueryParameters
        {
            WhereClause = $"Name=\'{_gardenName}\'",
            ReturnGeometry = true,
            OutSpatialReference=SpatialReferences.Wgs84
        };

        var result = await _gardenFeatureTable.QueryFeaturesAsync(query);
        var record = result.FirstOrDefault();

        if (record == null)
        {
            throw new InvalidOperationException("Cannot get garden data: garden name is invalid.");
        }
        else
        {
            _gardenFeature = record;
        }
    }
}