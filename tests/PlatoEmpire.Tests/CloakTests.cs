using System.Windows;
using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

public class CloakTests
{
    /// <summary>
    /// A Romulan player parked in deep space and one active enemy bot <paramref name="range"/> units away.
    /// Returns the distances at which the bot fired, plus whether the player took damage.
    /// </summary>
    static (List<double> shots, bool hit) Encounter(bool cloaked, double range, int ticks = 600)
    {
        var w = TestWorld.Quiet(s => s.PlayerTeam = Team.Romulan);
        var me = w.Player;
        var bot = TestWorld.Enemy(w);
        bot.Brain = new BotBrain(w, bot, seed: 1);
        var space = TestWorld.DeepSpace(w);
        TestWorld.Park(me, space);
        TestWorld.Park(bot, space + new Vector(range, 0));
        if (cloaked) w.SetCloak(me, true);

        var shots = new List<double>();
        for (int i = 0; i < ticks; i++)
        {
            me.Energy = me.Stats.MaxEnergy;   // keep the cloak powered for the whole test
            w.Step();
            // Cooldowns are set when firing and tick down once in the same step.
            if (bot.TorpCooldown == 6 || bot.PhaserCooldown == 23) shots.Add((bot.Pos - me.Pos).Length);
        }
        return (shots, me.Hull > 0 || me.Deaths > 0);
    }

    [Fact]
    public void Uncloaked_ship_is_attacked_at_range()
    {
        var (shots, hit) = Encounter(cloaked: false, range: 800);
        Assert.NotEmpty(shots);
        Assert.True(hit);
    }

    [Theory]
    [InlineData(800)]   // long range
    [InlineData(350)]   // normal fighting distance
    public void Cloaked_ship_is_never_fired_on_from_beyond_detection_range(double range)
    {
        var (shots, _) = Encounter(cloaked: false, range: range);
        var (cloakedShots, _) = Encounter(cloaked: true, range: range);

        Assert.NotEmpty(shots);
        Assert.All(cloakedShots, d => Assert.True(d < GameWorld.CloakVisibility, $"bot fired from {d:0} units"));
    }

    [Fact]
    public void Cloak_hides_a_ship_at_normal_fighting_distance()
    {
        // Bots fight at roughly 400-900 units, so detection must be well inside that.
        Assert.True(GameWorld.CloakVisibility <= 300);
    }

    [Fact]
    public void Cloaked_ship_is_seen_and_attacked_up_close()
    {
        var (shots, hit) = Encounter(cloaked: true, range: 150);
        Assert.NotEmpty(shots);
        Assert.True(hit);
    }

    [Fact]
    public void Enemy_planets_ignore_a_cloaked_ship_passing_by()
    {
        var w = TestWorld.Quiet(s => s.PlayerTeam = Team.Romulan);
        var colony = TestWorld.ColonyOf(w, TestWorld.Rival(w));
        TestWorld.Park(w.Player, colony.Pos + new Vector(0, -300));
        w.SetCloak(w.Player, true);

        TestWorld.Run(w, 90);

        Assert.True(w.Player.Cloaked);
        Assert.Equal(0, w.Player.Hull);
    }

    [Fact]
    public void Enemy_planets_fire_on_a_cloaked_ship_in_orbit()
    {
        var w = TestWorld.Quiet(s => s.PlayerTeam = Team.Romulan);
        var colony = TestWorld.ColonyOf(w, TestWorld.Rival(w));
        TestWorld.Orbit(w, w.Player, colony);
        w.SetCloak(w.Player, true);

        TestWorld.Run(w, 31);

        Assert.True(w.Player.Hull > 0);
    }

    [Fact]
    public void Firing_drops_the_cloak()
    {
        var w = TestWorld.Quiet(s => s.PlayerTeam = Team.Romulan);
        w.SetCloak(w.Player, true);

        w.FireTorpedo(w.Player, 0);

        Assert.False(w.Player.Cloaked);
    }

