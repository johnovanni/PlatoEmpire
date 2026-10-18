using System.Windows;

namespace PlatoEmpire.Game;

/// <summary>
/// The whole simulation. Runs on a fixed 30 Hz tick; all ships — human or bot — act through
/// the same command methods so bots get no hidden advantages beyond their difficulty profile.
/// </summary>
public sealed class GameWorld
{
    public const double GalaxySize = 10000;
    public const double WarpUnit = 2.0;            // units per tick per warp factor
    public const int TicksPerSecond = 30;
    public const double OrbitRadius = 130;
    public const double OrbitEnterRange = 280;
    public const double OrbitAngularSpeed = 1.0;   // degrees per tick
    public const double ShipRadius = 22;
    public const double TorpedoHitRadius = ShipRadius + 6;  // a torpedo hits when its centre comes this close to a ship
    public const double CloakVisibility = 300;              // enemies closer than this can see a cloaked ship
    public const double CloakEngageCost = 150;              // energy spent to switch the cloak on
    public const double CloakDrainPerTick = 3.5;            // energy used every tick while cloaked (more than any ship regenerates)
    public const int OrbitActionInterval = 15;
    public const double TorpCost = 35;
    public const double PhaserCost = 80;
    public const int RespawnTicks = 120;
    public const int ShipyardTicks = 150;                   // one new ship per empire at most this often
    public const int BombFloor = 4;

    public GameSettings Settings { get; }
    public Random Rng { get; }
    public Team PlayerTeam { get; }
    public Ship Player { get; private set; } = null!;
    public List<Planet> Planets { get; } = [];
    public List<Ship> Ships { get; } = [];
    public List<Torpedo> Torpedoes { get; } = [];
    public List<Beam> Beams { get; } = [];
    public List<Explosion> Explosions { get; } = [];
    public List<GameMessage> Messages { get; } = [];
    public HashSet<Team> ActiveTeams { get; }
    public HashSet<Team> Eliminated { get; } = [];
    public DifficultyProfile Difficulty { get; private set; }
    public GameResult Result { get; private set; }
    public int Tick { get; private set; }
    public string EndReason { get; private set; } = "";

    /// <summary>Empire currently holding the conquest share of the galaxy, if any.</summary>
    public Team? ConquestLeader { get; private set; }
    int conquestStartTick;
    readonly Dictionary<Team, int> startingWorlds = [];
    readonly Dictionary<Team, int> startingFleet = [];
    readonly Dictionary<Team, int> shipsBuilt = [];

    public bool ConquestEnabled => Settings.ConquestPercent > 0;
    public int ConquestPercent => Math.Clamp(Settings.ConquestPercent, 51, 100);
    public int ConquestPlanetsNeeded => ConquestEnabled ? (int)Math.Ceiling(Planets.Count * ConquestPercent / 100.0) : int.MaxValue;
    public int ConquestTicksRemaining => ConquestLeader == null
        ? 0
        : Math.Max(0, Settings.ConquestHoldSeconds * TicksPerSecond - (Tick - conquestStartTick));

    public event Action<GameMessage>? MessageAdded;
    public event Action<GameResult>? GameEnded;

    public GameWorld(GameSettings settings)
    {
        Settings = settings;
        Rng = new Random(settings.Seed ?? Environment.TickCount);
        Difficulty = DifficultyProfile.Get(settings.Difficulty);
        PlayerTeam = settings.PlayerTeam;

        var rivals = RaceStats.Empires.Where(t => t != PlayerTeam)
            .OrderBy(_ => Rng.Next())
            .Take(Math.Clamp(settings.Opponents, 1, 3));
        ActiveTeams = new HashSet<Team>(rivals) { PlayerTeam };

        BuildGalaxy();
        foreach (var team in ActiveTeams) startingWorlds[team] = Planets.Count(p => p.Owner == team);
        BuildShips();

        var rivalNames = string.Join(", ", ActiveTeams.Where(t => t != PlayerTeam).Select(t => RaceStats.For(t).Plural));
        Post($"Captain, you command the {Player.Label}. Rival empires: {rivalNames}.", null);
        Post($"Difficulty: {Difficulty.Name}. " + (ConquestEnabled
            ? $"Win by holding {ConquestPlanetsNeeded} of {Planets.Count} worlds for {settings.ConquestHoldSeconds}s, or by destroying every rival."
            : "Win by destroying every rival empire.") + " (Press H for help.)", null);
    }

