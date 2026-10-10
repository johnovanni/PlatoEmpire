using System.Windows;
using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

public class OrbitTests
{
    const int Action = GameWorld.OrbitActionInterval;

    [Fact]
    public void Cannot_orbit_while_too_fast()
    {
        var w = TestWorld.Quiet();
        var p = TestWorld.ColonyOf(w, w.PlayerTeam);
        TestWorld.Park(w.Player, p.Pos + new Vector(0, -200));
        w.Player.Warp = 5;

        Assert.False(w.EnterOrbit(w.Player));
        Assert.Null(w.Player.Orbiting);
    }

    [Fact]
    public void Cannot_orbit_with_no_planet_nearby()
    {
        var w = TestWorld.Quiet();
        TestWorld.Park(w.Player, TestWorld.DeepSpace(w));
        Assert.False(w.EnterOrbit(w.Player));
    }

    [Fact]
    public void Slow_ship_in_range_enters_orbit_and_circles_the_planet()
    {
        var w = TestWorld.Quiet();
        var p = TestWorld.ColonyOf(w, w.PlayerTeam);
        TestWorld.Park(w.Player, p.Pos + new Vector(0, -250));
        w.Player.Warp = 2;

        Assert.True(w.EnterOrbit(w.Player));
        Assert.Same(p, w.Player.Orbiting);

        TestWorld.Run(w, 60);

        Assert.Equal(GameWorld.OrbitRadius, (w.Player.Pos - p.Pos).Length, 1e-6);
        Assert.Equal(0, w.Player.Warp);
    }

    [Fact]
    public void Setting_warp_breaks_orbit()
    {
        var w = TestWorld.Quiet();
        TestWorld.Orbit(w, w.Player, TestWorld.ColonyOf(w, w.PlayerTeam));
        w.Player.Action = OrbitAction.BeamUp;

        w.SetWarp(w.Player, 3);

        Assert.Null(w.Player.Orbiting);
        Assert.Equal(OrbitAction.None, w.Player.Action);
    }

    [Fact]
    public void Bombing_reduces_enemy_armies_down_to_the_floor()
    {
        var w = TestWorld.Quiet();
        var p = TestWorld.ColonyOf(w, TestWorld.Rival(w));
        p.Armies = 8;
        p.RegenTimer = 0;
        TestWorld.Orbit(w, w.Player, p);
        w.Player.ShieldsUp = true;

        w.SetAction(w.Player, OrbitAction.Bomb);
        Assert.Equal(OrbitAction.Bomb, w.Player.Action);
        TestWorld.Run(w, Action * 8);

        Assert.Equal(GameWorld.BombFloor, p.Armies);
        Assert.Equal(OrbitAction.None, w.Player.Action);
        Assert.Equal(TestWorld.Rival(w), p.Owner);
    }

    [Fact]
    public void Cannot_bomb_own_planet()
    {
        var w = TestWorld.Quiet();
        var p = TestWorld.ColonyOf(w, w.PlayerTeam);
        int armies = p.Armies;
        TestWorld.Orbit(w, w.Player, p);

        w.SetAction(w.Player, OrbitAction.Bomb);
        TestWorld.Run(w, Action * 3);

        Assert.Equal(OrbitAction.None, w.Player.Action);
        Assert.Equal(armies, p.Armies);
    }

    [Fact]
    public void Actions_require_orbit()
    {
        var w = TestWorld.Quiet();
        TestWorld.Park(w.Player, TestWorld.DeepSpace(w));
        w.SetAction(w.Player, OrbitAction.Bomb);
        Assert.Equal(OrbitAction.None, w.Player.Action);
    }

    [Fact]
    public void Beam_up_fills_troop_bays_to_capacity()
    {
        var w = TestWorld.Quiet();
        var p = TestWorld.ColonyOf(w, w.PlayerTeam);
        p.Armies = 10;
        p.RegenTimer = 0;
        TestWorld.Orbit(w, w.Player, p);

        w.SetAction(w.Player, OrbitAction.BeamUp);
        TestWorld.Run(w, Action * 9);

        int cap = w.Player.Stats.ArmyCapacity;
        Assert.Equal(cap, w.Player.Armies);
        Assert.Equal(10 - cap, p.Armies);
        Assert.Equal(OrbitAction.None, w.Player.Action);
    }

    [Fact]
    public void Beam_up_always_leaves_one_army_behind()
    {
        var w = TestWorld.Quiet();
        var p = TestWorld.ColonyOf(w, w.PlayerTeam);
        p.Armies = 3;
        p.RegenTimer = 0;
        TestWorld.Orbit(w, w.Player, p);

        w.SetAction(w.Player, OrbitAction.BeamUp);
        TestWorld.Run(w, Action * 6);

        Assert.Equal(2, w.Player.Armies);
        Assert.Equal(1, p.Armies);
    }

    [Fact]
    public void Cannot_beam_up_from_enemy_planet()
    {
        var w = TestWorld.Quiet();
        TestWorld.Orbit(w, w.Player, TestWorld.ColonyOf(w, TestWorld.Rival(w)));
        w.SetAction(w.Player, OrbitAction.BeamUp);
        Assert.Equal(OrbitAction.None, w.Player.Action);
    }

    [Fact]
    public void Beaming_down_more_armies_than_defenders_captures_the_planet()
    {
        var w = TestWorld.Quiet();
        var p = TestWorld.ColonyOf(w, TestWorld.Rival(w));
        p.Armies = 2;
        p.RegenTimer = 0;
        TestWorld.Orbit(w, w.Player, p);
        w.Player.Armies = 4;

        w.SetAction(w.Player, OrbitAction.BeamDown);
        TestWorld.Run(w, Action * 5);

        // 2 armies kill the defenders, the 3rd lands and captures, the 4th reinforces.
        Assert.Equal(w.PlayerTeam, p.Owner);
        Assert.Equal(2, p.Armies);
        Assert.Equal(0, w.Player.Armies);
        Assert.Equal(1, w.Player.Captures);
        Assert.Contains(w.Messages, m => m.Text.Contains($"{p.Name} captured"));
    }

    [Fact]
    public void Beaming_down_too_few_armies_only_weakens_the_planet()
    {
        var w = TestWorld.Quiet();
        var rival = TestWorld.Rival(w);
        var p = TestWorld.ColonyOf(w, rival);
        p.Armies = 5;
        p.RegenTimer = 0;
        TestWorld.Orbit(w, w.Player, p);
        w.Player.Armies = 3;

        w.SetAction(w.Player, OrbitAction.BeamDown);
        TestWorld.Run(w, Action * 5);

        Assert.Equal(rival, p.Owner);
        Assert.Equal(2, p.Armies);
    }

    [Fact]
    public void Orbiting_a_friendly_world_repairs_faster()
    {
        var w = TestWorld.Quiet();
        TestWorld.Orbit(w, w.Player, TestWorld.ColonyOf(w, w.PlayerTeam));
        w.Player.Hull = 50;

        TestWorld.Run(w, 30);

        Assert.Equal(50 - 0.18 * 30, w.Player.Hull, 1e-6);
    }

    [Fact]
    public void Owned_planets_grow_armies_over_time()
    {
        var w = TestWorld.Quiet();
        var p = TestWorld.ColonyOf(w, w.PlayerTeam);
        p.Armies = 5;
        p.RegenTimer = 0;

        TestWorld.Run(w, 300);

        Assert.Equal(6, p.Armies);
    }
}
