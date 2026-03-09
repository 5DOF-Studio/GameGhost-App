namespace GaimerDesktop.Controls;

/// <summary>
/// Three concentric spinning rings — orbital loader inspired by 21st.dev.
/// Outer ring: clockwise 1s. Middle ring: counter-clockwise 1.5s. Inner ring: clockwise 0.8s.
/// </summary>
public class OrbitalLoader : ContentView
{
    private static readonly Color RingColor = Color.FromArgb("#4ea3ff");
    private static readonly Color TransparentColor = Colors.Transparent;

    public static readonly BindableProperty IsActiveProperty =
        BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(OrbitalLoader),
            true, propertyChanged: OnIsActiveChanged);

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(OrbitalLoader),
            null, propertyChanged: OnMessageChanged);

    public static readonly BindableProperty RingSizeProperty =
        BindableProperty.Create(nameof(RingSize), typeof(double), typeof(OrbitalLoader),
            64.0, propertyChanged: OnRingSizeChanged);

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public double RingSize
    {
        get => (double)GetValue(RingSizeProperty);
        set => SetValue(RingSizeProperty, value);
    }

    private readonly Border _outerRing;
    private readonly Border _middleRing;
    private readonly Border _innerRing;
    private readonly Label _messageLabel;
    private bool _animating;

    public OrbitalLoader()
    {
        var size = RingSize;

        _outerRing = CreateRing(size, size, 2);
        _middleRing = CreateRing(size - 16, size - 16, 2);
        _innerRing = CreateRing(size - 32, size - 32, 2);

        var ringContainer = new Grid
        {
            WidthRequest = size,
            HeightRequest = size,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { _outerRing, _middleRing, _innerRing }
        };

        _messageLabel = new Label
        {
            FontFamily = "RajdhaniSemiBold",
            FontSize = 14,
            TextColor = Color.FromArgb("#aaaaaa"),
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = !string.IsNullOrEmpty(Message)
        };

        var stack = new VerticalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { ringContainer, _messageLabel }
        };

        Content = stack;

        if (IsActive)
            StartAnimations();
    }

    private Border CreateRing(double width, double height, double strokeThickness)
    {
        // Use a partial border effect: the ring is a rounded border with a
        // gradient stroke that shows only a quarter arc (top portion visible).
        var ring = new Border
        {
            WidthRequest = width,
            HeightRequest = height,
            BackgroundColor = TransparentColor,
            Stroke = RingColor,
            StrokeThickness = strokeThickness,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Opacity = 0.3,
            StrokeDashArray = new DoubleCollection { 3, 5 },
        };
        ring.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
        {
            CornerRadius = new CornerRadius(width / 2)
        };

        // Add a visible "head" arc overlay
        var head = new Border
        {
            WidthRequest = width,
            HeightRequest = height,
            BackgroundColor = TransparentColor,
            Stroke = RingColor,
            StrokeThickness = strokeThickness,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            StrokeDashArray = new DoubleCollection { 1.5, 6.5 },
        };
        head.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
        {
            CornerRadius = new CornerRadius(width / 2)
        };

        // Wrap both in a grid so the head overlays the dim ring
        var container = new Border
        {
            WidthRequest = width,
            HeightRequest = height,
            BackgroundColor = TransparentColor,
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new Grid { Children = { ring, head } }
        };

        return container;
    }

    private void StartAnimations()
    {
        if (_animating) return;
        _animating = true;

        // Outer: clockwise, 1s
        var outerAnim = new Animation(v => _outerRing.Rotation = v, 0, 360);
        outerAnim.Commit(this, "OuterRing", length: 1000, repeat: () => true, easing: Easing.Linear);

        // Middle: counter-clockwise, 1.5s
        var middleAnim = new Animation(v => _middleRing.Rotation = v, 0, -360);
        middleAnim.Commit(this, "MiddleRing", length: 1500, repeat: () => true, easing: Easing.Linear);

        // Inner: clockwise, 0.8s
        var innerAnim = new Animation(v => _innerRing.Rotation = v, 0, 360);
        innerAnim.Commit(this, "InnerRing", length: 800, repeat: () => true, easing: Easing.Linear);
    }

    private void StopAnimations()
    {
        if (!_animating) return;
        _animating = false;

        this.AbortAnimation("OuterRing");
        this.AbortAnimation("MiddleRing");
        this.AbortAnimation("InnerRing");
    }

    private static void OnIsActiveChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var loader = (OrbitalLoader)bindable;
        if (newValue is true)
            loader.StartAnimations();
        else
            loader.StopAnimations();
    }

    private static void OnMessageChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var loader = (OrbitalLoader)bindable;
        var text = newValue as string;
        loader._messageLabel.Text = text;
        loader._messageLabel.IsVisible = !string.IsNullOrEmpty(text);
    }

    private static void OnRingSizeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        // Rebuild would be needed for size changes — typically set once at construction
    }
}
