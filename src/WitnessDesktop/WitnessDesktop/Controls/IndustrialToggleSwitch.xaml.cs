namespace WitnessDesktop.Controls;

public partial class IndustrialToggleSwitch : ContentView
{
    // Handle travel distance: rail height (60) - handle height (28) - padding (4) = 28px
    private const double HandleTravel = 28;

    public static readonly BindableProperty IsToggledProperty =
        BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(IndustrialToggleSwitch),
            false, BindingMode.TwoWay, propertyChanged: OnIsToggledChanged);

    public static readonly BindableProperty LedOnColorProperty =
        BindableProperty.Create(nameof(LedOnColor), typeof(Color), typeof(IndustrialToggleSwitch),
            Color.FromArgb("#f2a900"));

    public static readonly BindableProperty LedOffColorProperty =
        BindableProperty.Create(nameof(LedOffColor), typeof(Color), typeof(IndustrialToggleSwitch),
            Color.FromArgb("#3d1515"));

    public static readonly BindableProperty IsInteractiveProperty =
        BindableProperty.Create(nameof(IsInteractive), typeof(bool), typeof(IndustrialToggleSwitch),
            true);

    public static readonly BindableProperty IsPendingProperty =
        BindableProperty.Create(nameof(IsPending), typeof(bool), typeof(IndustrialToggleSwitch),
            false, propertyChanged: OnIsPendingChanged);

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

    public bool IsPending
    {
        get => (bool)GetValue(IsPendingProperty);
        set => SetValue(IsPendingProperty, value);
    }

    private double _panStartY;
    private bool _isDragging;

    public IndustrialToggleSwitch()
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
        if (bindable is IndustrialToggleSwitch toggle)
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
                _panStartY = Handle.TranslationY;
                _isDragging = true;
                break;

            case GestureStatus.Running:
                if (!_isDragging) break;
                var newY = _panStartY + e.TotalY;
                // Clamp within rail
                newY = Math.Max(0, Math.Min(HandleTravel, newY));
                Handle.TranslationY = newY;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (!_isDragging) break;
                _isDragging = false;
                // Snap to nearest state: top half = ON, bottom half = OFF
                var midPoint = HandleTravel / 2;
                IsToggled = Handle.TranslationY < midPoint;
                break;
        }
    }

    private static void OnIsPendingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is IndustrialToggleSwitch toggle)
        {
            if ((bool)newValue)
                toggle.StartPendingAnimation();
            else
                toggle.StopPendingAnimation();
        }
    }

    private void AnimateToState(bool isOn)
    {
        var targetY = isOn ? 0 : HandleTravel;

        // Animate handle
        Handle.Animate("handleSlide",
            new Animation(v => Handle.TranslationY = v, Handle.TranslationY, targetY, Easing.SpringOut),
            length: 300);

        // Skip LED update if pending animation is running — it owns the LED
        if (IsPending) return;

        UpdateLedVisual(isOn);
    }

    private void UpdateLedVisual(bool isOn)
    {
        var ledColor = isOn ? LedOnColor : LedOffColor;
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

    private void StartPendingAnimation()
    {
        // Pulse LED with the OnColor to signal async work in flight
        LedBar.BackgroundColor = LedOnColor;
        LedGlow.Brush = new SolidColorBrush(LedOnColor);
        LedGlow.Radius = 8;

        var pulse = new Animation(v => LedGlow.Opacity = (float)v, 0.3, 0.9);
        var fadeBack = new Animation(v => LedGlow.Opacity = (float)v, 0.9, 0.3);

        var combined = new Animation();
        combined.Add(0, 0.5, pulse);
        combined.Add(0.5, 1.0, fadeBack);

        LedBar.Animate("pendingPulse", combined, length: 800, repeat: () => IsPending);
    }

    private void StopPendingAnimation()
    {
        LedBar.AbortAnimation("pendingPulse");
        // Restore LED to match current toggle state
        UpdateLedVisual(IsToggled);
    }
}
