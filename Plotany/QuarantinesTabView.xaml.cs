namespace Plotany;

public partial class QuarantinesTabView : ContentPage
{
    private QuarantinesTabViewModel _viewModel;
    public QuarantinesTabView(GardenManager gardenManager)
    {
        InitializeComponent();

        _viewModel = new QuarantinesTabViewModel(gardenManager);
        this.BindingContext = _viewModel;
    }
}