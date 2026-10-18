using System.Windows;
using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

/// <summary>Each race's bots play to their ship's strengths (ISS-001).</summary>
public class DoctrineTests
{
    /// <summary>
    /// A four-empire world with every bot switched off except one bot of <paramref name="botRace"/>. The player
    /// (no brain) flies <paramref name="playerRace"/>. Other ships are removed unless <paramref name="keep"/> says so.
    /// </summary>
    static (GameWorld w, Ship bot) Setup(Team botRace, Team playerRace, int seed = 42, Func<Ship, bool>? keep = null)
    {
        var w = TestWorld.Quiet(s => { s.Opponents = 3; s.PlayerTeam = playerRace; s.Seed = seed; });
        var bot = w.Ships.First(s => s.Team == botRace && !s.IsPlayer);
        foreach (var s in w.Ships.Where(s => s != bot && !s.IsPlayer && !(keep?.Invoke(s) ?? false)))
            TestWorld.Remove(s);
        bot.Brain = new BotBrain(w, bot, seed);
        return (w, bot);
    }

    static Vector Space(GameWorld w) => TestWorld.DeepSpace(w);

    // ------------------------------------------------------------------ Orion: raider and transport

    /// <summary>Share of fresh bots of <paramref name="race"/> that choose to ferry when they could also bomb.</summary>
    static double FerryRate(Team race)
    {
        int ferry = 0, decided = 0;
        for (int seed = 1; seed <= 100; seed++)
        {
            var (w, bot) = Setup(race, race == Team.Klingon ? Team.Federation : Team.Klingon, seed);
            TestWorld.Remove(w.Player);
            var rival = w.ActiveTeams.First(t => t != race && t != w.PlayerTeam);
            TestWorld.ColonyOf(w, rival).Armies = 3;   // a world every race can capture in one trip

            bot.Brain!.ThinkNow();
            if (bot.Brain.Mode is BotMode.Ferry or BotMode.Bomb) decided++;
            if (bot.Brain.Mode == BotMode.Ferry) ferry++;
        }
        Assert.True(decided > 90, $"only {decided} of 100 bots chose ferry or bomb");
        return ferry / (double)decided;
    }

    [Fact]
    public void Orion_bots_ferry_armies_far_more_often_than_Federation_bots()
    {
        double orion = FerryRate(Team.Orion), federation = FerryRate(Team.Federation);
        Assert.True(orion > 0.75, $"Orion ferry rate {orion:P0}");
        Assert.True(federation < 0.5, $"Federation ferry rate {federation:P0}");
    }

    [Theory]
    [InlineData(Team.Federation)]
    [InlineData(Team.Klingon)]
    [InlineData(Team.Romulan)]
    [InlineData(Team.Orion)]
    public void Bots_stick_to_a_ferry_or_bombing_plan_instead_of_rerolling_it(Team race)
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            var (w, bot) = Setup(race, race == Team.Klingon ? Team.Federation : Team.Klingon, seed);
            TestWorld.Remove(w.Player);
            var rival = w.ActiveTeams.First(t => t != race && t != w.PlayerTeam);
            TestWorld.ColonyOf(w, rival).Armies = 3;   // ferrying and bombing are both possible

