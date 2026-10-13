using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

/// <summary>
/// Race balance documented for the original PLATO Empire IV: "Orion ships have the weakest weapons but are fast",
/// "Roms have the strongest weapons, but are the slowest", Federation and Kazari (Klingon) are medium in both.
/// See docs/original-vs-recreation.md.
/// </summary>
public class RaceTests
{
    static RaceStats Fed => RaceStats.For(Team.Federation);
    static RaceStats Kli => RaceStats.For(Team.Klingon);
    static RaceStats Rom => RaceStats.For(Team.Romulan);
    static RaceStats Ori => RaceStats.For(Team.Orion);

    static IEnumerable<RaceStats> Others(RaceStats r) => RaceStats.Empires.Select(RaceStats.For).Where(o => o != r);

    [Fact]
    public void Romulans_have_the_strongest_weapons()
    {
        Assert.All(Others(Rom), o =>
        {
            Assert.True(Rom.PhaserDamage > o.PhaserDamage, $"Romulan phasers vs {o.Name}");
            Assert.True(Rom.TorpDamage > o.TorpDamage, $"Romulan torpedoes vs {o.Name}");
        });
    }

    [Fact]
    public void Romulans_are_the_slowest() =>
        Assert.All(Others(Rom), o => Assert.True(Rom.MaxWarp < o.MaxWarp, $"Romulan speed vs {o.Name}"));

    [Fact]
    public void Orions_have_the_weakest_weapons()
    {
        Assert.All(Others(Ori), o =>
        {
            Assert.True(Ori.PhaserDamage < o.PhaserDamage, $"Orion phasers vs {o.Name}");
            Assert.True(Ori.TorpDamage < o.TorpDamage, $"Orion torpedoes vs {o.Name}");
        });
    }

    [Fact]
    public void Orions_are_the_fastest() =>
        Assert.All(Others(Ori), o => Assert.True(Ori.MaxWarp > o.MaxWarp, $"Orion speed vs {o.Name}"));

    [Fact]
    public void Federation_and_Klingons_are_evenly_matched_in_speed_and_firepower()
    {
        Assert.Equal(Fed.MaxWarp, Kli.MaxWarp);
        Assert.Equal(Fed.PhaserDamage, Kli.PhaserDamage);
        Assert.Equal(Fed.TorpDamage, Kli.TorpDamage);
    }
}
