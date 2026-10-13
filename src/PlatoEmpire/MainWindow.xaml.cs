using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PlatoEmpire.Game;
using PlatoEmpire.UI;

namespace PlatoEmpire;

public sealed record MessageVm(string Text, Brush Brush);

public partial class MainWindow : Window
{
    const string Help =
        "CONTROLS\n" +
        "  Left click ............ set course toward the point (engages warp 4 if stopped)\n" +
        "  Right click / SPACE / T fire photon torpedo toward the mouse\n" +
        "  Shift+click / P ....... fire phasers toward the mouse\n" +
        "  0 - 9 ................. set warp speed        Up/Down  warp +1 / -1\n" +
        "  Left/Right ............ turn course 15 degrees\n" +
        "  S shields   O orbit planet   B bomb   U beam armies up   D beam armies down\n" +
        "  C cloak (Romulans only)\n" +
        "  + / - / mouse wheel ... zoom          Click the galaxy map to set a long-range course\n" +
        "  [ / ] ................. enemy difficulty  ESC pause   H help\n" +
        "  ENTER ................. type a PLATO-style command, e.g.\n" +
        "                          w 6   c 270   w 5 90   t 45   p 90   o   b   u   d   s   cloak   diff 4\n\n" +
        "HOW TO CONQUER THE GALAXY\n" +
        "  Every world holds armies. Orbit an enemy world and BOMB it down to 4 armies.\n" +
        "  Orbit one of your own worlds and BEAM UP armies (up to your ship's troop capacity).\n" +
        "  Return to the enemy world and BEAM DOWN: each army kills one defender, and once the\n" +
        "  defenders reach zero your next army captures the planet. Planets shoot at enemy ships,\n" +
        "  and orbiting your own planets refuels and repairs you. Destroyed ships respawn at a\n" +
        "  friendly world - an empire with no worlds and no ships has fallen.\n" +
        "  With GROW & SHRINK fleets (a setup option), fleet size follows the worlds you hold: lose\n" +
        "  worlds and lost ships aren't replaced; gain worlds and your shipyards build new ships.\n" +
        "  Your own ship always returns, but counts toward your empire's limit.\n" +
        "  WIN by toppling every rival, or by holding the conquest share of all worlds (chosen at\n" +
        "  setup) for 30 seconds. Rivals can win the same way - break their grip if they get close.\n\n" +
        "                                (click or press any key to close)";

    readonly GameSettings setup = new();
    readonly ObservableCollection<MessageVm> messages = [];
    readonly List<RadioButton> liveDiffButtons = [];
    GameWorld? world;
    TimeSpan lastRenderTime;
    double accumulator;
    bool paused;
    int frame;

    public MainWindow()
    {
        InitializeComponent();
        HelpText.Text = Help;
        MessageList.ItemsSource = messages;
        BuildSetup();
        BuildLiveDifficulty();

        Tactical.MouseLeftButtonDown += Tactical_MouseLeftButtonDown;
        Tactical.MouseRightButtonDown += (_, e) => { FireTorpAtMouse(); Tactical.Focus(); e.Handled = true; };
        Tactical.MouseMove += (_, e) => Tactical.MouseScreen = e.GetPosition(Tactical);
        Tactical.MouseLeave += (_, _) => Tactical.MouseScreen = null;
        Tactical.MouseWheel += (_, e) => Zoom(e.Delta > 0 ? 0.85 : 1 / 0.85);
        GalaxyMap.MapClicked += pos =>
        {
            if (world?.Player is { Alive: true } me) SteerToward(me, pos);
            Tactical.Focus();
        };

        CompositionTarget.Rendering += OnRendering;
    }

    // ------------------------------------------------------------------ setup screen

