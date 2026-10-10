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
    readonly Random rng;
    int thinkTimer;
    int side = 1;
    Ship? target;
    Planet? planet;
    Vector patrol;

    public BotMode Mode { get; private set; }

    public BotBrain(GameWorld world, Ship ship, int seed)
    {
        w = world;
        me = ship;
        rng = new Random(seed);
        thinkTimer = rng.Next(1, 20);
    }

    DifficultyProfile P => me.Team == w.PlayerTeam ? DifficultyProfile.Ally : w.Difficulty;

    public void Update()
    {
        if (!me.Alive)
        {
            Mode = BotMode.Idle;
            target = null;
            planet = null;
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
        NearestEnemy(out double threatDist);
        bool threatClose = threatDist < 650;

        // Stay committed to an orbital job unless threatened.
        if (!threatClose && planet != null && me.Orbiting == planet)
        {
            switch (Mode)
            {
                case BotMode.Repair when planet.Owner == me.Team && (hull > 0.08 || energy < 0.8):
                case BotMode.Ferry when planet.Owner == me.Team && me.Armies < me.Stats.ArmyCapacity && planet.Armies > 1:
                case BotMode.Bomb when planet.Owner != me.Team && planet.Armies > GameWorld.BombFloor && hull < p.RetreatHull:
                case BotMode.Capture when me.Armies > 0 && hull < p.RetreatHull:
                    return;
            }
        }

        if (hull > p.RetreatHull || energy < 0.12)
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
            var cp = Best(w.Planets.Where(pl => pl.Owner != me.Team),
                pl => Dist(pl.Pos) + pl.Armies * 350 + (pl.Owner == Team.Neutral ? 500 : 0));
            if (cp != null)
            {
                Mode = BotMode.Capture;
                planet = cp;
                return;
            }
        }

        if (p.StrategicCaptures || rng.NextDouble() < 0.35)
        {
            int canTake = Math.Max(GameWorld.BombFloor, me.Stats.ArmyCapacity - 1);
            if (w.Planets.Any(pl => pl.Owner != me.Team && pl.Armies <= canTake))
            {
                var src = NearestPlanet(pl => pl.Owner == me.Team && pl.Armies >= 4);
                if (src != null)
                {
                    Mode = BotMode.Ferry;
                    planet = src;
                    return;
                }
            }
        }

        var bomb = Best(w.Planets.Where(pl => pl.Owner != me.Team && pl.Armies > GameWorld.BombFloor),
            pl => Dist(pl.Pos) + pl.Armies * 60 + rng.Next(2500) + (pl.Owner == Team.Neutral ? 1500 : 0));
        if (bomb != null)
        {
            Mode = BotMode.Bomb;
            planet = bomb;
            return;
        }

        Mode = BotMode.Patrol;
        var enemyPlanets = w.Planets.Where(pl => pl.Owner != me.Team).ToList();
        patrol = enemyPlanets.Count > 0
            ? enemyPlanets[rng.Next(enemyPlanets.Count)].Pos + Geo.Dir(rng.Next(360)) * 600
            : new Vector(rng.Next(1000, 9000), rng.Next(1000, 9000));
    }

    Ship? PickEnemy()
    {
        var p = P;
        Ship? best = null;
        double bestScore = double.MaxValue;
        foreach (var s in w.Ships)
        {
            if (!s.Alive || s.Team == me.Team || !w.CanSee(me, s)) continue;
            double d = Dist(s.Pos);
            bool raidingUs = s.Orbiting?.Owner == me.Team;
            double reach = p.Level >= 4 && raidingUs ? 5000 : p.EngageRange;
            if (d > reach) continue;

            double score = d - s.HullFraction * 400;
            if (s.Armies > 0) score -= 800;
            if (s == me.LastAttacker && w.Tick - me.LastAttackTick < 90) score -= 500;
            if (raidingUs && p.Level >= 4) score -= 1500;
            if (score < bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

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

        if (me.Stats.CanCloak && p.UsesCloak)
        {
            bool travelling = Mode is BotMode.Ferry or BotMode.Capture or BotMode.Bomb or BotMode.Repair or BotMode.Patrol;
            bool wantCloak = travelling && me.Orbiting == null && td > 700 && me.Energy > me.Stats.MaxEnergy * 0.35;
            if (wantCloak != me.Cloaked) w.SetCloak(me, wantCloak);
        }

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
        double course, warp;
        if (d > 900)
        {
            course = P.LeadTargets ? Geo.LeadAngle(me.Pos, t.Pos, t.Velocity, max * GameWorld.WarpUnit) : bearing;
            warp = max;
        }
        else if (d > 400)
        {
            course = bearing + side * 30;
            warp = Math.Max(3, max * 0.65);
        }
        else
        {
            course = bearing + side * 110;
            warp = Math.Max(3, max * 0.75);
        }
        w.SetCourse(me, course);
        w.SetWarp(me, warp);
        TryShoot(t, d);
    }

    void TryShoot(Ship t, double d)
    {
        var p = P;
        var st = me.Stats;
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