    // ------------------------------------------------------------------ setup

    void BuildGalaxy()
    {
        var quadrants = new Dictionary<Team, (Vector center, Vector corner)>
        {
            [Team.Federation] = (new Vector(2500, 7500), new Vector(-1, 1)),
            [Team.Klingon] = (new Vector(2500, 2500), new Vector(-1, -1)),
            [Team.Romulan] = (new Vector(7500, 2500), new Vector(1, -1)),
            [Team.Orion] = (new Vector(7500, 7500), new Vector(1, 1)),
        };

        foreach (var team in RaceStats.Empires)
        {
            bool active = ActiveTeams.Contains(team);
            var names = RaceStats.For(team).PlanetNames;
            var (center, corner) = quadrants[team];
            Team owner = active ? team : Team.Neutral;

            Planets.Add(new Planet(names[0], center + corner * 700, owner, team, active ? 30 : 14, true));
            for (int i = 1; i < names.Length; i++)
                Planets.Add(new Planet(names[i], FindSpot(center, 1900), owner, team, active ? Rng.Next(10, 17) : Rng.Next(6, 12), false));
        }

        var neutralNames = RaceStats.For(Team.Neutral).PlanetNames;
        var mid = new Vector(GalaxySize / 2, GalaxySize / 2);
        for (int i = 0; i < neutralNames.Length; i++)
        {
            var near = mid + Geo.Dir(i * 90 + Rng.Next(-15, 16)) * Rng.Next(800, 1200);
            Planets.Add(new Planet(neutralNames[i], FindSpot(near, 350), Team.Neutral, Team.Neutral, Rng.Next(5, 10), false));
        }
    }

    Vector FindSpot(Vector center, double spread)
    {
        Vector best = center;
        for (int attempt = 0; attempt < 600; attempt++)
        {
            double minDist = attempt < 300 ? 1000 : 700;
            var p = new Vector(
                Math.Clamp(center.X + (Rng.NextDouble() * 2 - 1) * spread, 600, GalaxySize - 600),
                Math.Clamp(center.Y + (Rng.NextDouble() * 2 - 1) * spread, 600, GalaxySize - 600));
            if (Planets.All(pl => (pl.Pos - p).Length >= minDist)) return p;
            best = p;
        }
        return best;
    }

    void BuildShips()
    {
        Player = AddShip(PlayerTeam, true);
        for (int i = 0; i < Math.Clamp(Settings.AlliedBots, 0, 7); i++) AddShip(PlayerTeam, false);
        foreach (var team in ActiveTeams.Where(t => t != PlayerTeam))
            for (int i = 0; i < Math.Clamp(Settings.ShipsPerEmpire, 1, 8); i++) AddShip(team, false);

        foreach (var team in ActiveTeams) startingFleet[team] = FleetSize(team);
        foreach (var s in Ships) TryRespawn(s, announce: false);
    }

    Ship AddShip(Team team, bool isPlayer)
    {
        int n = shipsBuilt.GetValueOrDefault(team) + 1;
        shipsBuilt[team] = n;
        var stats = RaceStats.For(team);
        var ship = new Ship(Ships.Count, team, $"{stats.Letter}{n}", stats.ShipName(n), isPlayer);
        if (!isPlayer || Settings.PlayerAutopilot) ship.Brain = new BotBrain(this, ship, Rng.Next());
        Ships.Add(ship);
        return ship;
    }

    // ------------------------------------------------------------------ messages

    public void Post(string text, Team? team)
    {
        var m = new GameMessage(text, team, Tick);
        Messages.Add(m);
        if (Messages.Count > 400) Messages.RemoveAt(0);
        MessageAdded?.Invoke(m);
    }

