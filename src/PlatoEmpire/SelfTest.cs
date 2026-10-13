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

    sealed record Outcome(bool Finished, bool ByConquest, double Minutes, int Eliminated, int Captures, int PeakWorlds, int PeakShips);

    /// <summary>
    /// Compares rules: the same seeds and difficulty levels played under each fleet rule.
    /// Reports how many games finish within the time limit, how they ended, and how close leaders got.
    /// </summary>
    public static string Compare(int seedsPerLevel = 6, int minutes = 40)
    {
        int maxTicks = GameWorld.TicksPerSecond * 60 * minutes;
        var sb = new StringBuilder();
        sb.AppendLine($"Bot-only games, 4 empires x 4 ships, {seedsPerLevel} seeds per level, {minutes}-minute limit, conquest at 60%.");
        sb.AppendLine();
        sb.AppendLine("fleet rule  level       finished  conquest  elim'd  avg-min  captures  peak-worlds  peak-ships");

        foreach (var rule in new[] { FleetRule.Fixed, FleetRule.ScaleWithWorlds })
        {
            var all = new List<Outcome>();
            foreach (var level in DifficultyProfile.Levels)
            {
                var games = Enumerable.Range(1, seedsPerLevel).Select(seed => Play(rule, level.Level, seed, maxTicks)).ToList();
                all.AddRange(games);
                sb.AppendLine(Row(Label(rule), $"{level.Level} {level.Name}", games));
            }
            sb.AppendLine(Row(Label(rule), "ALL", all));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    static string Label(FleetRule rule) => rule == FleetRule.Fixed ? "fixed" : "grow&shrink";

    static Outcome Play(FleetRule rule, int level, int seed, int maxTicks)
    {
        var world = new GameWorld(new GameSettings
        {
            PlayerTeam = Team.Federation, Opponents = 3, ShipsPerEmpire = 4, AlliedBots = 3,
            Difficulty = level, Seed = seed * 100 + level, PlayerAutopilot = true, FleetRule = rule,
        });
        int captures = 0, peak = 0, peakShips = 0;
        world.MessageAdded += m => { if (m.Text.Contains(" captured from ")) captures++; };
        while (world.Result == GameResult.None && world.Tick < maxTicks)
        {
            world.Step();
            if (world.Tick % 30 == 0)
            {
                peak = Math.Max(peak, world.ActiveTeams.Max(t => world.Planets.Count(p => p.Owner == t)));
                peakShips = Math.Max(peakShips, world.ActiveTeams.Max(world.AliveShips));
            }
        }
        bool finished = world.Result != GameResult.None;
        return new Outcome(finished, finished && world.EndReason.Contains("conquered"),
            world.Tick / (double)GameWorld.TicksPerSecond / 60, world.Eliminated.Count, captures, peak, peakShips);
    }

    static string Row(string rule, string label, List<Outcome> games)
    {
        var done = games.Where(g => g.Finished).ToList();
        string avgMin = done.Count > 0 ? done.Average(g => g.Minutes).ToString("0.0") : "-";
        return $"{rule,-11} {label,-12} {done.Count,3}/{games.Count,-4} {games.Count(g => g.ByConquest),6}  " +
               $"{games.Average(g => g.Eliminated),6:0.0}  {avgMin,7}  {games.Average(g => g.Captures),8:0.0}  {games.Average(g => g.PeakWorlds),11:0.0}  {games.Average(g => g.PeakShips),10:0.0}";
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

        foreach (var (radius, name) in new[] { (600.0, "tactical-zoomed-in.png"), (4000.0, "tactical-zoomed-out.png") })
            Save(new UI.TacticalView { World = world, ViewRadius = radius }, 1000, 700, name);
        var tactical = new UI.TacticalView { World = world };
        Save(tactical, 1000, 700, "tactical.png");
        Save(new UI.GalaxyMapView { World = world, ViewRect = tactical.VisibleWorldRect }, 320, 320, "galaxy.png");
    }
}
