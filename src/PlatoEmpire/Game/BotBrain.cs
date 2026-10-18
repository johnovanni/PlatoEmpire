using System.Windows;

namespace PlatoEmpire.Game;

public enum BotMode { Idle, Fight, Repair, Bomb, Ferry, Capture, Patrol }

/// <summary>
/// Computer captain. Re-plans every few ticks (faster at higher difficulty) and steers,
/// fires and manages shields every tick using the same commands a human uses.
/// </summary>
public sealed class BotBrain
{
    readonly GameWorld w;
    readonly Ship me;
    readonly RaceDoctrine baseDoctrine;
    RaceDoctrine? scaledDoctrine;
    double scaledFor = -1;
    readonly Random rng;
    int thinkTimer;
    int side = 1;
    bool breakingOff;
    Ship? target;
    Planet? planet;
    Vector patrol;

    public BotMode Mode { get; private set; }

    /// <summary>The enemy ship this bot is fighting, if any.</summary>
    public Ship? Target => target;

    /// <summary>The planet this bot is heading for or working at, if any.</summary>
    public Planet? Destination => planet;

    /// <summary>Runs a planning step immediately (tests).</summary>
    internal void ThinkNow() => Think();

    /// <summary>Gives the bot a job directly (tests).</summary>
    internal void Assign(BotMode mode, Planet? destination, Ship? enemy)
    {
        Mode = mode;
        planet = destination;
        target = enemy;
    }

    public BotBrain(GameWorld world, Ship ship, int seed)
    {
        w = world;
        me = ship;
        baseDoctrine = RaceDoctrine.For(ship.Team);
        rng = new Random(seed);
        thinkTimer = rng.Next(1, 20);
    }

    DifficultyProfile P => me.Team == w.PlayerTeam ? DifficultyProfile.Ally : w.Difficulty;

    /// <summary>The race's doctrine, applied as fully as the difficulty level allows (ISS-002).</summary>
    RaceDoctrine doctrine
    {
        get
        {
            double strength = P.DoctrineStrength;
            if (strength != scaledFor)
            {
                scaledDoctrine = baseDoctrine.Scaled(strength);
                scaledFor = strength;
            }
            return scaledDoctrine!;
        }
    }

    /// <summary>Other living bots on this bot's side.</summary>
    IEnumerable<Ship> Allies() => w.Ships.Where(s => s != me && s.Alive && s.Team == me.Team && s.Brain != null);

    /// <summary>At Commander and above: a rival close to winning by conquest, whose worlds and ships come first.</summary>
    Team? ConquestThreat => P.RespondsToConquest && w.ConquestLeader is { } leader && leader != me.Team ? leader : null;

    /// <summary>Worlds worth attacking: every enemy world, or only the conquest leader's while it threatens to win.</summary>
    IEnumerable<Planet> CampaignTargets()
    {
        var enemyWorlds = w.Planets.Where(pl => pl.Owner != me.Team);
        return ConquestThreat is { } leader && w.Planets.Any(pl => pl.Owner == leader)
            ? enemyWorlds.Where(pl => pl.Owner == leader)
            : enemyWorlds;
    }

    public void Update()
    {
        if (!me.Alive)
        {
            Mode = BotMode.Idle;
            target = null;
            planet = null;
            breakingOff = false;
            return;
        }
        if (--thinkTimer <= 0)
        {
            Think();
            thinkTimer = P.ReactionTicks + rng.Next(P.ReactionTicks / 2 + 1);
        }
        Act();
    }

    public void OnAttacked(Ship attacker)
    {
        if (Mode != BotMode.Fight && w.CanSee(me, attacker))
            thinkTimer = Math.Min(thinkTimer, P.ReactionTicks / 2 + 1);
    }

    // ------------------------------------------------------------------ planning

