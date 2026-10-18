using System.Windows;
using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

/// <summary>Higher difficulty levels play smarter instead of hitting harder (ISS-002).</summary>
public class SmartDifficultyTests
{
    /// <summary>
    /// Four-empire world at <paramref name="level"/> with every ship removed except the player (no brain), one bot of
    /// <paramref name="botRace"/>, and any ships <paramref name="keep"/> selects (brains off).
    /// </summary>
    static (GameWorld w, Ship bot) Setup(int level, Team botRace, Team playerRace, Func<Ship, bool>? keep = null)
    {
        var w = TestWorld.Quiet(s => { s.Opponents = 3; s.PlayerTeam = playerRace; s.Difficulty = level; s.ShipsPerEmpire = 2; });
        var bot = w.Ships.First(s => s.Team == botRace && !s.IsPlayer);
        foreach (var s in w.Ships.Where(s => s != bot && !s.IsPlayer && !(keep?.Invoke(s) ?? false)))
            TestWorld.Remove(s);
        bot.Brain = new BotBrain(w, bot, seed: 1);
        return (w, bot);
    }

    // ------------------------------------------------------------------ doctrines at lower levels

    [Fact]
    public void Lower_levels_apply_race_doctrines_more_loosely()
    {
        Assert.True(DifficultyProfile.Get(1).DoctrineStrength < DifficultyProfile.Get(2).DoctrineStrength);
        Assert.True(DifficultyProfile.Get(2).DoctrineStrength < 1);
        Assert.All(DifficultyProfile.Levels.Skip(2), p => Assert.Equal(1, p.DoctrineStrength));

        var full = RaceDoctrine.Orion;
        var loose = full.Scaled(DifficultyProfile.Get(1).DoctrineStrength);
        Assert.InRange(loose.FerryChance, new RaceDoctrine { Name = "default" }.FerryChance, full.FerryChance);
        Assert.False(loose.AvoidsEvenFights);
        Assert.False(loose.HitAndRun);
        Assert.Equal(full, full.Scaled(1));
    }

    [Theory]
    [InlineData(1, true)]    // Cadet: barely uses its doctrine, so it attacks anyway
    [InlineData(3, false)]   // Lieutenant: avoids the better-armed ship
    public void Cadet_Orions_attack_a_healthy_Romulan_that_skilled_Orions_avoid(int level, bool attacks)
    {
        var (w, bot) = Setup(level, Team.Orion, Team.Romulan);
        var space = TestWorld.DeepSpace(w);
        TestWorld.Park(bot, space);
        TestWorld.Park(w.Player, space + new Vector(800, 0));

        bot.Brain!.ThinkNow();

        Assert.Equal(attacks, bot.Brain.Mode == BotMode.Fight);
    }

    // ------------------------------------------------------------------ Commander and above: focus fire

    [Theory]
    [InlineData(3, Team.Orion)]     // no focus fire: the nearest enemy
    [InlineData(4, Team.Romulan)]   // focus fire: the enemy an ally is already attacking
    public void Commanders_focus_fire_on_the_enemy_their_allies_are_attacking(int level, Team expectedTarget)
    {
        // Player (Orion) 600 away; a Romulan 700 away that an allied Federation bot is already attacking.
        var (w, bot) = Setup(level, Team.Federation, Team.Orion, keep: s => s.Team is Team.Romulan or Team.Federation);
        var romulan = w.Ships.First(s => s.Team == Team.Romulan);
        var ally = w.Ships.First(s => s.Team == Team.Federation && s != bot);
        foreach (var s in w.Ships.Where(s => s.Team is Team.Romulan or Team.Federation && s != bot && s != ally && s != romulan))
            TestWorld.Remove(s);
        var space = TestWorld.DeepSpace(w);
        TestWorld.Park(bot, space);
        TestWorld.Park(w.Player, space + new Vector(600, 0));
        TestWorld.Park(romulan, space + new Vector(0, 700));
        TestWorld.Park(ally, space + new Vector(0, 900));
        ally.Brain = new BotBrain(w, ally, seed: 2);
        ally.Brain.Assign(BotMode.Fight, null, romulan);

        bot.Brain!.ThinkNow();

        Assert.Equal(BotMode.Fight, bot.Brain.Mode);
        Assert.Equal(expectedTarget, bot.Brain.Target!.Team);
    }

