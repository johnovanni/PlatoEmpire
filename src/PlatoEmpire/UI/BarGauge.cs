using System.Windows;
using System.Windows.Media;

namespace PlatoEmpire.UI;

/// <summary>A flat plasma-style bar meter.</summary>
public sealed class BarGauge : FrameworkElement
{
    static readonly Pen Outline = Palette.FrozenPen(Palette.PlasmaDim, 1);
    double value;
    Brush fill = Palette.PlasmaBrush;

    public double Value
    {
        get => value;
        set
        {
            value = Math.Clamp(value, 0, 1);
            if (Math.Abs(value - this.value) < 0.002) return;
            this.value = value;
            InvalidateVisual();
        }
    }

    public Brush Fill
    {
        get => fill;
        set
        {
            if (fill == value) return;
            fill = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var r = new Rect(0.5, 0.5, Math.Max(0, ActualWidth - 1), Math.Max(0, ActualHeight - 1));
        dc.DrawRectangle(Brushes.Black, Outline, r);
        if (value > 0)
            dc.DrawRectangle(fill, null, new Rect(2, 2, Math.Max(0, (ActualWidth - 4) * value), Math.Max(0, ActualHeight - 4)));
    }
}
