using System.Windows;
using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

public class GeoTests
{
    const double Eps = 1e-9;

    [Fact]
    public void Course_zero_points_up_the_screen()
    {
        var d = Geo.Dir(0);
        Assert.Equal(0, d.X, Eps);
        Assert.Equal(-1, d.Y, Eps);
    }

    [Fact]
    public void Course_ninety_points_right()
    {
        var d = Geo.Dir(90);
        Assert.Equal(1, d.X, Eps);
        Assert.Equal(0, d.Y, Eps);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(271.5)]
    [InlineData(359)]
    public void AngleOf_inverts_Dir(double deg) => Assert.Equal(deg, Geo.AngleOf(Geo.Dir(deg) * 50), 1e-6);

    [Theory]
    [InlineData(-90, 270)]
    [InlineData(720, 0)]
    [InlineData(365, 5)]
    [InlineData(359.5, 359.5)]
    public void Norm_wraps_into_0_360(double input, double expected) => Assert.Equal(expected, Geo.Norm(input), Eps);

    [Theory]
    [InlineData(350, 10, 20)]
    [InlineData(10, 350, -20)]
    [InlineData(0, 180, 180)]
    [InlineData(90, 90, 0)]
    public void Diff_takes_the_short_way_round(double from, double to, double expected) =>
        Assert.Equal(expected, Geo.Diff(from, to), Eps);

    [Fact]
    public void LeadAngle_for_stationary_target_is_direct_bearing()
    {
        var angle = Geo.LeadAngle(new Vector(0, 0), new Vector(0, -500), new Vector(0, 0), 30);
        Assert.Equal(0, angle, 1e-6);
    }

    [Fact]
    public void LeadAngle_intercepts_a_moving_target()
    {
        var shooter = new Vector(0, 0);
        var target = new Vector(1000, 0);
        var vel = new Vector(0, 10);
        const double speed = 30;
        var dir = Geo.Dir(Geo.LeadAngle(shooter, target, vel, speed));

        double closest = double.MaxValue;
        for (double t = 0; t < 100; t += 0.01)
            closest = Math.Min(closest, (shooter + dir * speed * t - (target + vel * t)).Length);

        Assert.True(closest < 0.5, $"closest approach was {closest:0.00}");
    }

    [Fact]
    public void LeadAngle_falls_back_to_bearing_when_target_is_too_fast()
    {
        var angle = Geo.LeadAngle(new Vector(0, 0), new Vector(100, 0), new Vector(100, 0), 10);
        Assert.Equal(90, angle, 1e-6);
    }
}
