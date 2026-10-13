using PlatoEmpire.Game;
using PlatoEmpire.UI;

namespace PlatoEmpire.Tests;

public class RenderingTests
{
    [Theory]
    [InlineData(0.25)]    // default zoom
    [InlineData(0.583)]   // fully zoomed in
    [InlineData(0.4)]
    public void Ships_are_drawn_the_size_of_the_area_a_torpedo_must_reach(double scale) =>
        Assert.Equal(GameWorld.TorpedoHitRadius * scale, TacticalView.ShipPixelRadius(scale), 1e-9);

    [Theory]
    [InlineData(0.0875)]  // fully zoomed out
    [InlineData(0.15)]
    public void Ships_never_shrink_below_the_minimum_visible_size(double scale) =>
        Assert.Equal(TacticalView.MinShipPixels, TacticalView.ShipPixelRadius(scale), 1e-9);

    [Fact]
    public void Drawn_size_grows_with_zoom()
    {
        Assert.True(TacticalView.ShipPixelRadius(0.5) > TacticalView.ShipPixelRadius(0.25));
        Assert.Equal(2, TacticalView.ShipPixelRadius(0.5) / TacticalView.ShipPixelRadius(0.25), 1e-9);
    }
}
