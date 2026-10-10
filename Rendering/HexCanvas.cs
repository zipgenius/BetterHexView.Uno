using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using System.Numerics;

namespace BetterHexViewer.Uno.Rendering;

/// <summary>The two template surfaces share Uno's Skia renderer and its GPU context.</summary>
public sealed class HexCanvas : SKCanvasElement
{
    internal event Action<HexCanvas, HexDrawEventArgs>? Draw;

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        using var session = new HexDrawingSession(canvas);
        Draw?.Invoke(this, new HexDrawEventArgs(session));
    }
}

internal sealed class HexDrawEventArgs(HexDrawingSession session) : EventArgs
{
    internal HexDrawingSession DrawingSession { get; } = session;
}

internal sealed class HexDrawingSession(SKCanvas canvas) : IDisposable
{
    private readonly SKPaint _paint = new() { IsAntialias = true };

    private SKPaint Paint(Color color, bool stroke = false, float width = 1)
    {
        _paint.Color = new SKColor(color.R, color.G, color.B, color.A);
        _paint.Style = stroke ? SKPaintStyle.Stroke : SKPaintStyle.Fill;
        _paint.StrokeWidth = width;
        return _paint;
    }

    internal void FillRectangle(float x, float y, float w, float h, Color color)
        => canvas.DrawRect(x, y, w, h, Paint(color));
    internal void DrawRectangle(float x, float y, float w, float h, Color color, float width = 1)
        => canvas.DrawRect(x, y, w, h, Paint(color, true, width));
    internal void FillRoundedRectangle(float x, float y, float w, float h, float rx, float ry, Color color)
        => canvas.DrawRoundRect(new SKRect(x, y, x + w, y + h), rx, ry, Paint(color));
    internal void DrawLine(float x1, float y1, float x2, float y2, Color color)
        => canvas.DrawLine(x1, y1, x2, y2, Paint(color, true));
    internal void DrawTextLayout(HexTextLayout layout, Vector2 position, Color color)
        => canvas.DrawText(layout.Text, position.X, position.Y - layout.Format.Font.Metrics.Ascent,
            layout.Format.Font, Paint(color));
    public void Dispose() => _paint.Dispose();
}

internal sealed class HexTextFormat : IDisposable
{
    internal string FontFamily { get; init; } = "Courier New";
    internal float FontSize { get; init; } = 13;
    internal FontWeight FontWeight { get; init; } = new() { Weight = 400 };
    internal bool Wrap { get; init; }
    private SKTypeface? _typeface;
    private SKFont? _font;

    internal SKFont Font
    {
        get
        {
            if (_font != null) return _font;
            var style = new SKFontStyle(FontWeight.Weight, (int)SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            _typeface = SKTypeface.FromFamilyName(FontFamily, style)
                ?? SKTypeface.FromFamilyName("monospace", style);
            _font = new SKFont(_typeface, FontSize)
            {
                Subpixel = true,
                Edging = SKFontEdging.Antialias,
                Hinting = SKFontHinting.Normal
            };
            return _font;
        }
    }

    public void Dispose()
    {
        _font?.Dispose();
        _typeface?.Dispose();
        _font = null;
        _typeface = null;
    }
}

internal sealed class HexTextLayout : IDisposable
{
    internal string Text { get; }
    internal HexTextFormat Format { get; }
    internal Rect LayoutBounds { get; }

    internal HexTextLayout(object? resource, string text, HexTextFormat format, float width, float height)
    {
        Text = text;
        Format = format;
        var metrics = format.Font.Metrics;
        LayoutBounds = new Rect(0, 0, format.Font.MeasureText(text), metrics.Descent - metrics.Ascent + metrics.Leading);
    }

    public void Dispose() { } // Fonts are cached by the control, not owned by each label.
}
