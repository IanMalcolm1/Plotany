using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esri.ArcGISRuntime.Geometry;

namespace Plotany;
public class WeatherForecast
{
    public DateTime Date { get; set; }
    public double? TempMax { get; set; }
    public double? TempMin { get; set; }
    public double? Precipitation { get; set; }
}

/// <summary>
/// Provides map data to an application
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    private GardenManager _gardenManager;

    [ObservableProperty] private bool _needGardenName = true;
    [ObservableProperty] private string? _gardenNameWarning = string.Empty;
    public bool IsSetGardenNameWarningVisible => !string.IsNullOrEmpty(GardenNameWarning);
    [ObservableProperty] private string _gardenNameInput = string.Empty;
    [ObservableProperty] private string _weatherWarning = string.Empty;

    public MainPageViewModel(GardenManager gardenManager)
    {
        _gardenManager = gardenManager;
        _gardenManager.GardenNameChanged += async (s, e) => await HandleGardenNameChanged();
    }
    partial void OnGardenNameWarningChanged(string? value)
    {
        OnPropertyChanged(nameof(IsSetGardenNameWarningVisible));
    }

    [RelayCommand]
    private async Task SetGardenName()
    {
        try
        {
            await _gardenManager.SetGardenName(GardenNameInput);
            NeedGardenName = false;
        }
        catch
        {
            GardenNameWarning = "Error setting garden name: " + GardenNameInput + ". Please make sure the name was input correctly";
        }
    }

    private async Task HandleGardenNameChanged()
    {
        if (string.IsNullOrEmpty(_gardenManager.GardenName))
        {
            WeatherWarning = "Please set a garden name to get weather data.";
            return;
        }

        try
        {
            NeedGardenName = false;
            var forecast = await Get7DayWeatherAsync(await _gardenManager.GetGardenCenter());

            List<DateTime> hotDays = new List<DateTime>();
            List<DateTime> coldDays = new List<DateTime>();
            bool heavyRain = false;

            foreach (var day in forecast)
            {
                WeatherWarning = string.Empty;
                if (day.TempMin != null && day.TempMin <= 20)
                {
                    coldDays.Add(day.Date);
                }
                else if (day.TempMax != null && day.TempMax >= 90)
                {
                    hotDays.Add(day.Date);
                }
                else if (day.Precipitation != null && day.Precipitation > 0.5)
                {
                    heavyRain = true;
                }
            }

            var weatherWarning = string.Empty;
            if (hotDays.Count > 0)
            {
                weatherWarning += "High heat expected: " + string.Join(", ", hotDays.Select(d => d.ToString("M/d")));
            }
            if (coldDays.Count > 0)
            {
                weatherWarning += "\nExcessive cold expected: " + string.Join(", ", coldDays.Select(d => d.ToString("d / M")));
            }
            if (heavyRain)
            {
                weatherWarning += "\nHeavy rainfall expected, turn off your sprinklers!";
            }

            WeatherWarning = weatherWarning;
        }
        catch (Exception ex)
        {
            WeatherWarning = "Error getting weather data. Please try again later.";
            return;
        }
    }

    public async Task<List<WeatherForecast>> Get7DayWeatherAsync(MapPoint coords)
    {
        var longitude = coords.X;
        var latitude = coords.Y;

        using var httpClient = new HttpClient();
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={coords.Y}&longitude={coords.X}&forecast_days=5&daily=apparent_temperature_min,apparent_temperature_max,rain_sum&temperature_unit=fahrenheit&precipitation_unit=inch";
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        var daily = root.GetProperty("daily");

        var dates = daily.GetProperty("time").EnumerateArray().Select(e => DateTime.Parse(e.GetString()!)).ToList();
        var tempMax = daily.GetProperty("apparent_temperature_max").EnumerateArray().Select(e => e.GetDouble()).ToList();
        var tempMin = daily.GetProperty("apparent_temperature_min").EnumerateArray().Select(e => e.GetDouble()).ToList();
        var precipitation = daily.GetProperty("rain_sum").EnumerateArray().Select(e => e.GetDouble()).ToList();

        var forecasts = new List<WeatherForecast>();
        for (int i = 0; i < dates.Count; i++)
        {
            forecasts.Add(new WeatherForecast
            {
                Date = dates[i],
                TempMax = tempMax[i],
                TempMin = tempMin[i],
                Precipitation = precipitation[i]
            });
        }

        return forecasts;
    }

    [RelayCommand]
    private async Task NavigateMakeGarden()
    {
        await Shell.Current.GoToAsync("///ViewGarden");
    }
}