    void BuildSetup()
    {
        AddChoices(RacePanel, "race",
            RaceStats.Empires.Select(t => (RaceStats.For(t).Name.ToUpperInvariant(), t)),
            setup.PlayerTeam, t =>
            {
                setup.PlayerTeam = t;
                var r = RaceStats.For(t);
                RaceDesc.Text = $"{r.Description}  (max warp {r.MaxWarp})";
                RaceDesc.Foreground = Palette.TeamBrush(t);
            });
        AddChoices(OpponentPanel, "opp", Enumerable.Range(1, 3).Select(i => (i.ToString(), i)), setup.Opponents, i => setup.Opponents = i);
        AddChoices(ShipsPanel, "ships", Enumerable.Range(1, 8).Select(i => (i.ToString(), i)), setup.ShipsPerEmpire, i => setup.ShipsPerEmpire = i);
        AddChoices(AlliesPanel, "allies", Enumerable.Range(0, 8).Select(i => (i.ToString(), i)), setup.AlliedBots, i => setup.AlliedBots = i);
        AddChoices(ConquestPanel, "conquest",
            new[] { ("60%", 60), ("75%", 75), ("90%", 90), ("OFF - ELIMINATION ONLY", 0) },
            setup.ConquestPercent, pct => setup.ConquestPercent = pct);
        AddChoices(FleetLimitPanel, "fleet",
            new[] { ("FIXED (ORIGINAL)", FleetRule.Fixed), ("GROW & SHRINK", FleetRule.ScaleWithWorlds) },
            setup.FleetRule, rule => setup.FleetRule = rule);
        AddChoices(SetupDiffPanel, "sdiff",
            DifficultyProfile.Levels.Select(d => ($"{d.Level} {d.Name.ToUpperInvariant()}", d.Level)),
            setup.Difficulty, level =>
            {
                setup.Difficulty = level;
                SetupDiffDesc.Text = DifficultyProfile.Get(level).Description;
            });
    }

    void BuildLiveDifficulty()
    {
        foreach (var d in DifficultyProfile.Levels)
        {
            var rb = MakeChoice(d.Level.ToString(), "ldiff", false);
            rb.ToolTip = $"{d.Name}: {d.Description}";
            int level = d.Level;
            rb.Checked += (_, _) =>
            {
                LiveDiffDesc.Text = $"{d.Name.ToUpperInvariant()} - {d.Description}";
                world?.SetDifficulty(level);
                Tactical.Focus();
            };
            liveDiffButtons.Add(rb);
            LiveDiffPanel.Children.Add(rb);
        }
    }

    static RadioButton MakeChoice(string label, string group, bool isChecked) => new()
    {
        Content = label,
        GroupName = group,
        IsChecked = isChecked,
        Style = (Style)Application.Current.FindResource("Choice"),
    };

    static void AddChoices<T>(Panel panel, string group, IEnumerable<(string label, T value)> items, T selected, Action<T> onPick)
    {
        foreach (var (label, value) in items)
        {
            var rb = MakeChoice(label, group, EqualityComparer<T>.Default.Equals(value, selected));
            rb.Checked += (_, _) => onPick(value);
            panel.Children.Add(rb);
        }
        onPick(selected);
    }

    void Start_Click(object sender, RoutedEventArgs e) => StartGame();

    void NewGame_Click(object sender, RoutedEventArgs e)
    {
        GameOverOverlay.Visibility = Visibility.Collapsed;
        SetupOverlay.Visibility = Visibility.Visible;
    }

    void Help_Click(object sender, MouseButtonEventArgs e) => HelpOverlay.Visibility = Visibility.Collapsed;

    void StartGame()
    {
        if (world != null)
        {
            world.MessageAdded -= AddMessage;
            world.GameEnded -= OnGameEnded;
        }

        world = new GameWorld(setup.Clone());
        messages.Clear();
        foreach (var m in world.Messages) AddMessage(m);
        world.MessageAdded += AddMessage;
        world.GameEnded += OnGameEnded;

        Tactical.World = world;
        GalaxyMap.World = world;
        liveDiffButtons[world.Difficulty.Level - 1].IsChecked = true;

        SetupOverlay.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        HelpOverlay.Visibility = Visibility.Collapsed;
        paused = false;
        accumulator = 0;
        Tactical.Focus();
    }

    void OnGameEnded(GameResult result)
    {
        var me = world!.Player;
        GameOverTitle.Text = result == GameResult.Victory ? "V I C T O R Y" : "D E F E A T";
        GameOverText.Text =
            $"{world.EndReason}\n\n" +
            $"Game time {FormatTime(world.Tick)}   ·   Difficulty {world.Difficulty.Name}\n" +
            $"Kills {me.Kills}   ·   Planets captured {me.Captures}   ·   Ships lost {me.Deaths}";
        GameOverOverlay.Visibility = Visibility.Visible;
    }

    // ------------------------------------------------------------------ main loop

    void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (now == lastRenderTime) return;
        double dt = Math.Min(0.25, (now - lastRenderTime).TotalSeconds);
        lastRenderTime = now;

