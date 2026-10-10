using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

public class SetupTests
{
    [Fact]
    public void Galaxy_has_five_worlds_per_empire_plus_four_independents()
    {
        var w = TestWorld.Create(s => s.Opponents = 3);

        Assert.Equal(24, w.Planets.Count);
        foreach (var team in RaceStats.Empires)
        {
            Assert.Equal(5, w.Planets.Count(p => p.Origin == team));
            Assert.Single(w.Planets, p => p.Origin == team && p.IsHome);
        }
        Assert.Equal(4, w.Planets.Count(p => p.Origin == Team.Neutral));
    }

    [Fact]
    public void Empires_not_in_play_leave_their_worlds_independent()
    {
        var w = TestWorld.Create(s => s.Opponents = 1);

        Assert.Equal(2, w.ActiveTeams.Count);
        foreach (var p in w.Planets)
            Assert.Equal(w.ActiveTeams.Contains(p.Origin) ? p.Origin : Team.Neutral, p.Owner);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(2, 3, 2)]
    [InlineData(3, 8, 7)]
    public void Ship_count_matches_settings(int opponents, int shipsPerEmpire, int allies)
    {
        var w = TestWorld.Create(s => { s.Opponents = opponents; s.ShipsPerEmpire = shipsPerEmpire; s.AlliedBots = allies; });

        Assert.Equal(1 + allies + opponents * shipsPerEmpire, w.Ships.Count);
        Assert.Equal(1 + allies, w.Ships.Count(s => s.Team == w.PlayerTeam));
        Assert.Single(w.Ships, s => s.IsPlayer);
        Assert.Null(w.Player.Brain);
        Assert.All(w.Ships.Where(s => !s.IsPlayer), s => Assert.NotNull(s.Brain));
    }

    [Fact]
    public void Every_ship_starts_alive_beside_a_friendly_world()
    {
        var w = TestWorld.Create(s => { s.Opponents = 3; s.ShipsPerEmpire = 4; s.AlliedBots = 3; });

        Assert.All(w.Ships, s =>
        {
            Assert.True(s.Alive);
            Assert.Contains(w.Planets, p => p.Owner == s.Team && (p.Pos - s.Pos).Length <= GameWorld.OrbitRadius * 1.8 + 1e-6);
        });
    }

    [Fact]
    public void Player_starts_at_their_home_world_in_command_of_ship_one()
    {
        var w = TestWorld.Create(s => s.PlayerTeam = Team.Romulan);
        var home = w.Planets.Single(p => p.IsHome && p.Origin == Team.Romulan);

        Assert.Equal("R1", w.Player.Callsign);
        Assert.Equal(Team.Romulan, w.Player.Team);
        Assert.True((w.Player.Pos - home.Pos).Length < GameWorld.OrbitEnterRange);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Worlds_are_spread_apart(int seed)
    {
        var w = TestWorld.Create(s => { s.Seed = seed; s.Opponents = 3; });
        for (int i = 0; i < w.Planets.Count; i++)
        for (int j = i + 1; j < w.Planets.Count; j++)
            Assert.True((w.Planets[i].Pos - w.Planets[j].Pos).Length >= 700, $"{w.Planets[i].Name} too close to {w.Planets[j].Name}");
    }

    [Fact]
    public void Same_seed_builds_the_same_galaxy()
    {
        var a = TestWorld.Create(s => s.Seed = 99);
        var b = TestWorld.Create(s => s.Seed = 99);

        Assert.Equal(a.Planets.Select(p => (p.Name, p.Pos, p.Armies)), b.Planets.Select(p => (p.Name, p.Pos, p.Armies)));
        Assert.Equal(a.ActiveTeams.OrderBy(t => t), b.ActiveTeams.OrderBy(t => t));
    }
}
