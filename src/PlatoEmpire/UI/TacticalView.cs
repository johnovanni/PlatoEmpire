using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PlatoEmpire.Game;

namespace PlatoEmpire.UI;

/// <summary>The main screen: a window onto the galaxy centred on the player's ship.</summary>
public sealed class TacticalView : FrameworkElement
{
    static readonly Typeface Face = new("Consolas");
    static readonly Brush StarBrush = Palette.Frozen(Color.FromRgb(0x5A, 0x4A, 0x38));
    static readonly Pen BorderPen = Palette.FrozenPen(Palette.PlasmaDim, 2, DashStyles.Dash);
    static readonly Pen CoursePen = Palette.FrozenPen(Palette.PlasmaDim, 1, DashStyles.Dot);
    static readonly Pen ReticlePen = Palette.FrozenPen(Palette.PlasmaBrush, 1);
    static readonly Pen OrbitPen = Palette.FrozenPen(Palette.PlasmaFaint, 1, DashStyles.Dot);

    readonly List<Vector> stars = [];
    Vector center = new(GameWorld.GalaxySize / 2, GameWorld.GalaxySize / 2);
    double dpi = 1;

    public GameWorld? World { get; set; }
    public double ViewRadius { get; set; } = 1400;
    public Point? MouseScreen { get; set; }
    public bool Paused { get; set; }

    public TacticalView()
    {
        var rng = new Random(1973);
        for (int i = 0; i < 2600; i++)
            stars.Add(new Vector(rng.NextDouble() * 13000 - 1500, rng.NextDouble() * 13000 - 1500));
    }

    double Scale => Math.Max(1, Math.Min(ActualWidth, ActualHeight)) / 2 / ViewRadius;

    public Point WorldToScreen(Vector v)
    {
        double s = Scale;
        return new Point(ActualWidth / 2 + (v.X - center.X) * s, ActualHeight / 2 + (v.Y - center.Y) * s);
    }

    public Vector ScreenToWorld(Point p)
    {
        double s = Scale;
        return new Vector(center.X + (p.X - ActualWidth / 2) / s, center.Y + (p.Y - ActualHeight / 2) / s);
    }

