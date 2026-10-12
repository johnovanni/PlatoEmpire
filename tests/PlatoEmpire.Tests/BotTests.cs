using System.Windows;
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

    static double EdgeDistance(Vector p) =>
        Math.Min(Math.Min(p.X, p.Y), Math.Min(GameWorld.GalaxySize - p.X, GameWorld.GalaxySize - p.Y));

    [Theory]
    [InlineData("west")]
    [InlineData("east")]
    [InlineData("north")]
    [InlineData("south")]
    public void Bot_fighting_a_target_on_the_edge_stays_off_the_edge(string wall)
    {
        var w = TestWorld.Quiet();
        var bot = TestWorld.Enemy(w);
        bot.Brain = new BotBrain(w, bot, seed: 1);
        double size = GameWorld.GalaxySize, mid = size / 2;
        var (edge, inward) = wall switch
        {
            "west" => (new Vector(60, mid), new Vector(1, 0)),
            "east" => (new Vector(size - 60, mid), new Vector(-1, 0)),
            "north" => (new Vector(mid, 60), new Vector(0, 1)),
            _ => (new Vector(mid, size - 60), new Vector(0, -1)),
        };
        TestWorld.Park(bot, edge + inward * 1500);

        double closest = double.MaxValue;
        for (int i = 0; i < GameWorld.TicksPerSecond * 60; i++)
        {
            // Pin the target against the wall and keep it alive so the bot keeps fighting there.
            w.Player.Pos = edge;
            w.Player.Hull = 0;
            w.Step();
            if (bot.Alive) closest = Math.Min(closest, EdgeDistance(bot.Pos));
        }

        Assert.Equal(BotMode.Fight, bot.Brain!.Mode);
        Assert.True(closest >= BotBrain.EdgeBuffer, $"bot came within {closest:0} units of the {wall} edge");
    }

    [Fact]
    public void Bots_never_pin_themselves_against_the_edge_in_a_full_game()
    {
        var w = new GameWorld(new GameSettings { Seed = 11, PlayerAutopilot = true, Opponents = 3, ShipsPerEmpire = 6, AlliedBots = 5, Difficulty = 5 });
        double closest = double.MaxValue;

        for (int i = 0; i < GameWorld.TicksPerSecond * 60 * 5; i++)
        {
            w.Step();
            foreach (var s in w.Ships.Where(s => s.Alive)) closest = Math.Min(closest, EdgeDistance(s.Pos));
        }

        Assert.True(closest >= BotBrain.EdgeBuffer, $"a bot came within {closest:0} units of the edge");
    }

    [Theory]
    [InlineData(5000, 5000, 270)]   // open space
    [InlineData(300, 5000, 0)]      // close to the west edge but running parallel to it
    [InlineData(3000, 5000, 270)]   // heading west but the edge is still far off
    public void Edge_avoidance_leaves_safe_courses_alone(double x, double y, double course) =>
        Assert.Equal(course, BotBrain.AvoidEdges(new Vector(x, y), course, 16));

    [Theory]
    [InlineData(400, 5000, 270)]    // straight at the west edge
    [InlineData(9600, 5000, 90)]    // straight at the east edge
    [InlineData(5000, 400, 0)]      // straight at the north edge
    [InlineData(5000, 9600, 180)]   // straight at the south edge
    [InlineData(400, 400, 315)]     // into the north-west corner
    public void Edge_avoidance_turns_ships_away_from_an_approaching_edge(double x, double y, double course)
    {
        var pos = new Vector(x, y);
        double avoided = BotBrain.AvoidEdges(pos, course, 16);

        var toCenter = Geo.AngleOf(new Vector(GameWorld.GalaxySize / 2, GameWorld.GalaxySize / 2) - pos);
        Assert.True(Math.Abs(Geo.Diff(avoided, toCenter)) < 90, $"new course {avoided:0} still heads out of the galaxy");
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