    // ------------------------------------------------------------------ energy cost

    static Ship ParkedRomulan(GameWorld w)
    {
        TestWorld.Park(w.Player, TestWorld.DeepSpace(w));
        return w.Player;
    }

    /// <summary>Seconds until the cloak shuts off from a full energy tank.</summary>
    static double SecondsCloaked(bool fullSpeedWithShields)
    {
        var w = TestWorld.Quiet(s => s.PlayerTeam = Team.Romulan);
        var me = ParkedRomulan(w);
        w.SetCloak(me, true);
        if (fullSpeedWithShields)
        {
            w.SetShields(me, true);
            w.SetWarp(me, me.Stats.MaxWarp);
        }

        int ticks = 0;
        while (me.Cloaked && ticks < 120 * GameWorld.TicksPerSecond) { w.Step(); ticks++; }
        Assert.False(me.Cloaked);
        return ticks / (double)GameWorld.TicksPerSecond;
    }

    [Fact]
    public void Engaging_the_cloak_costs_energy()
    {
        var w = TestWorld.Quiet(s => s.PlayerTeam = Team.Romulan);
        var me = ParkedRomulan(w);

        w.SetCloak(me, true);

        Assert.True(me.Cloaked);
        Assert.Equal(me.Stats.MaxEnergy - GameWorld.CloakEngageCost, me.Energy, 1e-9);
    }

    [Fact]
    public void Cannot_cloak_without_enough_energy_to_engage_it()
    {
        var w = TestWorld.Quiet(s => s.PlayerTeam = Team.Romulan);
        var me = ParkedRomulan(w);
        me.Energy = GameWorld.CloakEngageCost - 1;

        w.SetCloak(me, true);

        Assert.False(me.Cloaked);
        Assert.Equal(GameWorld.CloakEngageCost - 1, me.Energy, 1e-9);
    }

    [Fact]
    public void Cloak_drains_more_energy_than_the_ship_regenerates()
    {
        var w = TestWorld.Quiet(s => s.PlayerTeam = Team.Romulan);
        var me = ParkedRomulan(w);
        w.SetCloak(me, true);
        double before = me.Energy;

        TestWorld.Run(w, 60);

        Assert.True(GameWorld.CloakDrainPerTick > me.Stats.EnergyRegen);
        Assert.Equal(before - (GameWorld.CloakDrainPerTick - me.Stats.EnergyRegen) * 60, me.Energy, 1e-6);
    }

    [Fact]
    public void Stopped_cloaked_ship_runs_out_of_energy_in_under_a_minute() =>
        Assert.InRange(SecondsCloaked(fullSpeedWithShields: false), 15, 40);

    [Fact]
    public void Cloaked_ship_at_full_speed_with_shields_runs_out_in_seconds() =>
        Assert.InRange(SecondsCloaked(fullSpeedWithShields: true), 5, 15);