    /// <summary>Feedback meant only for the human captain.</summary>
    void Notify(Ship s, string text)
    {
        if (s.IsPlayer && !Settings.PlayerAutopilot) Post(text, null);
    }

    public static string OwnerName(Planet p) => RaceStats.For(p.Owner).Name;

    public void SetDifficulty(int level)
    {
        var next = DifficultyProfile.Get(level);
        if (next == Difficulty) return;
        Difficulty = next;
        Settings.Difficulty = next.Level;
        Post($"Enemy difficulty set to {next.Level} - {next.Name}: {next.Description}", null);
    }

    // ------------------------------------------------------------------ commands

    public void SetCourse(Ship s, double deg) => s.DesiredHeading = Geo.Norm(deg);

    public void SetWarp(Ship s, double warp)
    {
        s.DesiredWarp = Math.Clamp(warp, 0, s.Stats.MaxWarp);
        if (s.DesiredWarp > 0 && s.Orbiting != null) LeaveOrbit(s);
    }

    void LeaveOrbit(Ship s)
    {
        if (s.Orbiting is { } p) Notify(s, $"Leaving orbit of {p.Name}.");
        s.Orbiting = null;
        s.Action = OrbitAction.None;
    }

    public void SetShields(Ship s, bool up)
    {
        if (!s.Alive) return;
        if (up && s.Energy < 20) { Notify(s, "Insufficient energy for shields."); return; }
        s.ShieldsUp = up;
    }

    public void SetCloak(Ship s, bool on)
    {
        if (!s.Alive) return;
        if (!s.Stats.CanCloak) { Notify(s, "Only Romulan ships carry a cloaking device."); return; }
        if (on == s.Cloaked) return;
        if (on)
        {
            if (s.Energy < Math.Max(CloakEngageCost, 1)) { Notify(s, "Insufficient energy to cloak."); return; }
            s.Energy -= CloakEngageCost;
        }
        s.Cloaked = on;
        Notify(s, on ? "Cloaking device engaged." : "Cloak disengaged.");
    }

    public bool FireTorpedo(Ship s, double deg)
    {
        if (!s.Alive || s.TorpCooldown > 0) return false;
        if (s.ActiveTorps >= s.Stats.MaxTorps) { Notify(s, "All torpedo tubes are in use."); return false; }
        if (s.Energy < TorpCost) { Notify(s, "Insufficient energy for torpedoes."); return false; }
        if (s.Cloaked) s.Cloaked = false;

        s.Energy -= TorpCost;
        s.TorpCooldown = 7;
        s.ActiveTorps++;
        var dir = Geo.Dir(deg);
        Torpedoes.Add(new Torpedo
        {
            Owner = s,
            Team = s.Team,
            Pos = s.Pos + dir * TorpedoHitRadius,
            Vel = dir * s.Stats.TorpSpeed,
            Life = s.Stats.TorpLife,
            Damage = s.Stats.TorpDamage,
        });
        return true;
    }

    public bool FirePhaser(Ship s, double deg)
    {
        if (!s.Alive || s.PhaserCooldown > 0) return false;
        if (s.Energy < PhaserCost) { Notify(s, "Insufficient energy for phasers."); return false; }
        if (s.Cloaked) s.Cloaked = false;

        s.Energy -= PhaserCost;
        s.PhaserCooldown = 24;
        var dir = Geo.Dir(deg);
        double range = s.Stats.PhaserRange;

        Ship? best = null;
        double bestAlong = double.MaxValue;
        foreach (var t in Ships)
        {
            if (!t.Alive || t.Team == s.Team) continue;
            var rel = t.Pos - s.Pos;
            double along = rel * dir;
            if (along <= 0 || along > range) continue;
            double perp = Math.Abs(Vector.CrossProduct(dir, rel));
            if (perp > 32 + along * 0.04) continue;
            if (along < bestAlong) { best = t; bestAlong = along; }
        }

        var end = best?.Pos ?? s.Pos + dir * range;
        Beams.Add(new Beam(s.Pos, end, s.Team, 8, false));
        if (best != null)
            ApplyDamage(best, s.Stats.PhaserDamage * (1 - 0.6 * bestAlong / range), s, null);
        return true;
    }