    // ------------------------------------------------------------------ Commander and above: conquest response

    [Theory]
    [InlineData(3, Team.Federation)]   // ignores the countdown: the nearby, weakly held world
    [InlineData(4, Team.Romulan)]      // answers the countdown: one of the leader's worlds
    public void Commanders_attack_the_worlds_of_an_empire_close_to_conquest(int level, Team expectedOwner)
    {
        var (w, bot) = Setup(level, Team.Klingon, Team.Federation);
        TestWorld.Remove(w.Player);

        // The Romulans hold 15 of 24 worlds (60%), starting the conquest countdown.
        var klingonColony = TestWorld.ColonyOf(w, Team.Klingon);
        foreach (var p in w.Planets.Where(p => p.Owner is Team.Romulan or Team.Orion or Team.Neutral || p == klingonColony))
            p.Owner = Team.Romulan;
        TestWorld.Run(w, 15);
        Assert.Equal(Team.Romulan, w.ConquestLeader);

        // A weakly held Federation world sits right next to the bot, which is carrying armies.
        var bait = TestWorld.ColonyOf(w, Team.Federation);
        bait.Armies = 2;
        TestWorld.Park(bot, bait.Pos + new Vector(0, -300));
        bot.Armies = 6;

        bot.Brain!.ThinkNow();

        Assert.Equal(BotMode.Capture, bot.Brain.Mode);
        Assert.Equal(expectedOwner, bot.Brain.Destination!.Owner);
    }

    // ------------------------------------------------------------------ Admiral: coordinated invasions

    /// <summary>A Klingon bot with an ally bombing a Federation world; no world is weak enough to capture yet.</summary>
    static (GameWorld w, Ship bot, Planet beingBombed) Invasion(int level)
    {
        var (w, bot) = Setup(level, Team.Klingon, Team.Federation, keep: s => s.Team == Team.Klingon);
        TestWorld.Remove(w.Player);
        foreach (var p in w.Planets.Where(p => p.Owner != Team.Klingon)) p.Armies = Math.Max(p.Armies, 8);
        var beingBombed = TestWorld.ColonyOf(w, Team.Federation);
        var ally = w.Ships.First(s => s.Team == Team.Klingon && s != bot && s.Alive);
        ally.Brain = new BotBrain(w, ally, seed: 2);
        ally.Brain.Assign(BotMode.Bomb, beingBombed, null);
        return (w, bot, beingBombed);
    }

    [Theory]
    [InlineData(4, BotMode.Bomb)]    // no coordination: nothing to capture yet, so it bombs too
    [InlineData(5, BotMode.Ferry)]   // Admiral: an ally is bombing, so it fetches the armies
    public void Admirals_fetch_armies_while_an_ally_bombs(int level, BotMode expected)
    {
        var (_, bot, _) = Invasion(level);
        bot.Brain!.ThinkNow();
        Assert.Equal(expected, bot.Brain.Mode);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void Admirals_land_armies_on_the_world_their_ally_is_bombing(int level, bool goesToBombedWorld)
    {
        var (w, bot, beingBombed) = Invasion(level);
        // A different enemy world is much closer to the bot.
        var closer = w.Planets.First(p => p.Owner == Team.Federation && p != beingBombed);
        TestWorld.Park(bot, closer.Pos + new Vector(0, -300));
        bot.Armies = 6;

        bot.Brain!.ThinkNow();

        Assert.Equal(BotMode.Capture, bot.Brain.Mode);
        Assert.Equal(goesToBombedWorld, bot.Brain.Destination == beingBombed);
    }
}
