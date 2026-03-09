using GaimerDesktop.Models;
using GaimerDesktop.ViewModels;
using GaimerDesktop.Views;

namespace GaimerDesktop;

[QueryProperty(nameof(AgentId), "agentId")]
public partial class MainPage : ContentPage
{
    private MainViewModel? _viewModel;
    private string? _agentId;
    private bool _suppressTextChanged;

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
        ChatEditor.TextChanged += OnChatEditorTextChanged;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        _viewModel = Application.Current?.Handler?.MauiContext?.Services.GetService<MainViewModel>();
        if (_viewModel != null)
        {
            if (!string.IsNullOrEmpty(_agentId))
            {
                _viewModel.SelectedAgent = Agents.GetByKey(_agentId);
            }

            _viewModel.IsPageReady = true;
            _viewModel.UnsupportedAudioFeatureToggled += OnUnsupportedAudioFeature;
            BindingContext = _viewModel;

            _ = _viewModel.LoadCaptureTargetsCommand.ExecuteAsync(null);

            FabOverlay.AttachViewModel(_viewModel);
            AudioPanel.AttachViewModel(_viewModel);
        }
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (_viewModel != null)
            _viewModel.UnsupportedAudioFeatureToggled -= OnUnsupportedAudioFeature;
        ChatEditor.TextChanged -= OnChatEditorTextChanged;
        FabOverlay.DetachViewModel();
        AudioPanel.DetachViewModel();
    }

    /// <summary>
    /// Enter sends the message. The Editor inserts \n on Enter before TextChanged fires.
    /// If the new text ends with \n and the old text didn't, it was a bare Enter press.
    /// Strip the \n and send. Shift+Enter inserts a real newline (the Editor handles this natively
    /// on Mac Catalyst — Shift+Enter produces a line break without triggering "Completed").
    /// </summary>
    private void OnChatEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged || _viewModel == null) return;

        // Detect bare Enter: new text ends with \n but old text didn't
        if (e.NewTextValue != null && e.NewTextValue.EndsWith('\n') &&
            (e.OldTextValue == null || !e.OldTextValue.EndsWith('\n')))
        {
            // Strip the trailing newline and send
            _suppressTextChanged = true;
            _viewModel.MessageDraftText = e.NewTextValue.TrimEnd('\n', '\r');
            _suppressTextChanged = false;

            if (_viewModel.CanSendTextMessage)
            {
                _ = _viewModel.SendTextMessageCommand.ExecuteAsync(null);
            }
        }
    }

    private async void OnUnsupportedAudioFeature(object? sender, string featureName)
    {
        var agentName = _viewModel?.SelectedAgent?.Name ?? "This agent";
        await DisplayAlert(
            "Not Supported",
            $"{agentName} does not support: {featureName}",
            "OK");
    }
}
