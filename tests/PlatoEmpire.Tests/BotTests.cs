using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

public class BotTests
{
    /// <summary>A world where only the rival ship has a brain, and everyone else is gone.</summary>
    static (GameWorld w, Ship bot) Solo()
    {
        var w = TestWorld.Quiet();
        var bot = TestWorld.Enemy(w);
        bot.Brain = new BotBrain(w, bot, seed: 1);
        TestWorld.Remove(w.Player);
        return (w, bot);
    }

    [Fact]
    public void Badly_damaged_bot_runs_for_repairs()
    {
        var (w, bot) = Solo();
        bot.Hull = bot.Stats.MaxHull * 0.95;

        TestWorld.Run(w, 40);

        Assert.Equal(BotMode.Repair, bot.Brain!.Mode);
    }

    [Fact]
    public void Idle_bot_with_no_enemies_goes_on_the_offensive()
    {
        var (w, bot) = Solo();

        TestWorld.Run(w, 40);

        Assert.Contains(bot.Brain!.Mode, new[] { BotMode.Bomb, BotMode.Ferry });
    }

    [Fact]
    public void Bot_carrying_armies_heads_out_to_capture()
    {
        var (w, bot) = Solo();
        bot.Armies = 4;

        TestWorld.Run(w, 40);

        Assert.Equal(BotMode.Capture, bot.Brain!.Mode);
        Assert.True(bot.DesiredWarp > 0);
    }

    [Fact]
    public void Bot_engages_a_nearby_enemy()
    {
        var w = TestWorld.Quiet();
        var bot = TestWorld.Enemy(w);
        bot.Brain = new BotBrain(w, bot, seed: 1);
        var space = TestWorld.DeepSpace(w);
        TestWorld.Park(bot, space);
        TestWorld.Park(w.Player, space + new System.Windows.Vector(600, 0));

        TestWorld.Run(w, 90);

        Assert.Equal(BotMode.Fight, bot.Brain!.Mode);
        Assert.True(w.Player.Hull > 0 || w.Torpedoes.Count > 0 || w.Player.Deaths > 0, "bot never fired");
    }

    [Fact]
    public void All_bot_game_runs_without_errors_and_worlds_change_hands()
    {
        var w = new GameWorld(new GameSettings { Seed = 3, PlayerAutopilot = true, Opponents = 3, ShipsPerEmpire = 4, AlliedBots = 3 });
        var initialOwners = w.Planets.Select(p => p.Owner).ToArray();

        TestWorld.Run(w, GameWorld.TicksPerSecond * 60 * 8);

        Assert.Contains(w.Planets.Select((p, i) => p.Owner != initialOwners[i]), changed => changed);
        Assert.True(w.Ships.Sum(s => s.Kills) > 0);
        Assert.All(w.Planets, p => Assert.True(p.Armies >= 0));
    }
}
