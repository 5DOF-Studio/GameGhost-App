namespace GaimerDesktop.Controls;

public partial class HorizontalIndustrialToggle : ContentView
{
    // Handle travel: rail width (60) - handle width (28) - padding (4) = 28px
    private const double HandleTravel = 28;

    public static readonly BindableProperty IsToggledProperty =
        BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(HorizontalIndustrialToggle),
            false, BindingMode.TwoWay, propertyChanged: OnIsToggledChanged);

    public static readonly BindableProperty LedOnColorProperty =
        BindableProperty.Create(nameof(LedOnColor), typeof(Color), typeof(HorizontalIndustrialToggle),
            Color.FromArgb("#f2a900"));

    public static readonly BindableProperty LedOffColorProperty =
        BindableProperty.Create(nameof(LedOffColor), typeof(Color), typeof(HorizontalIndustrialToggle),
            Color.FromArgb("#3d1515"));

    public static readonly BindableProperty IsInteractiveProperty =
        BindableProperty.Create(nameof(IsInteractive), typeof(bool), typeof(HorizontalIndustrialToggle),
            true);

    public bool IsToggled
    {
        get => (bool)GetValue(IsToggledProperty);
        set => SetValue(IsToggledProperty, value);
    }

    public Color LedOnColor
    {
        get => (Color)GetValue(LedOnColorProperty);
        set => SetValue(LedOnColorProperty, value);
    }

    public Color LedOffColor
    {
        get => (Color)GetValue(LedOffColorProperty);
        set => SetValue(LedOffColorProperty, value);
    }

    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    private double _panStartX;
    private bool _isDragging;

    public HorizontalIndustrialToggle()
    {
        InitializeComponent();

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        GestureRecognizers.Add(tap);

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(pan);
    }

    private static void OnIsToggledChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is HorizontalIndustrialToggle toggle)
        {
            toggle.AnimateToState((bool)newValue);
        }
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (!IsInteractive) return;
        IsToggled = !IsToggled;
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!IsInteractive) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartX = Handle.TranslationX;
                _isDragging = true;
                break;

            case GestureStatus.Running:
                if (!_isDragging) break;
                var newX = _panStartX + e.TotalX;
                newX = Math.Max(0, Math.Min(HandleTravel, newX));
                Handle.TranslationX = newX;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (!_isDragging) break;
                _isDragging = false;
                var midPoint = HandleTravel / 2;
                IsToggled = Handle.TranslationX > midPoint;
                break;
        }
    }

    private void AnimateToState(bool isOn)
    {
        var targetX = isOn ? HandleTravel : 0;
        var ledColor = isOn ? LedOnColor : LedOffColor;

        Handle.Animate("handleSlide",
            new Animation(v => Handle.TranslationX = v, Handle.TranslationX, targetX, Easing.SpringOut),
            length: 300);

        LedBar.BackgroundColor = ledColor;

        if (isOn)
        {
            LedGlow.Brush = new SolidColorBrush(LedOnColor);
            LedGlow.Radius = 8;
            LedGlow.Opacity = 0.8f;
        }
        else
        {
            LedGlow.Brush = new SolidColorBrush(Colors.Transparent);
            LedGlow.Radius = 0;
            LedGlow.Opacity = 0;
        }
    }
}
