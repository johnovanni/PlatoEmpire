using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

public class DifficultyTests
{
    [Theory]
    [InlineData(-3, 1)]
    [InlineData(0, 1)]
    [InlineData(3, 3)]
    [InlineData(99, 5)]
    public void Get_clamps_to_valid_levels(int requested, int expected) =>
        Assert.Equal(expected, DifficultyProfile.Get(requested).Level);

    [Fact]
    public void Each_level_is_at_least_as_hard_as_the_last()
    {
        var levels = DifficultyProfile.Levels;
        for (int i = 1; i < levels.Length; i++)
        {
            var easier = levels[i - 1];
            var harder = levels[i];
            Assert.True(harder.ReactionTicks < easier.ReactionTicks, $"{harder.Name} reaction");
            Assert.True(harder.AimErrorDeg < easier.AimErrorDeg, $"{harder.Name} aim");
            Assert.True(harder.FireChance > easier.FireChance, $"{harder.Name} fire chance");
            // Engagement range stops growing at Lieutenant: chasing distant ships made higher levels capture less (ISS-002).
            Assert.True(harder.EngageRange >= easier.EngageRange, $"{harder.Name} engage range");
            Assert.True(harder.DoctrineStrength >= easier.DoctrineStrength, $"{harder.Name} doctrine strength");
            Assert.True(!easier.FocusFire || harder.FocusFire, $"{harder.Name} focus fire");
            Assert.True(!easier.RespondsToConquest || harder.RespondsToConquest, $"{harder.Name} conquest response");
            Assert.True(!easier.CoordinatesInvasions || harder.CoordinatesInvasions, $"{harder.Name} coordination");
        }
    }

    [Fact]
    public void Allies_fly_at_lieutenant_level() => Assert.Equal("Lieutenant", DifficultyProfile.Ally.Name);

    [Fact]
    public void SetDifficulty_changes_profile_and_announces_it()
    {
        var w = TestWorld.Quiet();
        int before = w.Messages.Count;

        w.SetDifficulty(5);

        Assert.Equal(5, w.Difficulty.Level);
        Assert.Equal(5, w.Settings.Difficulty);
        Assert.Contains("Admiral", w.Messages[^1].Text);
        Assert.Equal(before + 1, w.Messages.Count);
    }

    [Fact]
    public void SetDifficulty_to_current_level_is_silent()
    {
        var w = TestWorld.Quiet();
        int before = w.Messages.Count;
        w.SetDifficulty(w.Difficulty.Level);
        Assert.Equal(before, w.Messages.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Bot_weapons_do_the_same_damage_at_every_level(int level)
    {
        // ISS-002: difficulty changes how bots play, never how hard their weapons hit.
        var w = TestWorld.Create(s => s.Difficulty = level);
        var enemy = TestWorld.Enemy(w);
        var space = TestWorld.DeepSpace(w);
        TestWorld.Park(enemy, space);
        TestWorld.Park(w.Player, space + new System.Windows.Vector(300, 0));

        Assert.True(w.FireTorpedo(enemy, 0));
        Assert.True(w.FirePhaser(enemy, 90));

        Assert.Equal(enemy.Stats.TorpDamage, w.Torpedoes[^1].Damage, 1e-9);
        Assert.Equal(enemy.Stats.PhaserDamage * (1 - 0.6 * 300 / enemy.Stats.PhaserRange), w.Player.Hull, 1e-6);
    }

    [Fact]
    public void Allied_bots_and_player_are_not_boosted_by_difficulty()
    {
        var w = TestWorld.Create(s => { s.Difficulty = 5; s.AlliedBots = 1; });
        var ally = w.Ships.First(s => s.Team == w.PlayerTeam && !s.IsPlayer);

        Assert.True(w.FireTorpedo(ally, 0));
        Assert.True(w.FireTorpedo(w.Player, 0));

        Assert.All(w.Torpedoes, t => Assert.Equal(t.Owner.Stats.TorpDamage, t.Damage, 1e-9));
    }
}
