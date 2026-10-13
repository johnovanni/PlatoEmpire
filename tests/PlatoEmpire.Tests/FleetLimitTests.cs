using System.Windows;
using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

/// <summary>Fleet size versus worlds held, under each <see cref="FleetRule"/>.</summary>
public class FleetLimitTests
{
    const int Fleet = 4;

    static GameWorld World(FleetRule rule = FleetRule.ScaleWithWorlds) => TestWorld.Quiet(s =>
    {
        s.ShipsPerEmpire = Fleet;
        s.AlliedBots = Fleet - 1;
        s.FleetRule = rule;
    });

    /// <summary>Leaves <paramref name="team"/> holding exactly <paramref name="worlds"/> of its worlds; the rest go independent.</summary>
    static void HoldWorlds(GameWorld w, Team team, int worlds)
    {
        int kept = 0;
        foreach (var p in w.Planets.Where(p => p.Owner == team))
            if (kept++ >= worlds) p.Owner = Team.Neutral;
    }

    /// <summary>Hands <paramref name="team"/> independent worlds until it holds <paramref name="worlds"/>.</summary>
    static void GrowTo(GameWorld w, Team team, int worlds)
    {
        foreach (var p in w.Planets.Where(p => p.Owner == Team.Neutral).Take(worlds - w.Planets.Count(p => p.Owner == team)))
            p.Owner = team;
    }

    /// <summary>Moves a team's ships into deep space, spaced beyond explosion range of each other.</summary>
    static List<Ship> Spread(GameWorld w, Team team)
    {
        var ships = w.Ships.Where(s => s.Team == team).ToList();
        var space = TestWorld.DeepSpace(w);
        for (int i = 0; i < ships.Count; i++) TestWorld.Park(ships[i], space + new Vector(i * 300 - 450, 0));
        return ships;
    }

    static int Alive(GameWorld w, Team team) => w.Ships.Count(s => s.Team == team && s.Alive);

    // ------------------------------------------------------------------ the limit

    [Theory]
    [InlineData(FleetRule.Fixed)]
    [InlineData(FleetRule.ScaleWithWorlds)]
    public void Empire_at_full_strength_can_field_its_whole_fleet(FleetRule rule)
    {
        var w = World(rule);
        Assert.Equal(Fleet, w.FleetCap(TestWorld.Rival(w)));
    }

    [Theory]
    [InlineData(5, 4)]
    [InlineData(4, 4)]
    [InlineData(3, 3)]
    [InlineData(2, 2)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void Losing_worlds_lowers_the_limit_rounded_up(int worldsHeld, int expectedCap)
    {
        var w = World();
        var rival = TestWorld.Rival(w);
        HoldWorlds(w, rival, worldsHeld);
        Assert.Equal(expectedCap, w.FleetCap(rival));
    }

    [Theory]
    [InlineData(6, 5)]
    [InlineData(10, 8)]
    [InlineData(15, 12)]
    public void Gaining_worlds_raises_the_limit(int worldsHeld, int expectedCap)
    {
        var w = World();
        var rival = TestWorld.Rival(w);
        GrowTo(w, rival, worldsHeld);

        Assert.Equal(expectedCap, w.FleetCap(rival));
    }

    [Fact]
    public void Fixed_fleets_ignore_worlds_held()
    {
        var w = World(FleetRule.Fixed);
        var rival = TestWorld.Rival(w);
        HoldWorlds(w, rival, 1);
        Assert.Equal(Fleet, w.FleetCap(rival));

        GrowTo(w, rival, 10);
        Assert.Equal(Fleet, w.FleetCap(rival));
    }

    // ------------------------------------------------------------------ shrinking

    [Fact]
    public void Ships_already_flying_are_not_removed_when_worlds_are_lost()
    {
        var w = World();
        var rival = TestWorld.Rival(w);
        Spread(w, rival);
        HoldWorlds(w, rival, 1);

        TestWorld.Run(w, 60);

        Assert.Equal(Fleet, Alive(w, rival));
    }

    [Fact]
    public void Lost_ships_are_only_replaced_up_to_the_limit()
    {
        var w = World();
        var rival = TestWorld.Rival(w);
        var ships = Spread(w, rival);
        HoldWorlds(w, rival, 2);

        foreach (var s in ships.Take(3)) w.ApplyDamage(s, 1000, w.Player, null);
        TestWorld.Run(w, GameWorld.RespawnTicks + 30);
        Assert.Equal(2, Alive(w, rival));

        TestWorld.Run(w, GameWorld.RespawnTicks * 3);
        Assert.Equal(2, Alive(w, rival));
        Assert.Equal(Fleet, w.FleetSize(rival));
    }

    [Fact]
    public void Regaining_worlds_lets_waiting_ships_return()
    {
        var w = World();
        var rival = TestWorld.Rival(w);
        var ships = Spread(w, rival);
        var worlds = w.Planets.Where(p => p.Owner == rival).ToList();
        HoldWorlds(w, rival, 1);
        foreach (var s in ships.Take(3)) w.ApplyDamage(s, 1000, w.Player, null);
        TestWorld.Run(w, GameWorld.RespawnTicks + 30);
        Assert.Equal(1, Alive(w, rival));

        foreach (var p in worlds) p.Owner = rival;
        TestWorld.Run(w, 2);

        Assert.Equal(Fleet, Alive(w, rival));
    }

    // ------------------------------------------------------------------ growing

    [Fact]
    public void Shipyards_build_one_ship_at_a_time_up_to_the_raised_limit()
    {
        var w = World();
        var rival = TestWorld.Rival(w);
        GrowTo(w, rival, 10);   // limit 8

        RunWithNewShipsIdle(w, GameWorld.ShipyardTicks);
        Assert.Equal(Fleet + 1, w.FleetSize(rival));

        RunWithNewShipsIdle(w, GameWorld.ShipyardTicks * 3);
        Assert.Equal(8, w.FleetSize(rival));

        RunWithNewShipsIdle(w, GameWorld.ShipyardTicks * 4);
        Assert.Equal(8, w.FleetSize(rival));
    }

    /// <summary>Steps the world, switching off each newly built ship's brain so it can't capture worlds and move the limit.</summary>
    static void RunWithNewShipsIdle(GameWorld w, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            w.Step();
            foreach (var s in w.Ships) s.Brain = null;
        }
    }

