using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

public class VictoryTests
{
    static int HoldTicks(GameWorld w) => w.Settings.ConquestHoldSeconds * GameWorld.TicksPerSecond;

    /// <summary>Quiet world with the player and rival ship parked out of harm's way.</summary>
    static GameWorld Arena(int conquestPercent = 75)
    {
        var w = TestWorld.Quiet(s => s.ConquestPercent = conquestPercent);
        var space = TestWorld.DeepSpace(w);
        TestWorld.Park(w.Player, space);
        TestWorld.Park(TestWorld.Enemy(w), space + new System.Windows.Vector(2000, 0));
        return w;
    }

    [Theory]
    [InlineData(60, 15)]
    [InlineData(75, 18)]
    [InlineData(90, 22)]
    [InlineData(100, 24)]
    public void Planets_needed_is_rounded_up_share_of_the_galaxy(int percent, int needed) =>
        Assert.Equal(needed, Arena(percent).ConquestPlanetsNeeded);

    [Fact]
    public void Conquest_share_below_a_majority_is_raised_to_51_percent()
    {
        var w = Arena(30);
        Assert.Equal(51, w.ConquestPercent);
        Assert.Equal(13, w.ConquestPlanetsNeeded);
    }

    [Fact]
    public void Holding_the_conquest_share_long_enough_wins()
    {
        var w = Arena();
        TestWorld.AssignWorlds(w, 18);

        TestWorld.Run(w, 15);
        Assert.Equal(w.PlayerTeam, w.ConquestLeader);
        Assert.Equal(GameResult.None, w.Result);

        TestWorld.Run(w, HoldTicks(w) - 30);
        Assert.Equal(GameResult.None, w.Result);

        TestWorld.Run(w, 30);
        Assert.Equal(GameResult.Victory, w.Result);
        Assert.Contains("conquered 18 of 24", w.EndReason);
    }

    [Fact]
    public void Falling_short_of_the_share_starts_no_countdown()
    {
        var w = Arena();
        TestWorld.AssignWorlds(w, 17);

        TestWorld.Run(w, HoldTicks(w) * 2);

        Assert.Null(w.ConquestLeader);
        Assert.Equal(GameResult.None, w.Result);
    }

    [Fact]
    public void Losing_a_world_resets_the_countdown()
    {
        var w = Arena();
        TestWorld.AssignWorlds(w, 18);
        TestWorld.Run(w, HoldTicks(w) / 2);

        var lost = w.Planets.First(p => p.Owner == w.PlayerTeam);
        lost.Owner = Team.Neutral;
        TestWorld.Run(w, 15);
        Assert.Null(w.ConquestLeader);
        Assert.Contains(w.Messages, m => m.Text.Contains("conquest countdown is reset"));

        lost.Owner = w.PlayerTeam;
        TestWorld.Run(w, 15);
        Assert.Equal(w.PlayerTeam, w.ConquestLeader);
        Assert.True(w.ConquestTicksRemaining > HoldTicks(w) - 30);

        TestWorld.Run(w, HoldTicks(w) - 60);
        Assert.Equal(GameResult.None, w.Result);
    }

    [Fact]
    public void Rival_holding_the_conquest_share_defeats_the_player()
    {
        var w = Arena();
        var rival = TestWorld.Rival(w);
        TestWorld.AssignWorlds(w, 18, holder: rival);

        TestWorld.Run(w, 15);
        Assert.Equal(rival, w.ConquestLeader);
        Assert.Contains(w.Messages, m => m.Text.StartsWith("WARNING"));

        TestWorld.Run(w, HoldTicks(w) + 15);

        Assert.Equal(GameResult.Defeat, w.Result);
        Assert.Contains(RaceStats.For(rival).Plural, w.EndReason);
    }

    [Fact]
    public void Conquest_can_be_switched_off()
    {
        var w = Arena(conquestPercent: 0);
        TestWorld.AssignWorlds(w, 23);

        TestWorld.Run(w, HoldTicks(w) * 2);

        Assert.False(w.ConquestEnabled);
        Assert.Null(w.ConquestLeader);
        Assert.Equal(GameResult.None, w.Result);
    }

    [Fact]
    public void Eliminating_every_rival_wins_regardless_of_world_count()
    {
        var w = Arena();
        var rival = TestWorld.Rival(w);
        foreach (var p in w.Planets.Where(p => p.Owner == rival)) p.Owner = Team.Neutral;
        foreach (var s in w.Ships.Where(s => s.Team == rival)) TestWorld.Remove(s);

        TestWorld.Run(w, 15);

        Assert.Equal(GameResult.Victory, w.Result);
        Assert.Contains(rival, w.Eliminated);
        Assert.Equal("Every rival empire has fallen.", w.EndReason);
    }

    [Fact]
    public void Rival_with_ships_but_no_worlds_is_not_yet_eliminated()
    {
        var w = Arena();
        var rival = TestWorld.Rival(w);
        foreach (var p in w.Planets.Where(p => p.Owner == rival)) p.Owner = Team.Neutral;

        TestWorld.Run(w, 30);

        Assert.DoesNotContain(rival, w.Eliminated);
        Assert.Equal(GameResult.None, w.Result);
    }

    [Fact]
    public void Player_with_no_worlds_left_to_respawn_at_is_defeated()
    {
        var w = Arena();
        var rival = TestWorld.Rival(w);
        foreach (var p in w.Planets.Where(p => p.Owner == w.PlayerTeam)) p.Owner = rival;

        w.ApplyDamage(w.Player, 1000, null, null);
        TestWorld.Run(w, GameWorld.RespawnTicks + 30);

        Assert.Equal(GameResult.Defeat, w.Result);
    }

    [Fact]
    public void Game_stops_advancing_once_decided()
    {
        var w = Arena();
        var rival = TestWorld.Rival(w);
        foreach (var p in w.Planets.Where(p => p.Owner == rival)) p.Owner = Team.Neutral;
        foreach (var s in w.Ships.Where(s => s.Team == rival)) TestWorld.Remove(s);
        GameResult? raised = null;
        w.GameEnded += r => raised = r;

        TestWorld.Run(w, 15);
        int tick = w.Tick;
        TestWorld.Run(w, 100);

        Assert.Equal(GameResult.Victory, raised);
        Assert.Equal(tick, w.Tick);
    }
}
