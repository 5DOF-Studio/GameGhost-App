using System.ComponentModel;
using System.Windows.Input;
using WitnessDesktop.Models;

namespace WitnessDesktop.Models.Timeline;

public class TimelineEvent : INotifyPropertyChanged
{
    private bool _isExpanded;

    public TimelineEvent()
    {
        // Latest event cannot be collapsed — only older events toggle
        ToggleExpandedCommand = new RelayCmd(() =>
        {
            if (IsLatest) return;
            if (IsDirectChat) return; // Direct chat never collapses
            IsExpanded = !IsExpanded;
        });
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public EventOutputType Type { get; set; }
    public string? Icon { get; set; }
    public string? CapsuleColorHex { get; set; }
    public string? CapsuleStrokeHex { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? FullContent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public BrainMetadata? Brain { get; set; }
    public MessageRole? Role { get; set; }
    /// <summary>True for direct chat messages that should never collapse.</summary>
    public bool IsDirectChat => Type == EventOutputType.DirectMessage;
    public ChatMessage? LinkedMessage { get; set; }
    public ToolCallInfo? ToolCall { get; set; }
    public MediaContent? Media { get; set; }

    /// <summary>True when this event should render as a full readable bubble. The global latest event is expanded by default.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCollapsed)));
        }
    }

    /// <summary>True when this event should render as a compact circular icon token.</summary>
    public bool IsCollapsed => !IsExpanded;

    /// <summary>True when this is the global latest event. Latest events cannot be manually collapsed.</summary>
    public bool IsLatest { get; set; }

    /// <summary>Tap-to-expand command for collapsed tokens.</summary>
    public ICommand ToggleExpandedCommand { get; }

    private class RelayCmd : ICommand
    {
        private readonly Action _execute;
        public RelayCmd(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    /// <summary>Resolved capsule background color for XAML binding.</summary>
    public Color CapsuleColor => Color.FromArgb(CapsuleColorHex ?? "#30808080");

    /// <summary>Resolved capsule stroke color for XAML binding.</summary>
    public Color CapsuleStroke => Color.FromArgb(CapsuleStrokeHex ?? "#50808080");

    public event PropertyChangedEventHandler? PropertyChanged;
}