    public bool EnterOrbit(Ship s)
    {
        if (!s.Alive) return false;
        if (s.Orbiting != null) return true;
        var p = Planets.MinBy(pl => (pl.Pos - s.Pos).LengthSquared)!;
        double d = (p.Pos - s.Pos).Length;
        if (d > OrbitEnterRange) { Notify(s, "No planet close enough to orbit."); return false; }
        if (s.Warp > 3.01) { Notify(s, "Too fast to orbit - slow to warp 3 or less."); return false; }

        s.Orbiting = p;
        s.OrbitAngle = Geo.AngleOf(s.Pos - p.Pos);
        s.OrbitDist = d;
        s.Warp = 0;
        s.DesiredWarp = 0;
        s.Action = OrbitAction.None;
        s.ActionTimer = 0;
        Notify(s, $"Standard orbit of {p.Name} ({OwnerName(p)}, {p.Armies} armies).");
        return true;
    }

    public void SetAction(Ship s, OrbitAction a)
    {
        if (a == OrbitAction.None) { s.Action = OrbitAction.None; return; }
        if (s.Orbiting is not { } p) { Notify(s, "You must be orbiting a planet first (O)."); return; }
        if (s.Action == a) { s.Action = OrbitAction.None; Notify(s, "Operation halted."); return; }

        switch (a)
        {
            case OrbitAction.Bomb when p.Owner == s.Team:
                Notify(s, "You can't bomb your own planet."); return;
            case OrbitAction.Bomb when p.Armies <= BombFloor:
                Notify(s, $"{p.Name} has only {p.Armies} armies - bombing can't reduce it further. Beam down troops!"); return;
            case OrbitAction.BeamUp when p.Owner != s.Team:
                Notify(s, "You can only beam up armies from your own planets."); return;
            case OrbitAction.BeamUp when s.Armies >= s.Stats.ArmyCapacity:
                Notify(s, "Troop bays are full."); return;
            case OrbitAction.BeamUp when p.Armies <= 1:
                Notify(s, $"{p.Name} has no armies to spare."); return;
            case OrbitAction.BeamDown when s.Armies == 0:
                Notify(s, "No armies aboard."); return;
        }

        s.Action = a;
        s.ActionTimer = 0;
        Notify(s, a switch
        {
            OrbitAction.Bomb => $"Bombing {p.Name}...",
            OrbitAction.BeamUp => $"Beaming armies up from {p.Name}...",
            _ => $"Beaming armies down to {p.Name}...",
        });
    }

    public bool CanSee(Ship observer, Ship target) =>
        !target.Cloaked || target.Team == observer.Team || (target.Pos - observer.Pos).Length < CloakVisibility;

    // ------------------------------------------------------------------ simulation

    public void Step()
    {
        if (Result != GameResult.None) return;
        Tick++;

        foreach (var s in Ships) s.Brain?.Update();
        foreach (var s in Ships) UpdateShip(s);
        UpdateTorpedoes();
        UpdatePlanets();

        foreach (var b in Beams) b.Life--;
        Beams.RemoveAll(b => b.Life <= 0);
        foreach (var e in Explosions) e.Age++;
        Explosions.RemoveAll(e => e.Age >= e.MaxAge);

        if (Tick % ShipyardTicks == 0) RunShipyards();
        if (Tick % 15 == 0) CheckVictory();
    }