    public Rect VisibleWorldRect
    {
        get
        {
            double s = Scale;
            double hw = ActualWidth / 2 / s, hh = ActualHeight / 2 / s;
            return new Rect(center.X - hw, center.Y - hh, hw * 2, hh * 2);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(Brushes.Black, null, bounds);
        if (World is not { } world) return;

        dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var me = world.Player;
        if (me.Alive) center = me.Pos;
        double scale = Scale;
        var view = VisibleWorldRect;
        view.Inflate(200, 200);

        dc.PushClip(new RectangleGeometry(bounds));

        foreach (var s in stars)
            if (view.Contains(s.X, s.Y))
            {
                var p = WorldToScreen(s);
                dc.DrawRectangle(StarBrush, null, new Rect(p.X, p.Y, 1.5, 1.5));
            }

        dc.DrawRectangle(null, BorderPen, new Rect(WorldToScreen(new Vector(0, 0)), WorldToScreen(new Vector(GameWorld.GalaxySize, GameWorld.GalaxySize))));

        DrawPlanets(dc, world, view, scale);

        foreach (var t in world.Torpedoes)
        {
            if (!view.Contains(t.Pos.X, t.Pos.Y)) continue;
            var p = WorldToScreen(t.Pos);
            var tail = WorldToScreen(t.Pos - t.Vel * 1.5);
            dc.DrawLine(Palette.TeamPen(t.Team, 1), tail, p);
            dc.DrawEllipse(Palette.TeamBrush(t.Team), null, p, 2.5, 2.5);
        }

        foreach (var b in world.Beams)
        {
            var brush = Palette.Frozen(Palette.TeamColor(b.Team), (double)b.Life / b.MaxLife);
            var pen = Palette.FrozenPen(brush, b.FromPlanet ? 1 : 2, b.FromPlanet ? DashStyles.Dash : null);
            dc.DrawLine(pen, WorldToScreen(b.From), WorldToScreen(b.To));
        }

        foreach (var s in world.Ships)
        {
            if (!s.Alive || !view.Contains(s.Pos.X, s.Pos.Y)) continue;
            if (!world.CanSee(me, s) && s != me) continue;
            DrawShip(dc, world, s, scale);
        }

        foreach (var e in world.Explosions)
        {
            if (!view.Contains(e.Pos.X, e.Pos.Y)) continue;
            double f = (double)e.Age / e.MaxAge;
            var p = WorldToScreen(e.Pos);
            var brush = Palette.Frozen(e.Age % 4 < 2 ? Colors.White : Palette.TeamColor(e.Team), 1 - f);
            var pen = Palette.FrozenPen(brush, 2);
            double r = Math.Max(3, e.Radius * scale * (0.2 + f));
            dc.DrawEllipse(null, pen, p, r, r);
            if (e.MaxAge > 20)
                for (int i = 0; i < 8; i++)
                {
                    var dir = Geo.Dir(i * 45 + e.Age * 3);
                    dc.DrawLine(pen, p + dir * (r * 0.5), p + dir * (r * 1.2));
                }
        }

        if (me.Alive)
        {
            var sp = WorldToScreen(me.Pos);
            dc.DrawLine(CoursePen, sp, sp + Geo.Dir(me.DesiredHeading) * 70);
        }

        if (MouseScreen is { } m)
        {
            dc.DrawLine(ReticlePen, new Point(m.X - 8, m.Y), new Point(m.X - 3, m.Y));
            dc.DrawLine(ReticlePen, new Point(m.X + 3, m.Y), new Point(m.X + 8, m.Y));
            dc.DrawLine(ReticlePen, new Point(m.X, m.Y - 8), new Point(m.X, m.Y - 3));
            dc.DrawLine(ReticlePen, new Point(m.X, m.Y + 3), new Point(m.X, m.Y + 8));
        }

        Text(dc, $"SECTOR {(int)(center.X / 1000)}-{(int)(center.Y / 1000)}   POS {center.X:0},{center.Y:0}   SCALE {ViewRadius:0}",
            new Point(8, 6), Palette.PlasmaDim, 12);
        Text(dc, "H = HELP   ENTER = COMMAND", new Point(8, ActualHeight - 20), Palette.PlasmaDim, 12);

        if (world.ConquestLeader is { } leader)
        {
            int secs = (int)Math.Ceiling(world.ConquestTicksRemaining / (double)GameWorld.TicksPerSecond);
            string banner = leader == world.PlayerTeam
                ? $"CONQUEST IN {secs}s - HOLD {world.ConquestPlanetsNeeded} WORLDS"
                : $"{RaceStats.For(leader).Plural.ToUpperInvariant()} CONQUER THE GALAXY IN {secs}s - RETAKE THEIR WORLDS!";
            Text(dc, banner, new Point(ActualWidth / 2, 28), Palette.TeamBrush(leader), 18, centered: true);
        }

        if (!me.Alive)
        {
            string msg = me.Stranded
                ? "SHIP DESTROYED - NO WORLDS LEFT TO RESPAWN"
                : $"SHIP DESTROYED - NEW COMMAND IN {Math.Ceiling(me.RespawnTimer / (double)GameWorld.TicksPerSecond)}";
            Text(dc, msg, new Point(ActualWidth / 2, ActualHeight / 2 - 60), Palette.PlasmaBrush, 22, centered: true);
        }
        if (Paused)
            Text(dc, "- PAUSED -   (ESC to resume)", new Point(ActualWidth / 2, ActualHeight / 2 - 100), Palette.PlasmaBrush, 24, centered: true);

        dc.Pop();
    }

    void DrawPlanets(DrawingContext dc, GameWorld world, Rect view, double scale)
    {
        foreach (var pl in world.Planets)
        {
            if (!view.Contains(pl.Pos.X, pl.Pos.Y)) continue;
            var p = WorldToScreen(pl.Pos);
            double r = Math.Max(6, 45 * scale);
            var pen = Palette.TeamPen(pl.Owner, 2);
            dc.DrawEllipse(null, OrbitPen, p, GameWorld.OrbitRadius * scale, GameWorld.OrbitRadius * scale);
            dc.DrawEllipse(Palette.Frozen(Palette.TeamColor(pl.Owner), 0.12), pen, p, r, r);
            if (pl.IsHome) dc.DrawEllipse(null, Palette.TeamPen(pl.Owner, 1), p, r + 4, r + 4);
            Text(dc, pl.Armies.ToString(), new Point(p.X, p.Y - 8), Palette.TeamBrush(pl.Owner), 13, centered: true);
            Text(dc, pl.Name.ToUpperInvariant(), new Point(p.X, p.Y + r + 4), Palette.TeamBrush(pl.Owner), 12, centered: true);
        }
    }

    /// <summary>Ship drawings below span about this many local units from their centre.</summary>
    const double GlyphExtent = 11;

    /// <summary>Smallest on-screen ship radius, in pixels, so ships stay recognisable when zoomed far out.</summary>
    public const double MinShipPixels = 5;

    /// <summary>
    /// On-screen radius of a ship: the true area a torpedo must reach to hit it, at the current zoom, so a shot that
    /// looks like a hit is one. Only when zoomed far out is it held at <see cref="MinShipPixels"/> to stay visible.
    /// </summary>
    public static double ShipPixelRadius(double scale) => Math.Max(MinShipPixels, GameWorld.TorpedoHitRadius * scale);

    void DrawShip(DrawingContext dc, GameWorld world, Ship s, double scale)
    {
        var c = WorldToScreen(s.Pos);
        double r = ShipPixelRadius(scale);
        double k = r / GlyphExtent;
        double h = s.Heading * Math.PI / 180, cos = Math.Cos(h), sin = Math.Sin(h);
        Point P(double x, double y) => new(c.X + (x * cos - y * sin) * k, c.Y + (x * sin + y * cos) * k);

        var pen = Palette.TeamPen(s.Team, s.IsPlayer ? 2 : 1.5);
        if (s.Cloaked) pen = Palette.FrozenPen(Palette.Frozen(Palette.TeamColor(s.Team), 0.35), 1.5);

        switch (s.Team)
        {
            case Team.Federation:
                dc.DrawEllipse(null, pen, P(0, -5), 5 * k, 5 * k);
                dc.DrawLine(pen, P(0, 0), P(0, 5));
                dc.DrawLine(pen, P(-6, 5), P(6, 5));
                dc.DrawLine(pen, P(-6, 2), P(-6, 11));
                dc.DrawLine(pen, P(6, 2), P(6, 11));
                break;
            case Team.Klingon:
                dc.DrawEllipse(null, pen, P(0, -10), 2.5 * k, 2.5 * k);
                dc.DrawLine(pen, P(0, -7.5), P(0, 0));
                Poly(dc, pen, false, P(-11, 8), P(-8, 2), P(0, 0), P(8, 2), P(11, 8));
                break;
            case Team.Romulan:
                Poly(dc, pen, true, P(0, -10), P(11, 4), P(5, 8), P(0, 3), P(-5, 8), P(-11, 4));
                break;
            default:
                Poly(dc, pen, true, P(0, -11), P(6, 0), P(0, 9), P(-6, 0));
                dc.DrawLine(pen, P(-6, 0), P(-9, 7));
                dc.DrawLine(pen, P(6, 0), P(9, 7));
                break;
        }

        if (s.ShieldsUp)
        {
            var sb = Palette.Frozen(Palette.TeamColor(s.Team), 0.25 + 0.6 * s.Shield / s.Stats.MaxShield);
            double shield = Math.Max(r * 1.45, r + 4);
            dc.DrawEllipse(null, Palette.FrozenPen(sb, 1), c, shield, shield);
        }

        // Label and hull bar sit a fixed gap outside the ship so they stay readable at any size.
        string label = s.Armies > 0 ? $"{s.Callsign} +{s.Armies}" : s.Callsign;
        Text(dc, label, new Point(c.X + r + 3, c.Y - r - 12), Palette.TeamBrush(s.Team), 11);

        if (s.HullFraction > 0.05 && s != world.Player)
        {
            double w = 22;
            var origin = new Point(c.X - w / 2, c.Y + r + 4);
            dc.DrawRectangle(Palette.PlasmaFaint, null, new Rect(origin, new Size(w, 2)));
            dc.DrawRectangle(Palette.TeamBrush(s.Team), null, new Rect(origin, new Size(w * (1 - s.HullFraction), 2)));
        }
    }

    static void Poly(DrawingContext dc, Pen pen, bool closed, params Point[] pts)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(pts[0], false, closed);
            ctx.PolyLineTo(pts[1..], true, true);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }

    void Text(DrawingContext dc, string s, Point p, Brush b, double size, bool centered = false)
    {
        var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, size, b, dpi);
        if (centered) p = new Point(p.X - ft.Width / 2, p.Y);
        dc.DrawText(ft, p);
    }
}
