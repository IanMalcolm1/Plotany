using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;

namespace Plotany
{
    public partial class MainPage : ContentPage
    {
        private MainPageViewModel _viewModel;
        private GardenManager _gardenManager;

        public MainPage(MainPageViewModel viewModel, GardenManager gardenManager)
        {
            InitializeComponent();
            Routing.RegisterRoute("ViewGarden", typeof(ViewGarden));

            Shell.SetTabBarIsVisible(this, false);

            _gardenManager = gardenManager;
            _gardenManager.GardenNameChanged += (s, e) => Shell.SetTabBarIsVisible(this, true);

            _viewModel = viewModel;
            this.BindingContext = _viewModel;
        }

        
    }

}