    void UpdateShip(Ship s)
    {
        if (!s.Alive)
        {
            if (s.RespawnTimer > 1)
            {
                s.RespawnTimer--;
                return;
            }
            bool due = s.RespawnTimer == 1 || (s.Stranded && Tick % 30 == 0);
            if (!due) return;

            // A bot whose empire is at its fleet cap waits (timer parked at 1) until a slot frees up.
            // The player is never kept waiting, but does count toward the cap.
            bool hasWorlds = Planets.Any(p => p.Owner == s.Team);
            if (!s.IsPlayer && hasWorlds && AliveShips(s.Team) >= FleetCap(s.Team))
            {
                s.RespawnTimer = 1;
                return;
            }
            s.RespawnTimer = 0;
            TryRespawn(s, announce: true);
            return;
        }

        var st = s.Stats;
        if (s.PhaserCooldown > 0) s.PhaserCooldown--;
        if (s.TorpCooldown > 0) s.TorpCooldown--;

        if (s.Orbiting is { } p)
        {
            s.OrbitDist = Math.Max(OrbitRadius, s.OrbitDist - 5);
            s.OrbitAngle = Geo.Norm(s.OrbitAngle + OrbitAngularSpeed);
            s.Pos = p.Pos + Geo.Dir(s.OrbitAngle) * s.OrbitDist;
            s.Heading = Geo.Norm(s.OrbitAngle + 90);
            s.Warp = 0;
            if (++s.ActionTimer >= OrbitActionInterval)
            {
                s.ActionTimer = 0;
                DoOrbitAction(s, p);
            }
        }
        else
        {
            double turn = st.TurnRate * (1.3 - 0.6 * s.Warp / st.MaxWarp);
            s.Heading = Geo.Norm(s.Heading + Math.Clamp(Geo.Diff(s.Heading, s.DesiredHeading), -turn, turn));

            double target = Math.Min(s.DesiredWarp, s.EffectiveMaxWarp);
            if (s.Energy < 5) target = Math.Min(target, 1);
            s.Warp = s.Warp < target ? Math.Min(target, s.Warp + st.Accel) : Math.Max(target, s.Warp - st.Accel * 1.5);

            var next = s.Pos + Geo.Dir(s.Heading) * (s.Warp * WarpUnit);
            s.Pos = new Vector(Math.Clamp(next.X, 40, GalaxySize - 40), Math.Clamp(next.Y, 40, GalaxySize - 40));
            s.Energy -= 0.035 * s.Warp * s.Warp;
        }

        bool friendlyOrbit = s.Orbiting?.Owner == s.Team;
        s.Energy += st.EnergyRegen * (friendlyOrbit ? 3 : 1);
        if (s.ShieldsUp) s.Energy -= 0.7;
        if (s.Cloaked) s.Energy -= CloakDrainPerTick;
        if (s.Energy <= 0)
        {
            s.Energy = 0;
            if (s.ShieldsUp || s.Cloaked) Notify(s, "Energy depleted! Shields and cloak are down.");
            s.ShieldsUp = false;
            s.Cloaked = false;
        }
        s.Energy = Math.Min(s.Energy, st.MaxEnergy);
        s.Shield = Math.Min(st.MaxShield, s.Shield + (s.ShieldsUp ? 0.12 : 0.3));

        double repair = friendlyOrbit ? 0.18 : s.Warp < 0.5 && !s.ShieldsUp ? 0.06 : 0.02;
        s.Hull = Math.Max(0, s.Hull - repair);
    }

    void DoOrbitAction(Ship s, Planet p)
    {
        switch (s.Action)
        {
            case OrbitAction.Bomb:
                if (p.Owner == s.Team || p.Armies <= BombFloor)
                {
                    s.Action = OrbitAction.None;
                    if (p.Owner != s.Team) Notify(s, $"{p.Name} bombed down to {p.Armies} armies. Beam down troops to capture it.");
                    break;
                }
                p.Armies--;
                AlertPlanetAttacked(p, s);
                break;

            case OrbitAction.BeamUp:
                if (p.Owner != s.Team || p.Armies <= 1 || s.Armies >= s.Stats.ArmyCapacity)
                {
                    s.Action = OrbitAction.None;
                    Notify(s, $"Beam-up complete: {s.Armies} armies aboard.");
                    break;
                }
                p.Armies--;
                s.Armies++;
                break;

            case OrbitAction.BeamDown:
                if (s.Armies <= 0)
                {
                    s.Action = OrbitAction.None;
                    Notify(s, "All armies beamed down.");
                    break;
                }
                s.Armies--;
                if (p.Owner == s.Team) p.Armies++;
                else if (p.Armies > 0) { p.Armies--; AlertPlanetAttacked(p, s); }
                else Capture(p, s);
                break;
        }
    }

