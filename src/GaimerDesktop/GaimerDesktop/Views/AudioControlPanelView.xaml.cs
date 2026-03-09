using GaimerDesktop.ViewModels;

namespace GaimerDesktop.Views;

public partial class AudioControlPanelView : ContentView
{
    private MainViewModel? _viewModel;

    public AudioControlPanelView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(MainViewModel vm)
    {
        DetachViewModel();
        _viewModel = vm;
        BindingContext = vm;
    }

    public void DetachViewModel()
    {
        if (_viewModel != null)
        {
            _viewModel = null;
            BindingContext = null;
        }
    }
}
