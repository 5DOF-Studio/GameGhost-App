using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace WitnessDesktop.Controls;

public class PlusPatternBackground : SKCanvasView
{
    public static readonly BindableProperty PlusSizeProperty =
        BindableProperty.Create(nameof(PlusSize), typeof(float), typeof(PlusPatternBackground), 60f, propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty PlusColorProperty =
        BindableProperty.Create(nameof(PlusColor), typeof(Color), typeof(PlusPatternBackground), Color.FromArgb("#fb3a5d"), propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty PatternBackgroundColorProperty =
        BindableProperty.Create(nameof(PatternBackgroundColor), typeof(Color), typeof(PlusPatternBackground), Colors.Transparent, propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty FadeProperty =
        BindableProperty.Create(nameof(Fade), typeof(bool), typeof(PlusPatternBackground), true, propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty PlusOpacityProperty =
        BindableProperty.Create(nameof(PlusOpacity), typeof(float), typeof(PlusPatternBackground), 0.4f, propertyChanged: OnPropertyChanged);

    public float PlusSize
    {
        get => (float)GetValue(PlusSizeProperty);
        set => SetValue(PlusSizeProperty, value);
    }

    public Color PlusColor
    {
        get => (Color)GetValue(PlusColorProperty);
        set => SetValue(PlusColorProperty, value);
    }

    public Color PatternBackgroundColor
    {
        get => (Color)GetValue(PatternBackgroundColorProperty);
        set => SetValue(PatternBackgroundColorProperty, value);
    }

    public bool Fade
    {
        get => (bool)GetValue(FadeProperty);
        set => SetValue(FadeProperty, value);
    }

    public float PlusOpacity
    {
        get => (float)GetValue(PlusOpacityProperty);
        set => SetValue(PlusOpacityProperty, value);
    }

    private static void OnPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((PlusPatternBackground)bindable).InvalidateSurface();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear();

        // Draw background color
        if (PatternBackgroundColor != Colors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = PatternBackgroundColor.ToSKColor(),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(info.Rect, bgPaint);
        }

        // Draw plus pattern
        DrawPlusPattern(canvas, info);

        // Apply radial fade if enabled
        if (Fade)
        {
            ApplyRadialFade(canvas, info);
        }
    }

    private void DrawPlusPattern(SKCanvas canvas, SKImageInfo info)
    {
        var plusColor = PlusColor.ToSKColor();
        plusColor = plusColor.WithAlpha((byte)(255 * PlusOpacity));

        using var paint = new SKPaint
        {
            Color = plusColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        float tileSize = PlusSize;
        float scale = tileSize / 60f;
        float plusThickness = 2f * scale;
        float plusArmLength = 4f * scale;

        // Plus positions within a 60x60 tile (from the SVG)
        var plusPositions = new[]
        {
            (36f, 34f),  // center-right area
            (36f, 4f),   // top-right area
            (6f, 34f),   // center-left area
            (6f, 4f)     // top-left area
        };

        int tilesX = (int)Math.Ceiling(info.Width / tileSize) + 1;
        int tilesY = (int)Math.Ceiling(info.Height / tileSize) + 1;

        for (int ty = 0; ty < tilesY; ty++)
        {
            for (int tx = 0; tx < tilesX; tx++)
            {
                float tileOffsetX = tx * tileSize;
                float tileOffsetY = ty * tileSize;

                foreach (var (px, py) in plusPositions)
                {
                    float centerX = tileOffsetX + (px * scale);
                    float centerY = tileOffsetY + (py * scale);

                    // Draw horizontal bar of plus
                    canvas.DrawRect(
                        centerX - plusArmLength - plusThickness / 2,
                        centerY - plusThickness / 2,
                        plusArmLength * 2 + plusThickness,
                        plusThickness,
                        paint);

                    // Draw vertical bar of plus
                    canvas.DrawRect(
                        centerX - plusThickness / 2,
                        centerY - plusArmLength - plusThickness / 2,
                        plusThickness,
                        plusArmLength * 2 + plusThickness,
                        paint);
                }
            }
        }
    }

    private void ApplyRadialFade(SKCanvas canvas, SKImageInfo info)
    {
        float centerX = info.Width / 2f;
        float centerY = info.Height / 2f;
        float radius = Math.Max(info.Width, info.Height) * 0.7f;

        using var fadePaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.DstIn
        };

        fadePaint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(centerX, centerY),
            radius,
            new SKColor[] { SKColors.White, SKColors.Transparent },
            new float[] { 0.1f, 0.9f },
            SKShaderTileMode.Clamp);

        canvas.DrawRect(info.Rect, fadePaint);
    }
}
