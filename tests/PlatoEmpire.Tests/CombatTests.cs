using System.Windows;
using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

public class CombatTests
{
    /// <summary>Player in deep space facing east, enemy <paramref name="range"/> units directly east.</summary>
    static (GameWorld w, Ship me, Ship enemy) Duel(double range)
    {
        var w = TestWorld.Quiet();
        var space = TestWorld.DeepSpace(w);
        var me = w.Player;
        var enemy = TestWorld.Enemy(w);
        TestWorld.Park(me, space, heading: 90);
        TestWorld.Park(enemy, space + new Vector(range, 0), heading: 270);
        return (w, me, enemy);
    }

    [Fact]
    public void Torpedo_hits_and_damages_enemy()
    {
        var (w, me, enemy) = Duel(200);

        Assert.True(w.FireTorpedo(me, 90));
        Assert.Equal(me.Stats.MaxEnergy - GameWorld.TorpCost, me.Energy, 1e-9);
        Assert.Equal(1, me.ActiveTorps);

        TestWorld.Run(w, 10);

        Assert.Empty(w.Torpedoes);
        Assert.Equal(0, me.ActiveTorps);
        Assert.InRange(enemy.Hull, me.Stats.TorpDamage - 1, me.Stats.TorpDamage);
    }

    [Fact]
    public void Torpedo_fired_away_from_enemy_misses_and_expires()
    {
        var (w, me, enemy) = Duel(200);

        w.FireTorpedo(me, 270);
        TestWorld.Run(w, me.Stats.TorpLife + 1);

        Assert.Empty(w.Torpedoes);
        Assert.Equal(0, enemy.Hull);
    }

    [Fact]
    public void Torpedoes_do_not_hit_friendly_ships()
    {
        var w = TestWorld.Quiet(s => s.AlliedBots = 1);
        var space = TestWorld.DeepSpace(w);
        var ally = w.Ships.First(s => s.Team == w.PlayerTeam && !s.IsPlayer);
        TestWorld.Park(w.Player, space);
        TestWorld.Park(ally, space + new Vector(150, 0));

        w.FireTorpedo(w.Player, 90);
        TestWorld.Run(w, 10);

        Assert.Equal(0, ally.Hull);
    }

    [Fact]
    public void Cannot_fire_without_energy()
    {
        var (w, me, _) = Duel(200);
        me.Energy = 10;

        Assert.False(w.FireTorpedo(me, 90));
        Assert.False(w.FirePhaser(me, 90));
        Assert.Empty(w.Torpedoes);
    }

    [Fact]
    public void Torpedo_tubes_are_limited()
    {
        var (w, me, _) = Duel(5000);
        for (int i = 0; i < me.Stats.MaxTorps; i++)
        {
            me.TorpCooldown = 0;
            Assert.True(w.FireTorpedo(me, 0));
        }
        me.TorpCooldown = 0;
        Assert.False(w.FireTorpedo(me, 0));
    }

    [Fact]
    public void Phaser_hits_target_on_the_line_with_range_falloff()
    {
        var (w, me, enemy) = Duel(300);

        Assert.True(w.FirePhaser(me, 90));

        double expected = me.Stats.PhaserDamage * (1 - 0.6 * 300 / me.Stats.PhaserRange);
        Assert.Equal(expected, enemy.Hull, 1e-6);
        Assert.True(me.PhaserCooldown > 0);
        Assert.False(w.FirePhaser(me, 90));
    }

    [Fact]
    public void Phaser_misses_target_off_the_line()
    {
        var (w, me, enemy) = Duel(300);
        w.FirePhaser(me, 0);
        Assert.Equal(0, enemy.Hull);
    }

    [Fact]
    public void Phaser_cannot_reach_beyond_its_range()
    {
        var (w, me, enemy) = Duel(RaceStats.For(Team.Federation).PhaserRange + 100);
        w.FirePhaser(me, 90);
        Assert.Equal(0, enemy.Hull);
    }

    [Fact]
    public void Shields_absorb_most_damage_until_depleted()
    {
        var (w, me, enemy) = Duel(300);
        enemy.ShieldsUp = true;
        enemy.Shield = 10;

        w.ApplyDamage(enemy, 20, me, null);   // shield can absorb 17 but only has 10

        Assert.Equal(0, enemy.Shield, 1e-9);
        Assert.Equal(10, enemy.Hull, 1e-9);
    }

    [Fact]
    public void Shields_take_85_percent_when_charged()
    {
        var (w, me, enemy) = Duel(300);
        enemy.ShieldsUp = true;
        enemy.Shield = enemy.Stats.MaxShield;

        w.ApplyDamage(enemy, 20, me, null);

        Assert.Equal(3, enemy.Hull, 1e-9);
        Assert.Equal(enemy.Stats.MaxShield - 17, enemy.Shield, 1e-9);
    }

    [Fact]
    public void Destroying_a_ship_credits_the_kill_and_it_respawns_at_a_friendly_world()
    {
        var (w, me, enemy) = Duel(300);
        enemy.Armies = 3;

        w.ApplyDamage(enemy, enemy.Stats.MaxHull, me, null);

        Assert.False(enemy.Alive);
        Assert.Equal(1, me.Kills);
        Assert.Equal(1, enemy.Deaths);
        Assert.Equal(0, enemy.Armies);
        Assert.Contains(w.Messages, m => m.Text.Contains("destroyed by"));

        TestWorld.Run(w, GameWorld.RespawnTicks);

        Assert.True(enemy.Alive);
        Assert.Equal(0, enemy.Hull);
        Assert.Contains(w.Planets, p => p.Owner == enemy.Team && (p.Pos - enemy.Pos).Length < GameWorld.OrbitEnterRange);
    }

    [Fact]
    public void Exploding_ship_damages_nearby_ships()
    {
        var (w, me, enemy) = Duel(100);
        w.ApplyDamage(enemy, enemy.Stats.MaxHull, me, null);
        Assert.True(me.Hull > 0);
    }

    [Fact]
    public void Ship_without_friendly_worlds_is_stranded()
    {
        // A second rival keeps the game going once the first empire falls.
        var w = TestWorld.Quiet(s => s.Opponents = 2);
        var enemy = TestWorld.Enemy(w);
        TestWorld.Park(enemy, TestWorld.DeepSpace(w));
        foreach (var p in w.Planets.Where(p => p.Owner == enemy.Team)) p.Owner = Team.Neutral;

        w.ApplyDamage(enemy, enemy.Stats.MaxHull, w.Player, null);
        TestWorld.Run(w, GameWorld.RespawnTicks + 1);

        Assert.False(enemy.Alive);
        Assert.True(enemy.Stranded);
    }

    [Fact]
    public void Planets_fire_on_enemy_ships_in_range()
    {
        var w = TestWorld.Quiet();
        var colony = TestWorld.ColonyOf(w, TestWorld.Rival(w));
        TestWorld.Park(w.Player, colony.Pos + new Vector(0, -300));

        TestWorld.Run(w, 31);

        Assert.True(w.Player.Hull > 0);
    }

    [Fact]
    public void Heavy_damage_limits_top_speed()
    {
        var w = TestWorld.Quiet();
        var me = w.Player;
        Assert.Equal(me.Stats.MaxWarp, me.EffectiveMaxWarp);
        me.Hull = me.Stats.MaxHull * 0.8;
        Assert.True(me.EffectiveMaxWarp < me.Stats.MaxWarp * 0.7);
    }
}