    void AlertPlanetAttacked(Planet p, Ship attacker)
    {
        if (p.Owner != PlayerTeam || Tick - p.LastAlertTick < 10 * TicksPerSecond) return;
        p.LastAlertTick = Tick;
        Post($"{p.Name} is under attack by {attacker.Label} ({attacker.Stats.Plural})!", attacker.Team);
    }

    void Capture(Planet p, Ship s)
    {
        var old = p.Owner;
        int playerCapBefore = FleetCap(PlayerTeam);
        p.Owner = s.Team;
        p.Armies = 1;
        p.RegenTimer = 0;
        s.Captures++;
        Post($"{p.Name} captured from the {RaceStats.For(old).Plural} by {s.Label} ({s.Stats.Plural})!", s.Team);

        int playerCap = FleetCap(PlayerTeam);
        if (playerCap != playerCapBefore)
            Post($"Fleet limit: your {Planets.Count(pl => pl.Owner == PlayerTeam)} worlds support {ShipCount(playerCap)}.", null);
    }

    static string ShipCount(int n) => n == 1 ? "1 ship" : $"{n} ships";

    /// <summary>All ships ever commissioned for <paramref name="team"/>, flying or not.</summary>
    public int FleetSize(Team team) => Ships.Count(s => s.Team == team);

    public int AliveShips(Team team) => Ships.Count(s => s.Team == team && s.Alive);

    /// <summary>
    /// How many ships <paramref name="team"/> may have flying: its starting fleet scaled by worlds held versus worlds
    /// it started with, rounded up so any empire with a world keeps at least one ship.
    /// </summary>
    public int FleetCap(Team team)
    {
        int fleet = startingFleet.GetValueOrDefault(team);
        int start = startingWorlds.GetValueOrDefault(team);
        if (Settings.FleetRule == FleetRule.Fixed || start == 0) return fleet;
        return (int)Math.Ceiling(fleet * (double)Planets.Count(p => p.Owner == team) / start);
    }

    /// <summary>
    /// Under <see cref="FleetRule.ScaleWithWorlds"/>, an empire whose fleet is below its limit builds one new ship
    /// every <see cref="ShipyardTicks"/>. The fleet counts every ship the empire owns, flying or not: destroyed ships
    /// waiting to return, including stranded ones whose empire had no worlds, come back instead of being replaced
    /// (ISS-005).
    /// </summary>
    void RunShipyards()
    {
        if (Settings.FleetRule != FleetRule.ScaleWithWorlds) return;
        foreach (var team in ActiveTeams)
        {
            if (Eliminated.Contains(team) || !Planets.Any(p => p.Owner == team)) continue;
            int cap = FleetCap(team);
            if (FleetSize(team) >= cap) continue;

            var ship = AddShip(team, false);
            TryRespawn(ship, announce: false);
            if (team == PlayerTeam)
                Post($"New ship {ship.Label} commissioned - your {Planets.Count(p => p.Owner == team)} worlds support {ShipCount(cap)}.", team);
        }
    }

    void UpdateTorpedoes()
    {
        foreach (var t in Torpedoes)
        {
            t.Pos += t.Vel;
            if (--t.Life <= 0 || t.Pos.X < 0 || t.Pos.Y < 0 || t.Pos.X > GalaxySize || t.Pos.Y > GalaxySize)
            {
                t.Dead = true;
                continue;
            }
            foreach (var s in Ships)
            {
                if (!s.Alive || s.Team == t.Team) continue;
                if ((s.Pos - t.Pos).LengthSquared > TorpedoHitRadius * TorpedoHitRadius) continue;
                t.Dead = true;
                Explosions.Add(new Explosion(t.Pos, 12, 35, t.Team));
                ApplyDamage(s, t.Damage, t.Owner, null);
                break;
            }
        }
        foreach (var t in Torpedoes)
            if (t.Dead) t.Owner.ActiveTorps--;
        Torpedoes.RemoveAll(t => t.Dead);
    }