    void Think()
    {
        var p = P;
        double hull = me.HullFraction, energy = me.Energy / me.Stats.MaxEnergy;
        double retreatAt = Math.Clamp(p.RetreatHull + doctrine.RetreatOffset, 0.3, 0.95);
        NearestEnemy(out double threatDist);
        bool threatClose = threatDist < 650;

        // Stay committed to an orbital job unless threatened.
        if (!threatClose && planet != null && me.Orbiting == planet)
        {
            switch (Mode)
            {
                case BotMode.Repair when planet.Owner == me.Team && (hull > 0.08 || energy < 0.8):
                case BotMode.Ferry when planet.Owner == me.Team && me.Armies < me.Stats.ArmyCapacity && planet.Armies > 1:
                case BotMode.Bomb when planet.Owner != me.Team && planet.Armies > GameWorld.BombFloor && hull < retreatAt:
                case BotMode.Capture when me.Armies > 0 && hull < retreatAt:
                    return;
            }
        }

        // Light ships (Orion doctrine) also break off a fight once their shields are spent, and stay broken off
        // until they're back above 60%; otherwise they'd flip between fighting and fleeing every planning step.
        double shields = me.Shield / me.Stats.MaxShield;
        if (doctrine.BreakOffShields > 0 && Mode == BotMode.Fight && threatDist < 1200 && shields < doctrine.BreakOffShields)
            breakingOff = true;
        else if (breakingOff && shields >= 0.6)
            breakingOff = false;

        if (hull > retreatAt || energy < 0.12 || breakingOff)
        {
            var refuge = NearestPlanet(pl => pl.Owner == me.Team);
            if (refuge != null)
            {
                Mode = BotMode.Repair;
                planet = refuge;
                target = null;
                return;
            }
        }

        var enemy = PickEnemy();
        if (enemy != null)
        {
            double d = (enemy.Pos - me.Pos).Length;
            if (me.Armies == 0 || d < 900 || enemy.Armies > 0)
            {
                if (target != enemy) side = rng.Next(2) * 2 - 1;
                Mode = BotMode.Fight;
                target = enemy;
                return;
            }
        }
        target = null;

        if (me.Armies > 0)
        {
            // Admirals land their armies on a world an ally is bombing, completing a coordinated invasion.
            var alliesBombing = P.CoordinatesInvasions
                ? Allies().Where(a => a.Brain!.Mode == BotMode.Bomb && a.Brain.Destination is { } d && d.Owner != me.Team)
                    .Select(a => a.Brain!.Destination!).ToList()
                : [];
            var candidates = alliesBombing.Count > 0 ? alliesBombing : CampaignTargets();
            var cp = Best(candidates,
                pl => Dist(pl.Pos) + pl.Armies * 350 + (pl.Owner == Team.Neutral ? 500 : 0) + RaidPenalty(pl));
            if (cp != null)
            {
                Mode = BotMode.Capture;
                planet = cp;
                return;
            }
        }

        // Keep an unfinished bombing or ferry job while it's still valid, rather than re-deciding (with random
        // factors) on every planning step and never finishing anything. A conquest threat overrides old bombing jobs.
        int canTake = Math.Max(GameWorld.BombFloor, me.Stats.ArmyCapacity - 1);
        if (Mode == BotMode.Bomb && planet is { } bombing && bombing.Owner != me.Team && bombing.Armies > GameWorld.BombFloor
            && (ConquestThreat is not { } threat || bombing.Owner == threat))
            return;
        if (Mode == BotMode.Ferry && planet is { } source && source.Owner == me.Team && source.Armies >= 4
            && me.Armies < me.Stats.ArmyCapacity && w.Planets.Any(pl => pl.Owner != me.Team && pl.Armies <= canTake))
            return;

        // Admirals coordinate: while more allies are bombing than are bringing armies, go and fetch armies.
        if (P.CoordinatesInvasions)
        {
            var allies = Allies().ToList();
            int bombers = allies.Count(a => a.Brain!.Mode == BotMode.Bomb);
            int carriers = allies.Count(a => a.Brain!.Mode is BotMode.Ferry or BotMode.Capture || a.Armies > 0);
            if (bombers > carriers && NearestPlanet(pl => pl.Owner == me.Team && pl.Armies >= 4) is { } armySource)
            {
                Mode = BotMode.Ferry;
                planet = armySource;
                return;
            }
        }

        // Ferrying armies and bombing are both options; the race's doctrine decides how often to ferry when both
        // are available. Lower difficulty levels ferry half as often.
        var ferrySource = w.Planets.Any(pl => pl.Owner != me.Team && pl.Armies <= canTake)
            ? NearestPlanet(pl => pl.Owner == me.Team && pl.Armies >= 4)
            : null;
        var bomb = Best(CampaignTargets().Where(pl => pl.Armies > GameWorld.BombFloor),
            pl => Dist(pl.Pos) + pl.Armies * 60 + rng.Next(2500) + (pl.Owner == Team.Neutral ? 1500 : 0) + RaidPenalty(pl));
        double ferryChance = p.StrategicCaptures ? doctrine.FerryChance : doctrine.FerryChance * 0.5;
        bool ferry = ferrySource != null && (bomb == null || rng.NextDouble() < ferryChance);

        if (ferry)
        {
            Mode = BotMode.Ferry;
            planet = ferrySource;
            return;
        }
        if (bomb != null)
        {
            Mode = BotMode.Bomb;
            planet = bomb;
            return;
        }

        Mode = BotMode.Patrol;
        var enemyPlanets = w.Planets.Where(pl => pl.Owner != me.Team).ToList();
        var spot = enemyPlanets.Count > 0
            ? enemyPlanets[rng.Next(enemyPlanets.Count)].Pos + Geo.Dir(rng.Next(360)) * 600
            : new Vector(rng.Next(1000, 9000), rng.Next(1000, 9000));
        // Patrol points near the edge would be unreachable once edge avoidance turns the ship away.
        patrol = new Vector(Math.Clamp(spot.X, 1000, GameWorld.GalaxySize - 1000), Math.Clamp(spot.Y, 1000, GameWorld.GalaxySize - 1000));
    }

