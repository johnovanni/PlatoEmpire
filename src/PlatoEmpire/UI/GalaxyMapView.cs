using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PlatoEmpire.Game;

namespace PlatoEmpire.UI;

/// <summary>Long-range scanner showing the whole galaxy. Click to set course.</summary>
public sealed class GalaxyMapView : FrameworkElement
{
    static readonly Typeface Face = new("Consolas");
    static readonly Pen BorderPen = Palette.FrozenPen(Palette.PlasmaDim, 1);
    static readonly Pen GridPen = Palette.FrozenPen(Palette.PlasmaFaint, 1);
    static readonly Pen ViewPen = Palette.FrozenPen(Palette.PlasmaDim, 1, DashStyles.Dash);
    static readonly Pen PlayerPen = Palette.FrozenPen(Brushes.White, 1.5);

    public GameWorld? World { get; set; }
    public Rect ViewRect { get; set; }
    public event Action<Vector>? MapClicked;

    public GalaxyMapView() => Cursor = Cursors.Cross;

    (double scale, double ox, double oy) Layout()
    {
        double s = Math.Min(ActualWidth, ActualHeight) / GameWorld.GalaxySize;
        return (s, (ActualWidth - GameWorld.GalaxySize * s) / 2, (ActualHeight - GameWorld.GalaxySize * s) / 2);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var (s, ox, oy) = Layout();
        var p = e.GetPosition(this);
        MapClicked?.Invoke(new Vector((p.X - ox) / s, (p.Y - oy) / s));
        e.Handled = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (World is not { } world) return;
        var (s, ox, oy) = Layout();
        Point M(Vector v) => new(ox + v.X * s, oy + v.Y * s);
        double size = GameWorld.GalaxySize;

        for (int i = 1; i < 10; i++)
        {
            double g = size * i / 10;
            dc.DrawLine(GridPen, M(new Vector(g, 0)), M(new Vector(g, size)));
            dc.DrawLine(GridPen, M(new Vector(0, g)), M(new Vector(size, g)));
        }
        dc.DrawRectangle(null, BorderPen, new Rect(M(new Vector(0, 0)), M(new Vector(size, size))));

        var vr = ViewRect;
        if (!vr.IsEmpty)
            dc.DrawRectangle(null, ViewPen, new Rect(M(new Vector(vr.Left, vr.Top)), M(new Vector(vr.Right, vr.Bottom))));

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var pl in world.Planets)
        {
            var p = M(pl.Pos);
            double r = pl.IsHome ? 5.5 : 4;
            dc.DrawEllipse(Palette.TeamBrush(pl.Owner), null, p, r, r);
            var ft = new FormattedText(pl.Name[..Math.Min(3, pl.Name.Length)].ToUpperInvariant(), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Face, 9, Palette.TeamBrush(pl.Owner), dpi);
            dc.DrawText(ft, new Point(p.X - ft.Width / 2, p.Y + r + 1));
        }

        var me = world.Player;
        foreach (var sh in world.Ships)
        {
            if (!sh.Alive || sh == me) continue;
            if (sh.Team != me.Team && !world.CanSee(me, sh)) continue;
            var p = M(sh.Pos);
            dc.DrawRectangle(Palette.TeamBrush(sh.Team), null, new Rect(p.X - 1.5, p.Y - 1.5, 3, 3));
        }
        if (me.Alive)
        {
            var p = M(me.Pos);
            dc.DrawEllipse(null, PlayerPen, p, 4, 4);
        }
    }
}
