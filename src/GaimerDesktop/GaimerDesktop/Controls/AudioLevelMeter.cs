namespace GaimerDesktop.Controls;

public class AudioLevelMeter : ContentView
{
    private const int DefaultLedCount = 12;

    // VU meter color thresholds (green -> yellow -> red)
    private static readonly Color LedGreen = Color.FromArgb("#22c55e");
    private static readonly Color LedYellow = Color.FromArgb("#f2a900");
    private static readonly Color LedRed = Color.FromArgb("#ef4444");
    private static readonly Color LedOff = Color.FromArgb("#1a1225");

    public static readonly BindableProperty LevelProperty =
        BindableProperty.Create(nameof(Level), typeof(float), typeof(AudioLevelMeter),
            0f, propertyChanged: OnLevelChanged);

    public static readonly BindableProperty LedCountProperty =
        BindableProperty.Create(nameof(LedCount), typeof(int), typeof(AudioLevelMeter),
            DefaultLedCount, propertyChanged: OnLedCountChanged);

    public static readonly BindableProperty IsConnectedProperty =
        BindableProperty.Create(nameof(IsConnected), typeof(bool), typeof(AudioLevelMeter),
            false, propertyChanged: OnIsConnectedChanged);

    public float Level
    {
        get => (float)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public int LedCount
    {
        get => (int)GetValue(LedCountProperty);
        set => SetValue(LedCountProperty, value);
    }

    public bool IsConnected
    {
        get => (bool)GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    private readonly List<Border> _leds = new();
    private readonly HorizontalStackLayout _container;
    private bool _isPulsing;
    private CancellationTokenSource? _pulseCts;

    public AudioLevelMeter()
    {
        _container = new HorizontalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        Content = _container;
        BuildLeds();
    }

    private void BuildLeds()
    {
        _container.Children.Clear();
        _leds.Clear();

        for (int i = 0; i < LedCount; i++)
        {
            var led = new Border
            {
                WidthRequest = 6,
                HeightRequest = 24,
                BackgroundColor = LedOff,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 2 },
                Shadow = new Shadow
                {
                    Brush = new SolidColorBrush(Colors.Transparent),
                    Offset = new Point(0, 0),
                    Radius = 0,
                    Opacity = 0
                }
            };
            _leds.Add(led);
            _container.Children.Add(led);
        }
    }

    private static void OnLedCountChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AudioLevelMeter meter)
        {
            meter.BuildLeds();
            meter.UpdateLeds(meter.Level);
        }
    }

    private static void OnLevelChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AudioLevelMeter meter)
        {
            var level = (float)newValue;
            if (level > 0.01f)
            {
                meter.StopIdlePulse();
                meter.UpdateLeds(level);
            }
            else if (meter.IsConnected)
            {
                meter.UpdateLeds(0f);
                meter.StartIdlePulse();
            }
            else
            {
                meter.UpdateLeds(0f);
            }
        }
    }

    private static void OnIsConnectedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AudioLevelMeter meter)
        {
            var connected = (bool)newValue;
            if (connected && meter.Level <= 0.01f)
            {
                meter.StartIdlePulse();
            }
            else
            {
                meter.StopIdlePulse();
                if (!connected)
                {
                    meter.UpdateLeds(0f);
                }
            }
        }
    }

    private void StartIdlePulse()
    {
        if (_isPulsing) return;
        _isPulsing = true;
        _pulseCts = new CancellationTokenSource();
        var token = _pulseCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                // Fade in
                for (int step = 0; step <= 10 && !token.IsCancellationRequested; step++)
                {
                    var opacity = step / 10f;
                    MainThread.BeginInvokeOnMainThread(() => SetFirstLedPulse(opacity));
                    await Task.Delay(60, token).ConfigureAwait(false);
                }

                // Fade out
                for (int step = 10; step >= 0 && !token.IsCancellationRequested; step--)
                {
                    var opacity = step / 10f;
                    MainThread.BeginInvokeOnMainThread(() => SetFirstLedPulse(opacity));
                    await Task.Delay(60, token).ConfigureAwait(false);
                }

                // Brief pause between pulses
                if (!token.IsCancellationRequested)
                    await Task.Delay(300, token).ConfigureAwait(false);
            }
        }, token);
    }

    private void StopIdlePulse()
    {
        if (!_isPulsing) return;
        _isPulsing = false;
        _pulseCts?.Cancel();
        _pulseCts?.Dispose();
        _pulseCts = null;

        // Reset first LED to off state
        if (_leds.Count > 0)
        {
            SetLedOff(_leds[0]);
        }
    }

    private void SetFirstLedPulse(float opacity)
    {
        if (_leds.Count == 0) return;
        var led = _leds[0];
        var color = LedGreen.WithAlpha((float)Math.Clamp(opacity, 0.15f, 1f));
        led.BackgroundColor = color;
        led.Shadow = new Shadow
        {
            Brush = new SolidColorBrush(LedGreen),
            Offset = new Point(0, 0),
            Radius = 4,
            Opacity = opacity * 0.7f
        };
    }

    private void UpdateLeds(float level)
    {
        var clamped = Math.Clamp(level, 0f, 1f);
        var litCount = (int)(clamped * _leds.Count);

        for (int i = 0; i < _leds.Count; i++)
        {
            var led = _leds[i];
            if (i < litCount)
            {
                var color = GetLedColor(i, _leds.Count);
                led.BackgroundColor = color;
                led.Shadow = new Shadow
                {
                    Brush = new SolidColorBrush(color),
                    Offset = new Point(0, 0),
                    Radius = 4,
                    Opacity = 0.7f
                };
            }
            else
            {
                SetLedOff(led);
            }
        }
    }

    private static void SetLedOff(Border led)
    {
        led.BackgroundColor = LedOff;
        led.Shadow = new Shadow
        {
            Brush = new SolidColorBrush(Colors.Transparent),
            Offset = new Point(0, 0),
            Radius = 0,
            Opacity = 0
        };
    }

    private static Color GetLedColor(int index, int total)
    {
        // Green for first 60%, yellow for next 25%, red for top 15%
        var ratio = (float)index / total;
        if (ratio < 0.6f) return LedGreen;
        if (ratio < 0.85f) return LedYellow;
        return LedRed;
    }
}