    Ship? PickEnemy()
    {
        var p = P;
        var dc = doctrine;
        Ship? best = null;
        double bestScore = double.MaxValue;
        foreach (var s in w.Ships)
        {
            if (!s.Alive || s.Team == me.Team || !w.CanSee(me, s)) continue;
            double d = Dist(s.Pos);
            bool raidingUs = s.Orbiting?.Owner == me.Team;
            double reach = p.Level >= 4 && raidingUs ? 5000 : p.EngageRange * dc.EngageRangeScale;
            if (d > reach) continue;

            // Doctrine filters: light ships skip healthy, better-armed targets; slow ships don't chase faster ones.
            if (dc.AvoidsEvenFights && s.Armies == 0 && !raidingUs && s.HullFraction < 0.3
                && Firepower(s) > Firepower(me) * 1.05) continue;
            if (dc.ChaseLimit > 0 && !raidingUs && s.Stats.MaxWarp > me.Stats.MaxWarp && d > dc.ChaseLimit && PullingAway(s)) continue;

            double score = d - s.HullFraction * dc.DamagedTargetWeight;
            if (s.Armies > 0) score -= dc.CarrierTargetWeight;
            if (s == me.LastAttacker && w.Tick - me.LastAttackTick < 90) score -= 500;
            if (raidingUs) score -= dc.DefendBonus + (p.Level >= 4 ? 1500 : 0);
            if (dc.LoneChasePenalty > 0 && FarFromFriends(s.Pos, 2500)) score += dc.LoneChasePenalty;
            if (dc.OpenSpacePenalty > 0 && w.Planets.All(pl => (pl.Pos - s.Pos).Length > 2000)) score += dc.OpenSpacePenalty;

            // Commander and above: gang up on the enemy allies are already attacking, on damaged ships and carriers,
            // and on the ships of an empire close to winning by conquest.
            if (p.FocusFire)
            {
                score -= 400 * Math.Min(3, Allies().Count(a => a.Brain!.Target == s));
                score -= s.HullFraction * 400;
                if (s.Armies > 0) score -= 400;
            }
            if (ConquestThreat is { } leader && s.Team == leader) score -= 600;

            if (score < bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    static double Firepower(Ship s) => s.Stats.PhaserDamage + s.Stats.TorpDamage;

    /// <summary>True when the distance to <paramref name="s"/> is growing.</summary>
    bool PullingAway(Ship s) => (s.Velocity - me.Velocity) * (s.Pos - me.Pos) > 0;

    /// <summary>True when no friendly world or other friendly ship is within <paramref name="radius"/> of a point.</summary>
    bool FarFromFriends(Vector pos, double radius) =>
        w.Planets.All(pl => pl.Owner != me.Team || (pl.Pos - pos).Length > radius)
        && w.Ships.All(s => s == me || !s.Alive || s.Team != me.Team || (s.Pos - pos).Length > radius);

    /// <summary>Raiders (Orion doctrine) avoid worlds with enemy ships nearby, so they hit the poorly defended rear.</summary>
    double RaidPenalty(Planet pl) => !doctrine.RaidsRear
        ? 0
        : 1500 * w.Ships.Count(s => s.Alive && s.Team != me.Team && w.CanSee(me, s) && (s.Pos - pl.Pos).Length < 1500);

    Ship? NearestEnemy(out double dist)
    {
        Ship? best = null;
        dist = double.MaxValue;
        foreach (var s in w.Ships)
        {
            if (!s.Alive || s.Team == me.Team || !w.CanSee(me, s)) continue;
            double d = Dist(s.Pos);
            if (d < dist) { dist = d; best = s; }
        }
        return best;
    }

    Planet? NearestPlanet(Func<Planet, bool> filter) => Best(w.Planets.Where(filter), pl => Dist(pl.Pos));

    static Planet? Best(IEnumerable<Planet> planets, Func<Planet, double> score) =>
        planets.Select(pl => (pl, s: score(pl))).OrderBy(x => x.s).Select(x => x.pl).FirstOrDefault();

    double Dist(Vector v) => (v - me.Pos).Length;

    // ------------------------------------------------------------------ acting

    void Act()
    {
        var p = P;
        var threat = NearestEnemy(out double td);
        var torp = IncomingTorp();

        bool wantShields = p.SmartShields ? td < 1100 || torp != null : Mode == BotMode.Fight;
        if (Mode == BotMode.Repair && me.Orbiting != null && td > 1100 && torp == null) wantShields = false;
        if (me.ShieldsUp != wantShields) w.SetShields(me, wantShields);

        switch (Mode)
        {
            case BotMode.Fight:
                Fight();
                break;
            case BotMode.Repair:
            case BotMode.Bomb:
            case BotMode.Ferry:
            case BotMode.Capture:
                GoToPlanet();
                break;
            case BotMode.Patrol:
                w.SetCourse(me, Geo.AngleOf(patrol - me.Pos));
                w.SetWarp(me, me.EffectiveMaxWarp * 0.7);
                if (Dist(patrol) < 400) thinkTimer = 0;
                break;
        }

        if (Mode != BotMode.Fight && threat != null && td < 900) TryShoot(threat, td);
        if (p.Dodges && torp != null && me.Orbiting == null) Dodge(torp);

        // Decided after moving and firing so orbit and weapon state are current for this tick.
        if (me.Stats.CanCloak && p.UsesCloak) ManageCloak(td);

        // Applied last so it overrides every other steering decision, including dodges. Checks the ship's actual
        // heading too: a ship still turning can be flying at the edge even while it wants to go somewhere safe.
        if (me.Orbiting == null)
        {
            double speed = Math.Max(me.Warp, me.DesiredWarp) * GameWorld.WarpUnit;
            double course = AvoidEdges(me.Pos, me.DesiredHeading, speed);
            double fromActual = AvoidEdges(me.Pos, me.Heading, speed);
            if (fromActual != me.Heading) course = fromActual;
            w.SetCourse(me, course);
        }
    }

    /// <summary>
    /// Cloaks while travelling, but only when something could see or shoot the ship: an enemy ship within
    /// 2,500 units (yet beyond close range) or a hostile planet within 1,000. Engaging is expensive, so every
    /// on/off threshold has a gap (energy 80% / 30%, enemy distance 700 / 500, watch radius 2,500 / 3,500 for
    /// ships and 1,000 / 1,500 for planets) to avoid paying to re-engage over and over. The bot also won't
    /// engage right after firing, which drops the cloak.
    /// </summary>
    void ManageCloak(double enemyDist)
    {
        if (Mode == BotMode.Idle) return;   // momentary state between plans; don't pay to re-engage after it
        bool travelling = Mode is BotMode.Ferry or BotMode.Capture or BotMode.Bomb or BotMode.Repair or BotMode.Patrol;
        double energy = me.Energy / me.Stats.MaxEnergy;
        bool Watched(double shipRadius, double planetRadius) => enemyDist < shipRadius
            || w.Planets.Any(pl => pl.Owner != me.Team && pl.Armies > 0 && Dist(pl.Pos) < planetRadius);
        bool justFired = me.TorpCooldown > 0 || me.PhaserCooldown > 0;
        // About to orbit, where the cloak comes off: near the destination, or near any world it may switch plans to.
        bool almostThere = (planet != null && Dist(planet.Pos) < 900) || w.Planets.Any(pl => Dist(pl.Pos) < 600);

        // Already cloaked when a fight starts: stay cloaked and close in for an ambush (see TryShoot).
        bool ambushing = Mode == BotMode.Fight;
        bool wantCloak = me.Cloaked
            ? me.Orbiting == null && energy > 0.3
              && (ambushing || (travelling && Watched(3500, 1500) && enemyDist > 500))
            : travelling && me.Orbiting == null && Watched(2500, 1000) && enemyDist > 700 && energy > 0.8
              && !justFired && !almostThere;
        if (wantCloak != me.Cloaked) w.SetCloak(me, wantCloak);
    }

    /// <summary>A cloaked bot holds fire until its target is this close.</summary>
    public const double AmbushRange = 500;

    /// <summary>How close to the edge of the galaxy a bot is willing to fly.</summary>
    public const double EdgeBuffer = 150;

    /// <summary>
    /// Looks ahead along <paramref name="course"/> (further at higher <paramref name="speed"/>, in units per tick)
    /// and, if that point would come within <see cref="EdgeBuffer"/> of the edge, returns a course back toward
    /// open space instead. Courses that run parallel to the edge or stop short of it are left unchanged.
    /// </summary>
    public static double AvoidEdges(Vector pos, double course, double speed)
    {
        const double size = GameWorld.GalaxySize;
        double lookahead = 250 + speed * 25;
        var ahead = pos + Geo.Dir(course) * lookahead;
        if (ahead.X >= EdgeBuffer && ahead.X <= size - EdgeBuffer && ahead.Y >= EdgeBuffer && ahead.Y <= size - EdgeBuffer)
            return course;

        // Pull the look-ahead point in far enough that the new course points back inward on every offending axis.
        double inner = EdgeBuffer + lookahead;
        var safe = new Vector(Math.Clamp(ahead.X, inner, size - inner), Math.Clamp(ahead.Y, inner, size - inner));
        return Geo.AngleOf(safe - pos);
    }

    void Fight()
    {
        if (target is not { Alive: true } t || !w.CanSee(me, t))
        {
            thinkTimer = 0;
            return;
        }
        var rel = t.Pos - me.Pos;
        double d = rel.Length, bearing = Geo.AngleOf(rel), max = me.EffectiveMaxWarp;
        bool targetFaster = t.Stats.MaxWarp > me.Stats.MaxWarp;
        bool targetAttackingUs = t.Orbiting?.Owner == me.Team;

        // Slow ships (Romulan doctrine) give up on faster ships that pull away.
        if (doctrine.ChaseLimit > 0 && targetFaster && d > doctrine.ChaseLimit && !targetAttackingUs && PullingAway(t))
        {
            target = null;
            Mode = BotMode.Idle;
            thinkTimer = 0;
            return;
        }

        double course, warp;
        if (doctrine.HitAndRun && t.Stats.MaxWarp < me.Stats.MaxWarp)
        {
            // Hold just outside the slower ship's phaser range and harass it with torpedoes.
            // Slows and spirals in on approach so it doesn't overshoot into the target's range.
            double standoff = t.Stats.PhaserRange + 80;
            if (d < standoff) { course = bearing + 180 + side * 30; warp = max; }
            else if (d > standoff + 600) { course = bearing + side * 20; warp = max * 0.8; }
            else if (d > standoff + 150) { course = bearing + side * 60; warp = Math.Max(3, max * 0.6); }
            else { course = bearing + side * 100; warp = Math.Max(3, max * 0.7); }
        }
        else
        {
            // Close in, then circle at the doctrine's preferred fighting distance.
            double range = doctrine.FightRange;
            if (d > range + 500)
            {
                course = P.LeadTargets ? Geo.LeadAngle(me.Pos, t.Pos, t.Velocity, max * GameWorld.WarpUnit) : bearing;
                warp = max;
            }
            else if (d > range)
            {
                course = bearing + side * 30;
                warp = Math.Max(3, max * 0.65);
            }
            else
            {
                course = bearing + side * 110;
                warp = Math.Max(3, max * 0.75);
            }
        }
        w.SetCourse(me, course);
        w.SetWarp(me, warp);
        TryShoot(t, d);
    }

    void TryShoot(Ship t, double d)
    {
        var p = P;
        var st = me.Stats;
        if (me.Cloaked && d > AmbushRange) return;   // firing drops the cloak, so hold fire until close
        double torpRange = st.TorpSpeed * st.TorpLife * 0.8;
        if (d < torpRange && me.Energy > 150 && rng.NextDouble() < p.FireChance)
        {
            double aim = p.LeadTargets ? Geo.LeadAngle(me.Pos, t.Pos, t.Velocity, st.TorpSpeed) : Geo.AngleOf(t.Pos - me.Pos);
            w.FireTorpedo(me, aim + Geo.Gaussian(rng) * p.AimErrorDeg);
        }
        if (p.UsesPhasers && d < st.PhaserRange * 0.8 && me.PhaserCooldown == 0 && me.Energy > 300 && rng.NextDouble() < p.FireChance * 1.5)
            w.FirePhaser(me, Geo.AngleOf(t.Pos - me.Pos) + Geo.Gaussian(rng) * p.AimErrorDeg * 0.35);
    }

    void GoToPlanet()
    {
        if (planet == null)
        {
            thinkTimer = 0;
            return;
        }
        if (me.Orbiting == planet)
        {
            WorkOrbit(planet);
            return;
        }

        var rel = planet.Pos - me.Pos;
        double d = rel.Length, course = Geo.AngleOf(rel), max = me.EffectiveMaxWarp;
        w.SetCourse(me, course);
        double warp = d > 2200 ? max : d > 1000 ? Math.Min(max, 5) : d > 450 ? 3 : 2;
        if (Math.Abs(Geo.Diff(me.Heading, course)) > 70 && d < 1000) warp = Math.Min(warp, 2);
        w.SetWarp(me, warp);
        if (d < GameWorld.OrbitEnterRange - 30 && me.Warp <= 3) w.EnterOrbit(me);
    }

    void WorkOrbit(Planet pl)
    {
        switch (Mode)
        {
            case BotMode.Repair:
                Ensure(OrbitAction.None);
                if (pl.Owner != me.Team || (me.HullFraction < 0.05 && me.Energy > me.Stats.MaxEnergy * 0.9)) thinkTimer = 0;
                break;

            case BotMode.Bomb:
                if (pl.Owner == me.Team || pl.Armies <= GameWorld.BombFloor) Done();
                else Ensure(OrbitAction.Bomb);
                break;

            case BotMode.Ferry:
                if (pl.Owner != me.Team || me.Armies >= me.Stats.ArmyCapacity || pl.Armies <= 1) Done();
                else Ensure(OrbitAction.BeamUp);
                break;

            case BotMode.Capture:
                if (me.Armies == 0) Done();
                else if (pl.Owner != me.Team && pl.Armies > GameWorld.BombFloor && pl.Armies >= me.Armies) Ensure(OrbitAction.Bomb);
                else if (pl.Owner == me.Team && pl.Armies >= 8) Done();
                else Ensure(OrbitAction.BeamDown);
                break;
        }
    }

    void Done()
    {
        Ensure(OrbitAction.None);
        Mode = BotMode.Idle;
        thinkTimer = 0;
    }

    void Ensure(OrbitAction a)
    {
        if (me.Action != a) w.SetAction(me, a);
    }

    Torpedo? IncomingTorp()
    {
        foreach (var t in w.Torpedoes)
        {
            if (t.Team == me.Team) continue;
            var rel = me.Pos - t.Pos;
            double len = rel.Length;
            if (len > 320 || len < 1) continue;
            if (rel * t.Vel / (len * t.Vel.Length) > 0.97) return t;
        }
        return null;
    }

    void Dodge(Torpedo t)
    {
        double away = Vector.CrossProduct(t.Vel, me.Pos - t.Pos) > 0 ? 90 : -90;
        w.SetCourse(me, Geo.AngleOf(t.Vel) + away);
        w.SetWarp(me, me.EffectiveMaxWarp);
    }
}