        if (world == null) return;
        bool running = !paused && world.Result == GameResult.None && SetupOverlay.Visibility != Visibility.Visible
                       && HelpOverlay.Visibility != Visibility.Visible;
        if (running)
        {
            accumulator += dt;
            const double step = 1.0 / GameWorld.TicksPerSecond;
            while (accumulator >= step)
            {
                world.Step();
                accumulator -= step;
            }
        }

        Tactical.Paused = paused;
        Tactical.InvalidateVisual();
        GalaxyMap.ViewRect = Tactical.VisibleWorldRect;
        GalaxyMap.InvalidateVisual();

        frame++;
        if (frame % 3 == 0) UpdateStatus();
        if (frame % 20 == 0) UpdateScoreboard();
    }

    static int ConquestSeconds(GameWorld w) => (int)Math.Ceiling(w.ConquestTicksRemaining / (double)GameWorld.TicksPerSecond);

    static string FormatTime(int ticks)
    {
        int secs = ticks / GameWorld.TicksPerSecond;
        return $"{secs / 60:00}:{secs % 60:00}";
    }

    void AddMessage(GameMessage m)
    {
        messages.Add(new MessageVm($"{FormatTime(m.Tick)}  {m.Text}", Palette.MessageBrush(m.Team)));
        while (messages.Count > 150) messages.RemoveAt(0);
        MessageScroll.ScrollToEnd();
    }

    void UpdateStatus()
    {
        if (world == null) return;
        var me = world.Player;
        var st = me.Stats;
        ShipTitle.Text = $"{me.Callsign}  {st.Name.ToUpperInvariant()} {me.Name.ToUpperInvariant()}";
        ShipTitle.Foreground = Palette.TeamBrush(me.Team);
        GameClock.Text = FormatTime(world.Tick);
        ShipLine1.Text = $"WARP {me.Warp:0.0} > {me.DesiredWarp:0} (max {me.EffectiveMaxWarp:0.#})   COURSE {me.Heading:000} > {me.DesiredHeading:000}";

        EnergyBar.Value = me.Energy / st.MaxEnergy;
        EnergyText.Text = $"{me.Energy:0}";
        HullBar.Value = 1 - me.HullFraction;
        HullText.Text = $"{100 * (1 - me.HullFraction):0}%";
        ShieldBar.Value = me.Shield / st.MaxShield;
        ShieldBar.Fill = me.ShieldsUp ? Palette.PlasmaBrush : Palette.PlasmaDim;
        ShieldText.Text = me.ShieldsUp ? "UP" : "DOWN";

        ShipLine2.Text = $"ARMIES {me.Armies}/{st.ArmyCapacity}   TORPS {st.MaxTorps - me.ActiveTorps}/{st.MaxTorps}   PHASER {(me.PhaserCooldown == 0 ? "READY" : "....")}\n" +
                         $"KILLS {me.Kills}   CAPTURES {me.Captures}   LOSSES {me.Deaths}";

        string status;
        if (!me.Alive)
            status = me.Stranded ? "NO FRIENDLY WORLDS - CANNOT RESPAWN" : $"DESTROYED - NEW SHIP IN {me.RespawnTimer / GameWorld.TicksPerSecond + 1}s";
        else if (me.Orbiting is { } p)
        {
            string action = me.Action switch
            {
                OrbitAction.Bomb => " - BOMBING",
                OrbitAction.BeamUp => " - BEAMING UP",
                OrbitAction.BeamDown => " - BEAMING DOWN",
                _ => "",
            };
            status = $"ORBITING {p.Name.ToUpperInvariant()} [{GameWorld.OwnerName(p).ToUpperInvariant()}, {p.Armies} ARMIES]{action}";
        }
        else
        {
            var near = world.Planets.MinBy(pl => (pl.Pos - me.Pos).LengthSquared)!;
            double d = (near.Pos - me.Pos).Length;
            status = d < GameWorld.OrbitEnterRange ? $"IN RANGE OF {near.Name.ToUpperInvariant()} - PRESS O TO ORBIT" :
                     me.DesiredWarp == 0 ? "ALL STOP - PRESS 1-9 FOR WARP" : $"NEAREST WORLD {near.Name.ToUpperInvariant()} @ {d:0}";
        }
        if (me.Cloaked) status += "   [CLOAKED]";
        ShipStatus.Text = status;
    }

    void UpdateScoreboard()
    {
        if (world == null) return;
        ScoreText.Inlines.Clear();
        ScoreText.Inlines.Add(new Run(world.Settings.FleetRule != FleetRule.Fixed
            ? "EMPIRE        WORLDS  SHIPS/MAX ARMIES\n"
            : "EMPIRE        WORLDS  SHIPS  ARMIES\n") { Foreground = Palette.PlasmaDim });
        foreach (var team in RaceStats.Empires.Where(world.ActiveTeams.Contains).Append(Team.Neutral))
        {
            var r = RaceStats.For(team);
            int worlds = world.Planets.Count(p => p.Owner == team);
            int armies = world.Planets.Where(p => p.Owner == team).Sum(p => p.Armies);
            var ships = world.Ships.Where(s => s.Team == team).ToList();
            string line = world.Eliminated.Contains(team)
                ? $"{r.Name.ToUpperInvariant(),-13} -- FALLEN --"
                : team == Team.Neutral
                    ? $"{r.Name.ToUpperInvariant(),-13} {worlds,6}      -  {armies,6}"
                    : $"{r.Name.ToUpperInvariant(),-13} {worlds,6}  {ships.Count(s => s.Alive),2}/{world.FleetCap(team),-2} {armies,6}";
            if (team == world.PlayerTeam) line += "  <";
            ScoreText.Inlines.Add(new Run(line + "\n") { Foreground = Palette.TeamBrush(team) });
        }

        if (world.ConquestEnabled)
        {
            ScoreText.Inlines.Add(new Run($"\nCONQUEST: HOLD {world.ConquestPlanetsNeeded} OF {world.Planets.Count} WORLDS\n") { Foreground = Palette.PlasmaDim });
            if (world.ConquestLeader is { } leader)
                ScoreText.Inlines.Add(new Run($"{RaceStats.For(leader).Plural.ToUpperInvariant()} WIN IN {ConquestSeconds(world)}s!\n")
                    { Foreground = Palette.TeamBrush(leader), FontWeight = FontWeights.Bold });
        }

        ScoreText.Inlines.Add(new Run("\nCAPTAIN             KILLS CAPT LOSS\n") { Foreground = Palette.PlasmaDim });
        foreach (var s in world.Ships.OrderByDescending(s => s.Kills * 2 + s.Captures * 3 - s.Deaths).Take(10))
        {
            string name = (s.IsPlayer ? "*" : " ") + s.Label;
            ScoreText.Inlines.Add(new Run($"{name,-20} {s.Kills,5} {s.Captures,4} {s.Deaths,4}\n") { Foreground = Palette.TeamBrush(s.Team) });
        }
    }

    // ------------------------------------------------------------------ input

    void Tactical_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Tactical.Focus();
        e.Handled = true;
        if (world?.Player is not { Alive: true } me) return;
        var target = Tactical.ScreenToWorld(e.GetPosition(Tactical));
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            world.FirePhaser(me, Geo.AngleOf(target - me.Pos));
        else
            SteerToward(me, target);
    }

    void SteerToward(Ship me, Vector target)
    {
        world!.SetCourse(me, Geo.AngleOf(target - me.Pos));
        if (me.DesiredWarp == 0 && me.Orbiting == null) world.SetWarp(me, Math.Min(4, me.Stats.MaxWarp));
    }

    double? MouseAim()
    {
        if (world?.Player is not { Alive: true } me || Tactical.MouseScreen is not { } m) return null;
        return Geo.AngleOf(Tactical.ScreenToWorld(m) - me.Pos);
    }

    void FireTorpAtMouse()
    {
        if (world != null && MouseAim() is { } aim) world.FireTorpedo(world.Player, aim);
    }

    void FirePhaserAtMouse()
    {
        if (world != null && MouseAim() is { } aim) world.FirePhaser(world.Player, aim);
    }

    void Zoom(double factor) => Tactical.ViewRadius = Math.Clamp(Tactical.ViewRadius * factor, 600, 4000);

    void ChangeDifficulty(int delta)
    {
        if (world == null) return;
        int level = Math.Clamp(world.Difficulty.Level + delta, 1, DifficultyProfile.Levels.Length);
        liveDiffButtons[level - 1].IsChecked = true;
    }

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (CommandBox.IsKeyboardFocusWithin)
        {
            if (e.Key == Key.Enter)
            {
                ExecuteCommand(CommandBox.Text);
                CommandBox.Clear();
                Tactical.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CommandBox.Clear();
                Tactical.Focus();
                e.Handled = true;
            }
            return;
        }

        if (SetupOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Enter) { StartGame(); e.Handled = true; }
            return;
        }
        if (HelpOverlay.Visibility == Visibility.Visible)
        {
            HelpOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
            return;
        }
        if (world == null || GameOverOverlay.Visibility == Visibility.Visible) return;

        var me = world.Player;
        e.Handled = true;
        switch (e.Key)
        {
            case >= Key.D0 and <= Key.D9:
                world.SetWarp(me, e.Key - Key.D0);
                break;
            case >= Key.NumPad0 and <= Key.NumPad9:
                world.SetWarp(me, e.Key - Key.NumPad0);
                break;
            case Key.Up:
                world.SetWarp(me, Math.Round(me.DesiredWarp) + 1);
                break;
            case Key.Down:
                world.SetWarp(me, Math.Max(0, Math.Round(me.DesiredWarp) - 1));
                break;
            case Key.Left:
                world.SetCourse(me, me.DesiredHeading - 15);
                break;
            case Key.Right:
                world.SetCourse(me, me.DesiredHeading + 15);
                break;
            case Key.Space:
            case Key.T:
                FireTorpAtMouse();
                break;
            case Key.P:
                FirePhaserAtMouse();
                break;
            case Key.S:
                world.SetShields(me, !me.ShieldsUp);
                break;
            case Key.O:
                world.EnterOrbit(me);
                break;
            case Key.B:
                world.SetAction(me, OrbitAction.Bomb);
                break;
            case Key.U:
                world.SetAction(me, OrbitAction.BeamUp);
                break;
            case Key.D:
                world.SetAction(me, OrbitAction.BeamDown);
                break;
            case Key.C:
                world.SetCloak(me, !me.Cloaked);
                break;
            case Key.OemPlus:
            case Key.Add:
                Zoom(0.85);
                break;
            case Key.OemMinus:
            case Key.Subtract:
                Zoom(1 / 0.85);
                break;
            case Key.OemOpenBrackets:
                ChangeDifficulty(-1);
                break;
            case Key.OemCloseBrackets:
                ChangeDifficulty(+1);
                break;
            case Key.Escape:
                paused = !paused;
                break;
            case Key.H:
            case Key.F1:
                HelpOverlay.Visibility = Visibility.Visible;
                break;
            case Key.Enter:
            case Key.OemQuestion:
                CommandBox.Focus();
                break;
            default:
                e.Handled = false;
                break;
        }
    }

    /// <summary>PLATO-style typed commands, e.g. "w 6", "c 270", "w 5 90", "t 45", "p 90".</summary>
    void ExecuteCommand(string text)
    {
        if (world == null) return;
        var me = world.Player;
        var parts = text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        double? Arg(int i) => parts.Length > i && double.TryParse(parts[i], out var v) ? v : null;

        switch (parts[0])
        {
            case "w" or "warp" when Arg(1) is { } warp:
                if (Arg(2) is { } heading) world.SetCourse(me, heading);
                world.SetWarp(me, warp);
                break;
            case "c" or "course" when Arg(1) is { } course:
                world.SetCourse(me, course);
                break;
            case "t" or "torp" or "torpedo" when Arg(1) is { } dir:
                world.FireTorpedo(me, dir);
                break;
            case "p" or "phaser" or "phasers" when Arg(1) is { } dir:
                world.FirePhaser(me, dir);
                break;
            case "s" or "shields" or "shield":
                world.SetShields(me, parts.Length > 1 ? parts[1] is "up" or "on" : !me.ShieldsUp);
                break;
            case "o" or "orbit":
                world.EnterOrbit(me);
                break;
            case "b" or "bomb":
                world.SetAction(me, OrbitAction.Bomb);
                break;
            case "u" or "up" or "beamup":
                world.SetAction(me, OrbitAction.BeamUp);
                break;
            case "d" or "down" or "beamdown":
                world.SetAction(me, OrbitAction.BeamDown);
                break;
            case "cloak":
                world.SetCloak(me, !me.Cloaked);
                break;
            case "diff" or "difficulty" when Arg(1) is { } level:
                ChangeDifficulty((int)level - world.Difficulty.Level);
                break;
            case "help" or "?":
                HelpOverlay.Visibility = Visibility.Visible;
                break;
            default:
                world.Post($"Unknown command '{text.Trim()}'. Try: w 6, c 270, t 45, p 90, o, b, u, d, s, help", null);
                break;
        }
    }
}
