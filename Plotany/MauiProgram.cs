using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.Toolkit.Maui;

namespace Plotany
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .UseArcGISRuntime(config => config
                .UseApiKey("AAPTxy8BH1VEsoebNVZXo8HurNcmxnU9u4gO2oOoNPf4_nk6SmmuM466iEB7-kqVyRIqHGEDrQ9ikV61_r_0o3lt5t6TtZyAb8bWzVwzNqzQFf2JjIjsEVxXmftAUeoK11qZuqneceuMVjO2luVcBAPc_91eHEn0sv0bzt525a_f80wHR3A73vCg_kz2OvUo9PWKP1xOePpfz7EpRVlKjzKZhOaDgVxr8uHZyrhxSZLOWYzdDVYbpRSDz5ekdRtfUpiVAT1_9vPX9Rgx")
                ).UseArcGISToolkit();

            ArcGISRuntimeEnvironment.EnableTimestampOffsetSupport = true;

            builder.Services.AddSingleton<GardenManager>();
            builder.Services.AddTransient<MainPageViewModel>();

            return builder.Build();
        }
    }
}
