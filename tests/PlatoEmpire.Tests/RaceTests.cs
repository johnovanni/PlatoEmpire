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

    [Theory]
    [InlineData(Team.Federation)]
    [InlineData(Team.Klingon)]
    [InlineData(Team.Romulan)]
    [InlineData(Team.Orion)]
    public void Each_race_has_enough_distinct_names_for_the_largest_possible_fleet(Team team)
    {
        // The largest fleet: the most ships setup allows (values beyond the limits are clamped), holding every world.
        var w = TestWorld.Quiet(s =>
        {
            s.Opponents = 3; s.ShipsPerEmpire = 1000; s.AlliedBots = 1000; s.FleetRule = FleetRule.ScaleWithWorlds;
        });
        foreach (var p in w.Planets) p.Owner = team;
        int largestFleet = w.FleetCap(team);

        var names = RaceStats.For(team).ShipNames;
        Assert.Equal(39, largestFleet);
        Assert.True(names.Length >= largestFleet, $"{names.Length} names for a fleet of up to {largestFleet}");
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Theory]
    [InlineData(1, "Enterprise")]
    [InlineData(39, "Venture")]
    [InlineData(40, "Enterprise II")]
    [InlineData(41, "Excalibur II")]
    [InlineData(78, "Venture II")]
    [InlineData(79, "Enterprise III")]
    [InlineData(118, "Enterprise IV")]
    public void Names_are_reused_with_Roman_numerals_once_the_list_runs_out(int shipNumber, string expected) =>
        Assert.Equal(expected, RaceStats.For(Team.Federation).ShipName(shipNumber));

    [Theory]
    [InlineData(1, "I")]
    [InlineData(4, "IV")]
    [InlineData(9, "IX")]
    [InlineData(14, "XIV")]
    [InlineData(40, "XL")]
    [InlineData(1994, "MCMXCIV")]
    public void Roman_numerals_are_formed_correctly(int value, string expected) =>
        Assert.Equal(expected, RaceStats.ToRoman(value));

    [Fact]
    public void Every_ship_name_in_a_huge_fleet_is_unique()
    {
        var race = RaceStats.For(Team.Romulan);
        var names = Enumerable.Range(1, race.ShipNames.Length * 5).Select(race.ShipName).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void A_fleet_grown_to_twenty_ships_has_no_repeated_names()
    {
        // Conquest is off so holding nearly every world doesn't end the game before the fleet is built.
        var w = TestWorld.Quiet(s => { s.FleetRule = FleetRule.ScaleWithWorlds; s.ShipsPerEmpire = 5; s.ConquestPercent = 0; });
        var rival = TestWorld.Rival(w);
        foreach (var p in w.Planets) p.Owner = p == w.Planets[0] ? w.PlayerTeam : rival;   // limit 23, above 20

        for (int i = 0; i < GameWorld.ShipyardTicks * 15; i++)
        {
            w.Step();
            foreach (var s in w.Ships) s.Brain = null;   // keep the new ships idle
        }

        var names = w.Ships.Where(s => s.Team == rival).Select(s => s.Name).ToList();
        Assert.Equal(20, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Klingon_home_world_is_Qo_noS()
    {
        var w = TestWorld.Quiet(s => s.Opponents = 3);
        Assert.Equal("Qo'noS", w.Planets.Single(p => p.IsHome && p.Origin == Team.Klingon).Name);
    }

    [Fact]
    public void Federation_and_Klingons_are_evenly_matched_in_speed_and_firepower()
    {
        Assert.Equal(Fed.MaxWarp, Kli.MaxWarp);
        Assert.Equal(Fed.PhaserDamage, Kli.PhaserDamage);
        Assert.Equal(Fed.TorpDamage, Kli.TorpDamage);
    }
}