    void UpdatePlanets()
    {
        foreach (var p in Planets)
        {
            if (p.Owner != Team.Neutral)
            {
                int interval = p.IsHome && p.Origin == p.Owner ? 150 : 300;
                int cap = p.IsHome ? 40 : 25;
                if (++p.RegenTimer >= interval)
                {
                    p.RegenTimer = 0;
                    if (p.Armies < cap) p.Armies++;
                }
            }
            else if (p.Armies > 0 && ++p.RegenTimer >= 600)
            {
                p.RegenTimer = 0;
                if (p.Armies < 12) p.Armies++;
            }

            if (p.FireCooldown > 0) { p.FireCooldown--; continue; }
            if (p.Armies <= 0) continue;

            double range = p.Owner == Team.Neutral ? 380 : 520;
            Ship? target = null;
            double best = range;
            foreach (var s in Ships)
            {
                if (!s.Alive || s.Team == p.Owner) continue;
                if (s.Cloaked && s.Orbiting != p) continue;
                double d = (s.Pos - p.Pos).Length;
                if (d < best) { best = d; target = s; }
            }
            if (target == null) continue;

            p.FireCooldown = 30;
            Beams.Add(new Beam(p.Pos, target.Pos, p.Owner, 7, true));
            ApplyDamage(target, Math.Min(15, 3 + p.Armies * 0.3), null, p);
        }
    }

    public void ApplyDamage(Ship target, double dmg, Ship? attacker, Planet? planet)
    {
        if (!target.Alive || dmg <= 0) return;
        if (target.ShieldsUp && target.Shield > 0)
        {
            double absorbed = Math.Min(target.Shield, dmg * 0.85);
            target.Shield -= absorbed;
            dmg -= absorbed;
        }
        target.Hull += dmg;

        bool hostile = attacker != null && attacker.Team != target.Team;
        if (hostile)
        {
            target.LastAttacker = attacker;
            target.LastAttackTick = Tick;
            target.Brain?.OnAttacked(attacker!);
        }
        if (target.Hull >= target.Stats.MaxHull) Destroy(target, hostile ? attacker : null, planet);
    }

    void Destroy(Ship s, Ship? killer, Planet? planet)
    {
        s.Alive = false;
        s.Deaths++;
        s.Orbiting = null;
        s.Action = OrbitAction.None;
        s.ShieldsUp = false;
        s.Cloaked = false;
        s.Warp = 0;
        s.DesiredWarp = 0;
        int lostArmies = s.Armies;
        s.Armies = 0;
        s.RespawnTimer = RespawnTicks;
        Explosions.Add(new Explosion(s.Pos, 40, 170, s.Team));

        killer ??= Tick - s.LastAttackTick < 5 * TicksPerSecond ? s.LastAttacker : null;
        if (killer != null && killer.Team != s.Team) killer.Kills++;

        string armies = lostArmies > 0 ? $" ({lostArmies} armies lost)" : "";
        if (killer != null)
            Post($"{s.Label} ({s.Stats.Name}) destroyed by {killer.Label}{armies}.", killer.Team);
        else if (planet != null)
            Post($"{s.Label} ({s.Stats.Name}) destroyed by the defenses of {planet.Name}{armies}.", planet.Owner);
        else
            Post($"{s.Label} ({s.Stats.Name}) was destroyed{armies}.", s.Team);

        // Warp core breach damages anything nearby.
        foreach (var o in Ships)
        {
            if (!o.Alive || o == s) continue;
            double d = (o.Pos - s.Pos).Length;
            if (d < 170) ApplyDamage(o, 25 * (1 - d / 170), o.Team != s.Team ? s : null, null);
        }
    }

