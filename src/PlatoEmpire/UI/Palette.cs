using System.Windows.Media;
using PlatoEmpire.Game;

namespace PlatoEmpire.UI;

/// <summary>PLATO plasma-panel orange, plus a tint per empire so ships can be told apart.</summary>
public static class Palette
{
    public static readonly Color Plasma = Color.FromRgb(0xFF, 0x9A, 0x1F);
    public static readonly Brush PlasmaBrush = Frozen(Plasma);
    public static readonly Brush PlasmaDim = Frozen(Color.FromRgb(0x7A, 0x4A, 0x10));
    public static readonly Brush PlasmaFaint = Frozen(Color.FromRgb(0x2A, 0x1A, 0x06));

    static readonly Dictionary<Team, Brush> Brushes = [];
    static readonly Dictionary<(Team, double), Pen> Pens = [];

    public static Color TeamColor(Team t) => t switch
    {
        Team.Federation => Color.FromRgb(0xFF, 0xD2, 0x4A),
        Team.Klingon => Color.FromRgb(0xFF, 0x4B, 0x3E),
        Team.Romulan => Color.FromRgb(0x52, 0xE3, 0x6A),
        Team.Orion => Color.FromRgb(0x4C, 0xC9, 0xFF),
        _ => Color.FromRgb(0x8C, 0x8C, 0x8C),
    };

    public static Brush TeamBrush(Team t)
    {
        if (!Brushes.TryGetValue(t, out var b)) Brushes[t] = b = Frozen(TeamColor(t));
        return b;
    }

    public static Pen TeamPen(Team t, double thickness = 1.5)
    {
        if (!Pens.TryGetValue((t, thickness), out var p))
        {
            p = new Pen(TeamBrush(t), thickness);
            p.Freeze();
            Pens[(t, thickness)] = p;
        }
        return p;
    }

    public static Brush MessageBrush(Team? t) => t is { } team ? TeamBrush(team) : PlasmaBrush;

    public static Brush Frozen(Color c, double opacity = 1)
    {
        var b = new SolidColorBrush(c) { Opacity = opacity };
        b.Freeze();
        return b;
    }

    public static Pen FrozenPen(Brush b, double thickness, DashStyle? dash = null)
    {
        var p = new Pen(b, thickness);
        if (dash != null) p.DashStyle = dash;
        p.Freeze();
        return p;
    }
}
