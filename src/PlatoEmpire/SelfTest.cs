using System.Diagnostics;
using System.Text;
using PlatoEmpire.Game;

namespace PlatoEmpire;

/// <summary>Plays full bot-vs-bot games (the player's ship on autopilot) at every difficulty.</summary>
public static class SelfTest
{
    public static string Run()
    {
        var sb = new StringBuilder();
        const int maxTicks = GameWorld.TicksPerSecond * 60 * 40;
        foreach (var level in DifficultyProfile.Levels)
        {
            try
            {
                var world = new GameWorld(new GameSettings
                {
                    PlayerTeam = Team.Federation, Opponents = 3, ShipsPerEmpire = 4, AlliedBots = 3,
                    Difficulty = level.Level, Seed = 1000 + level.Level, PlayerAutopilot = true,
                });
                int captures = 0;
                world.MessageAdded += m => { if (m.Text.Contains("captured")) captures++; };

                var sw = Stopwatch.StartNew();
                while (world.Result == GameResult.None && world.Tick < maxTicks) world.Step();
                sw.Stop();

                sb.AppendLine($"== {level.Level} {level.Name}: result={world.Result} after {world.Tick / GameWorld.TicksPerSecond / 60.0:0.0} min, " +
                              $"{sw.Elapsed.TotalMicroseconds / Math.Max(1, world.Tick):0} us/tick, captures={captures}");
                foreach (var team in world.ActiveTeams)
                {
                    var ships = world.Ships.Where(s => s.Team == team).ToList();
                    sb.AppendLine($"   {team,-10} planets={world.Planets.Count(p => p.Owner == team),2} " +
                                  $"kills={ships.Sum(s => s.Kills),3} deaths={ships.Sum(s => s.Deaths),3} " +
                                  $"captures={ships.Sum(s => s.Captures),2}{(world.Eliminated.Contains(team) ? "  ELIMINATED" : "")}");
                }
                sb.AppendLine($"   Neutral    planets={world.Planets.Count(p => p.Owner == Team.Neutral),2}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"== {level.Level} {level.Name}: EXCEPTION {ex}");
            }
        }
        return sb.ToString();
    }

    /// <summary>Renders the tactical and galaxy views of a mid-game world to PNG files (visual smoke test).</summary>
    public static void Snapshot(string folder)
    {
        var world = new GameWorld(new GameSettings { Seed = 7, PlayerAutopilot = true, Difficulty = 3 });
        while (world.Tick < 2400) world.Step();

        void Save(System.Windows.FrameworkElement view, int w, int h, string name)
        {
            view.Measure(new System.Windows.Size(w, h));
            view.Arrange(new System.Windows.Rect(0, 0, w, h));
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bmp.Render(view);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using var fs = System.IO.File.Create(System.IO.Path.Combine(folder, name));
            enc.Save(fs);
        }

        var tactical = new UI.TacticalView { World = world };
        Save(tactical, 1000, 700, "tactical.png");
        Save(new UI.GalaxyMapView { World = world, ViewRect = tactical.VisibleWorldRect }, 320, 320, "galaxy.png");
    }
}