            bot.Brain!.ThinkNow();
            var (mode, destination) = (bot.Brain.Mode, bot.Brain.Destination);
            Assert.True(mode is BotMode.Ferry or BotMode.Bomb, $"seed {seed}: chose {mode}");
            for (int i = 0; i < 30; i++)
            {
                bot.Brain.ThinkNow();
                Assert.True(bot.Brain.Mode == mode && bot.Brain.Destination == destination,
                    $"seed {seed}: switched from {mode} to {bot.Brain.Mode} on re-plan {i + 1}");
            }
        }
    }

    [Fact]
    public void Orion_bot_avoids_a_healthy_Romulan()
    {
        var (w, bot) = Setup(Team.Orion, Team.Romulan);
        var space = Space(w);
        TestWorld.Park(bot, space);
        TestWorld.Park(w.Player, space + new Vector(800, 0));

        bot.Brain!.ThinkNow();

        Assert.NotEqual(BotMode.Fight, bot.Brain.Mode);
    }

    [Theory]
    [InlineData("damaged")]
    [InlineData("carrying armies")]
    public void Orion_bot_attacks_a_Romulan_that_is(string condition)
    {
        var (w, bot) = Setup(Team.Orion, Team.Romulan);
        var space = Space(w);
        TestWorld.Park(bot, space);
        TestWorld.Park(w.Player, space + new Vector(800, 0));
        if (condition == "damaged") w.Player.Hull = w.Player.Stats.MaxHull * 0.6;
        else w.Player.Armies = 4;

        bot.Brain!.ThinkNow();

        Assert.Equal(BotMode.Fight, bot.Brain.Mode);
        Assert.Same(w.Player, bot.Brain.Target);
    }

    [Fact]
    public void Orion_bot_hits_and_runs_outside_a_slower_ships_phaser_range()
    {
        var (w, bot) = Setup(Team.Orion, Team.Romulan);
        var space = Space(w);
        TestWorld.Park(w.Player, space);
        TestWorld.Park(bot, space + new Vector(1500, 0));
        double phaserRange = w.Player.Stats.PhaserRange;

        int ticks = 0, inside = 0, shots = 0;
        for (int i = 0; i < 30 * GameWorld.TicksPerSecond; i++)
        {
            w.Player.Hull = w.Player.Stats.MaxHull * 0.6;   // damaged, so worth attacking; kept from dying
            w.Step();
            if (bot.TorpCooldown == 6) shots++;
            if (i < 10 * GameWorld.TicksPerSecond) continue;   // let it close in first
            ticks++;
            if ((bot.Pos - w.Player.Pos).Length < phaserRange) inside++;
        }

        Assert.True(shots > 0, "Orion never fired");
        Assert.True(inside < ticks * 0.2, $"inside Romulan phaser range {inside * 100 / ticks}% of the time");
    }

    // ------------------------------------------------------------------ Klingon: aggressive hunter

    /// <summary>Two enemies: the player (healthy, empty) 600 units away and a second ship further out.</summary>
    static Ship TargetChosenBy(Team botRace, Action<Ship> configureFarShip, double farDistance)
    {
        var (w, bot) = Setup(botRace, Team.Orion, keep: s => s.Team == Team.Romulan);
        var far = w.Ships.First(s => s.Team == Team.Romulan && s.Alive);
        var space = Space(w);
        TestWorld.Park(bot, space);
        TestWorld.Park(w.Player, space + new Vector(600, 0));
        TestWorld.Park(far, space + new Vector(0, farDistance));
        configureFarShip(far);

        bot.Brain!.ThinkNow();
        Assert.Equal(BotMode.Fight, bot.Brain.Mode);
        return bot.Brain.Target!;
    }

    [Fact]
    public void Klingon_bot_hunts_a_damaged_ship_over_a_closer_healthy_one()
    {
        static void Damaged(Ship s) => s.Hull = s.Stats.MaxHull * 0.5;
        Assert.Equal(Team.Romulan, TargetChosenBy(Team.Klingon, Damaged, 900).Team);
        Assert.Equal(Team.Orion, TargetChosenBy(Team.Federation, Damaged, 900).Team);   // contrast
    }

    [Fact]
    public void Klingon_bot_hunts_a_distant_troop_carrier_over_a_closer_empty_ship()
    {
        static void Loaded(Ship s) => s.Armies = 4;
        Assert.Equal(Team.Romulan, TargetChosenBy(Team.Klingon, Loaded, 1800).Team);
        Assert.Equal(Team.Orion, TargetChosenBy(Team.Federation, Loaded, 1800).Team);   // contrast
    }

    // ------------------------------------------------------------------ Romulan: ambush and siege

    /// <summary>Whether a bot is still chasing an Orion player that flies away at full speed for 10 seconds.</summary>
    static bool StillChasingAfter10s(Team botRace)
    {
        var (w, bot) = Setup(botRace, Team.Orion);
        var start = new Vector(2000, 5000);
        TestWorld.Park(bot, start);
        TestWorld.Park(w.Player, start + new Vector(600, 0), heading: 90);
        bot.Brain!.ThinkNow();
        Assert.Equal(BotMode.Fight, bot.Brain.Mode);

        for (int i = 0; i < 10 * GameWorld.TicksPerSecond; i++)
        {
            w.Player.Hull = 0;
            w.SetCourse(w.Player, 90);
            w.SetWarp(w.Player, w.Player.Stats.MaxWarp);
            w.Step();
        }
        return bot.Brain.Mode == BotMode.Fight && bot.Brain.Target == w.Player;
    }

    [Fact]
    public void Romulan_bot_gives_up_chasing_a_faster_ship()
    {
        Assert.False(StillChasingAfter10s(Team.Romulan));
        Assert.True(StillChasingAfter10s(Team.Klingon));   // contrast: hunters keep chasing
    }

    // ------------------------------------------------------------------ Federation: steady line

    /// <summary>Average distance a bot keeps from a stationary target over the last 20 seconds of a 30-second fight.</summary>
    static double AverageFightingDistance(Team botRace)
    {
        var (w, bot) = Setup(botRace, Team.Romulan);
        var space = Space(w);
        TestWorld.Park(w.Player, space);
        TestWorld.Park(bot, space + new Vector(1500, 0));

        double total = 0;
        int samples = 0;
        for (int i = 0; i < 30 * GameWorld.TicksPerSecond; i++)
        {
            w.Player.Hull = 0;
            w.Step();
            if (i < 10 * GameWorld.TicksPerSecond) continue;
            total += (bot.Pos - w.Player.Pos).Length;
            samples++;
        }
        return total / samples;
    }

    [Fact]
    public void Federation_bot_fights_from_further_out_than_a_Klingon()
    {
        double federation = AverageFightingDistance(Team.Federation), klingon = AverageFightingDistance(Team.Klingon);
        Assert.True(federation > klingon + 100, $"Federation {federation:0} vs Klingon {klingon:0}");
    }

    [Fact]
    public void Every_race_has_a_doctrine()
    {
        Assert.Equal(4, RaceStats.Empires.Select(RaceDoctrine.For).Distinct().Count());
    }
}