    [Fact]
    public void New_ships_are_bots_that_launch_from_a_friendly_world()
    {
        var w = World();
        var rival = TestWorld.Rival(w);
        GrowTo(w, rival, 10);

        TestWorld.Run(w, GameWorld.ShipyardTicks);

        var built = w.Ships[^1];
        Assert.Equal(rival, built.Team);
        Assert.False(built.IsPlayer);
        Assert.NotNull(built.Brain);
        Assert.True(built.Alive);
        Assert.Equal($"{RaceStats.For(rival).Letter}{Fleet + 1}", built.Callsign);
    }

    [Fact]
    public void Destroyed_ships_return_instead_of_new_ones_being_built()
    {
        var w = World();
        var rival = TestWorld.Rival(w);
        var ships = Spread(w, rival);

        w.ApplyDamage(ships[0], 1000, w.Player, null);
        TestWorld.Run(w, GameWorld.ShipyardTicks * 2);

        Assert.Equal(Fleet, w.FleetSize(rival));
        Assert.Equal(Fleet, Alive(w, rival));
    }

    [Fact]
    public void Shipyards_do_not_build_when_fleets_are_fixed()
    {
        var w = World(FleetRule.Fixed);
        var rival = TestWorld.Rival(w);
        GrowTo(w, rival, 10);

        TestWorld.Run(w, GameWorld.ShipyardTicks * 4);

        Assert.Equal(Fleet, w.FleetSize(rival));
    }

    // ------------------------------------------------------------------ the player

    [Fact]
    public void Player_is_never_kept_waiting_by_the_limit()
    {
        var w = World();
        Spread(w, w.PlayerTeam);
        HoldWorlds(w, w.PlayerTeam, 1);   // limit 1, but 4 ships are flying

        w.ApplyDamage(w.Player, 1000, null, null);
        TestWorld.Run(w, GameWorld.RespawnTicks);

        Assert.True(w.Player.Alive);
    }

    [Fact]
    public void Player_counts_toward_the_limit()
    {
        var w = World();
        var ships = Spread(w, w.PlayerTeam);
        HoldWorlds(w, w.PlayerTeam, 2);   // limit 2: the player plus one ally

        foreach (var ally in ships.Where(s => !s.IsPlayer)) w.ApplyDamage(ally, 1000, null, null);
        TestWorld.Run(w, GameWorld.RespawnTicks + 30);

        Assert.True(w.Player.Alive);
        Assert.Equal(2, Alive(w, w.PlayerTeam));
    }

    [Fact]
    public void Player_is_told_when_losing_a_world_shrinks_their_fleet()
    {
        var w = World();
        HoldWorlds(w, w.PlayerTeam, 2);   // limit 2
        var colony = w.Planets.First(p => p.Owner == w.PlayerTeam && !p.IsHome);
        colony.Armies = 0;
        colony.RegenTimer = 0;
        var enemy = TestWorld.Enemy(w);
        TestWorld.Orbit(w, enemy, colony);
        enemy.Armies = 1;

        w.SetAction(enemy, OrbitAction.BeamDown);
        TestWorld.Run(w, GameWorld.OrbitActionInterval * 2);

        Assert.Equal(enemy.Team, colony.Owner);
        Assert.Contains(w.Messages, m => m.Text.Contains("Fleet limit") && m.Text.Contains("support 1 ship."));
    }

    [Fact]
    public void Player_is_told_when_a_new_allied_ship_is_commissioned()
    {
        var w = World();
        GrowTo(w, w.PlayerTeam, 10);

        TestWorld.Run(w, GameWorld.ShipyardTicks);

        Assert.Contains(w.Messages, m => m.Text.Contains("commissioned") && m.Text.Contains("support 8 ships"));
    }
}
