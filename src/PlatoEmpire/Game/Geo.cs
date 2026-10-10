using System.Windows;

namespace PlatoEmpire.Game;

/// <summary>
/// Angle helpers. Courses use compass convention: 0 = up (north), increasing clockwise,
/// matching screen coordinates where +Y points down.
/// </summary>
public static class Geo
{
    public static Vector Dir(double deg)
    {
        double r = deg * Math.PI / 180;
        return new Vector(Math.Sin(r), -Math.Cos(r));
    }

    public static double AngleOf(Vector v) => Norm(Math.Atan2(v.X, -v.Y) * 180 / Math.PI);

    public static double Norm(double deg)
    {
        deg %= 360;
        return deg < 0 ? deg + 360 : deg;
    }

    /// <summary>Signed shortest rotation from <paramref name="from"/> to <paramref name="to"/>, in (-180, 180].</summary>
    public static double Diff(double from, double to)
    {
        double d = Norm(to - from);
        return d > 180 ? d - 360 : d;
    }

    /// <summary>Course a projectile of <paramref name="speed"/> must take to intercept a target moving at constant velocity.</summary>
    public static double LeadAngle(Vector shooter, Vector targetPos, Vector targetVel, double speed)
    {
        var r = targetPos - shooter;
        double a = targetVel * targetVel - speed * speed;
        double b = 2 * (r * targetVel);
        double c = r * r;
        double t;
        if (Math.Abs(a) < 1e-6)
        {
            if (Math.Abs(b) < 1e-6) return AngleOf(r);
            t = -c / b;
        }
        else
        {
            double disc = b * b - 4 * a * c;
            if (disc < 0) return AngleOf(r);
            double sq = Math.Sqrt(disc);
            double t1 = (-b - sq) / (2 * a), t2 = (-b + sq) / (2 * a);
            t = t1 > 0 && t2 > 0 ? Math.Min(t1, t2) : Math.Max(t1, t2);
        }
        return t > 0 ? AngleOf(r + targetVel * t) : AngleOf(r);
    }

    public static double Gaussian(Random rng)
    {
        double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}
