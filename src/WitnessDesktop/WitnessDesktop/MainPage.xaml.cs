using WitnessDesktop.Models;
using WitnessDesktop.ViewModels;
using WitnessDesktop.Views;

namespace WitnessDesktop;

[QueryProperty(nameof(AgentId), "agentId")]
public partial class MainPage : ContentPage
{
    private MainViewModel? _viewModel;
    private string? _agentId;

    public string? AgentId
    {
        get => _agentId;
        set
        {
            _agentId = value;
            if (_viewModel != null && !string.IsNullOrEmpty(value))
            {
                _viewModel.SelectedAgent = Agents.GetByKey(value);
            }
        }
    }

    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        
        _viewModel = Application.Current?.Handler?.MauiContext?.Services.GetService<MainViewModel>();
        if (_viewModel != null)
        {
            BindingContext = _viewModel;
            
            if (!string.IsNullOrEmpty(_agentId))
            {
                _viewModel.SelectedAgent = Agents.GetByKey(_agentId);
            }
            
            _ = _viewModel.LoadCaptureTargetsCommand.ExecuteAsync(null);

            FabOverlay.AttachViewModel(_viewModel);
        }
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        FabOverlay.DetachViewModel();
    }
}