    /// <summary>Runs one Romulan bot (with the given company) for two minutes and logs each cloak period.</summary>
    static (int engagements, List<int> periods, string log) WatchRomulanBot(bool alone)
    {
        var w = TestWorld.Quiet(s => s.Opponents = 3);
        var bot = w.Ships.First(s => s.Team == Team.Romulan);
        bot.Brain = new BotBrain(w, bot, seed: 1);
        foreach (var s in w.Ships.Where(s => s != bot))
        {
            if (alone) TestWorld.Remove(s);
            else s.Brain = new BotBrain(w, s, seed: s.Id);
        }

        int engagements = 0, start = 0;
        var periods = new List<int>();
        var log = new List<string>();
        bool wasCloaked = bot.Cloaked;
        for (int i = 0; i < 120 * GameWorld.TicksPerSecond; i++)
        {
            w.Step();
            if (bot.Cloaked == wasCloaked) continue;
            if (bot.Cloaked) { engagements++; start = i; }
            else periods.Add(i - start);
            log.Add($"t={i} {(bot.Cloaked ? "ON " : "OFF")} mode={bot.Brain!.Mode} e={bot.Energy:0} orbit={bot.Orbiting?.Name ?? "-"}");
            wasCloaked = bot.Cloaked;
        }
        return (engagements, periods, string.Join("\n", log));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Romulan_bots_never_pay_to_cloak_and_drop_it_within_a_second(bool alone)
    {
        var (_, periods, log) = WatchRomulanBot(alone);
        Assert.True(periods.All(t => t >= GameWorld.TicksPerSecond), log);
    }

    [Fact]
    public void Cloaked_Romulan_bot_holds_fire_until_it_is_close()
    {
        var w = TestWorld.Quiet(s => s.Opponents = 3);
        var bot = w.Ships.First(s => s.Team == Team.Romulan);
        foreach (var s in w.Ships.Where(s => s != bot && !s.IsPlayer)) TestWorld.Remove(s);
        var space = TestWorld.DeepSpace(w);
        // The player starts beyond the bot's fighting range (2,200 at Lieutenant) but close enough to be worth
        // cloaking against, then flies toward the bot.
        TestWorld.Park(w.Player, space, heading: 90);
        TestWorld.Park(bot, space + new Vector(2400, 0));
        bot.Brain = new BotBrain(w, bot, seed: 1);

        var cloakedShots = new List<double>();
        int shots = 0;
        bool everCloaked = false;
        var trace = new List<string>();
        for (int i = 0; i < 40 * GameWorld.TicksPerSecond; i++)
        {
            w.Player.Hull = 0;   // keep the target alive
            if (i == 60) w.SetWarp(w.Player, 3);
            w.SetCourse(w.Player, Geo.AngleOf(bot.Pos - w.Player.Pos));
            bool wasCloaked = bot.Cloaked;
            double dist = (bot.Pos - w.Player.Pos).Length;
            w.Step();
            everCloaked |= bot.Cloaked;
            if (wasCloaked != bot.Cloaked)
                trace.Add($"t={i} cloaked={bot.Cloaked} mode={bot.Brain!.Mode} d={dist:0} e={bot.Energy:0}");
            if (bot.TorpCooldown == 6 || bot.PhaserCooldown == 23)
            {
                shots++;
                if (wasCloaked) cloakedShots.Add(dist);
            }
        }

        Assert.True(everCloaked, "bot never cloaked");
        Assert.True(shots > 0, "bot never attacked");
        Assert.True(cloakedShots.Count > 0, string.Join("\n", trace.Take(40)));   // it really did open fire from cloak
        Assert.All(cloakedShots, d => Assert.True(d <= BotBrain.AmbushRange, $"fired while cloaked from {d:0} units"));
    }

    [Fact]
    public void Romulan_bots_do_not_cloak_when_nothing_is_near_enough_to_see_them()
    {
        var w = TestWorld.Quiet(s => s.Opponents = 3);
        var bot = w.Ships.First(s => s.Team == Team.Romulan);
        bot.Brain = new BotBrain(w, bot, seed: 1);
        foreach (var s in w.Ships.Where(s => s != bot)) TestWorld.Remove(s);   // no enemy ships anywhere

        var wasted = new List<string>();
        for (int i = 0; i < 120 * GameWorld.TicksPerSecond; i++)
        {
            w.Step();
            double nearestHostileWorld = w.Planets.Where(p => p.Owner != bot.Team && p.Armies > 0)
                .Min(p => (p.Pos - bot.Pos).Length);
            // The bot decides before it moves each tick, so allow one tick of travel past its 1,500-unit limit.
            double oneTick = bot.Stats.MaxWarp * GameWorld.WarpUnit;
            if (bot.Cloaked && nearestHostileWorld > 1500 + oneTick) wasted.Add($"t={i} {nearestHostileWorld:0} units from any hostile world");
        }

        Assert.True(wasted.Count == 0, string.Join("\n", wasted.Take(5)));
    }
}
