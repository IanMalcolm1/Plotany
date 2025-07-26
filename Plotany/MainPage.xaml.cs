using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;

namespace Plotany
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            this.BindingContext = new MapViewModel();

        }

        
    }

}