    void TryRespawn(Ship s, bool announce)
    {
        var options = Planets.Where(p => p.Owner == s.Team).ToList();
        if (options.Count == 0)
        {
            s.Stranded = true;
            s.RespawnTimer = 0;
            if (announce && s.IsPlayer) Notify(s, "Your empire holds no planets - you cannot rejoin the battle.");
            return;
        }

        var home = options.FirstOrDefault(p => p.IsHome && p.Origin == s.Team);
        var at = s.IsPlayer && home != null ? home : options[Rng.Next(options.Count)];
        s.Stranded = false;
        s.Alive = true;
        s.Pos = at.Pos + Geo.Dir(Rng.Next(360)) * (OrbitRadius * 1.8);
        s.Heading = s.DesiredHeading = Rng.Next(360);
        s.Warp = s.DesiredWarp = 0;
        s.Energy = s.Stats.MaxEnergy;
        s.Hull = 0;
        s.Shield = s.Stats.MaxShield;
        s.ShieldsUp = false;
        s.Cloaked = false;
        s.Armies = 0;
        s.Orbiting = null;
        s.Action = OrbitAction.None;
        s.PhaserCooldown = s.TorpCooldown = 0;
        s.LastAttacker = null;
        if (announce) Notify(s, $"New ship commissioned at {at.Name}.");
    }

    void CheckVictory()
    {
        foreach (var team in ActiveTeams)
        {
            if (Eliminated.Contains(team)) continue;
            bool hasPlanets = Planets.Any(p => p.Owner == team);
            bool hasShips = Ships.Any(s => s.Team == team && s.Alive);
            if (hasPlanets || hasShips) continue;
            Eliminated.Add(team);
            Post($"*** The {RaceStats.For(team).Name} empire has fallen! ***", team);
        }

        if (Eliminated.Contains(PlayerTeam) || (!Player.Alive && Player.Stranded))
            End(GameResult.Defeat, "Your empire has been destroyed.");
        else if (ActiveTeams.All(t => t == PlayerTeam || Eliminated.Contains(t)))
            End(GameResult.Victory, "Every rival empire has fallen.");
        else
            CheckConquest();
    }

    /// <summary>An empire holding the conquest share of all worlds for the hold time wins outright.</summary>
    void CheckConquest()
    {
        if (!ConquestEnabled) return;
        int needed = ConquestPlanetsNeeded;
        var (team, held) = ActiveTeams.Where(t => !Eliminated.Contains(t))
            .Select(t => (t, Planets.Count(p => p.Owner == t)))
            .MaxBy(x => x.Item2);
        string plural = RaceStats.For(team).Plural;

        if (held < needed)
        {
            if (ConquestLeader is { } lost)
                Post(lost == PlayerTeam
                    ? "Your grip on the galaxy has slipped - the conquest countdown is reset."
                    : $"The {RaceStats.For(lost).Plural}' grip on the galaxy is broken!", lost);
            ConquestLeader = null;
            return;
        }

        if (ConquestLeader != team)
        {
            ConquestLeader = team;
            conquestStartTick = Tick;
            Post(team == PlayerTeam
                ? $"Your empire holds {held} of {Planets.Count} worlds! Keep at least {needed} for {Settings.ConquestHoldSeconds}s to win."
                : $"WARNING: the {plural} hold {held} of {Planets.Count} worlds! Take some back within {Settings.ConquestHoldSeconds}s or the galaxy is lost.", team);
            return;
        }

        if (ConquestTicksRemaining <= 0)
            End(team == PlayerTeam ? GameResult.Victory : GameResult.Defeat,
                team == PlayerTeam
                    ? $"Your empire conquered {held} of {Planets.Count} worlds."
                    : $"The {plural} conquered {held} of {Planets.Count} worlds.");
    }

    void End(GameResult result, string reason)
    {
        Result = result;
        EndReason = reason;
        Post(result == GameResult.Victory ? $"*** VICTORY! {reason} ***" : $"*** DEFEAT. {reason} ***", null);
        GameEnded?.Invoke(result);
    }
}
