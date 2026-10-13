using System.Windows;

namespace PlatoEmpire.Game;

public enum OrbitAction { None, Bomb, BeamUp, BeamDown }

public sealed class Planet(string name, Vector pos, Team owner, Team origin, int armies, bool isHome)
{
    public string Name { get; } = name;
    public Vector Pos { get; } = pos;
    public Team Owner { get; set; } = owner;
    public Team Origin { get; } = origin;
    public bool IsHome { get; } = isHome;
    public int Armies { get; set; } = armies;
    public int RegenTimer;
    public int FireCooldown;
    public int LastAlertTick = -9999;
}

public sealed class Ship
{
    public Ship(int id, Team team, string callsign, string name, bool isPlayer)
    {
        Id = id;
        Team = team;
        Stats = RaceStats.For(team);
        Callsign = callsign;
        Name = name;
        IsPlayer = isPlayer;
    }

    public int Id { get; }
    public Team Team { get; }
    public RaceStats Stats { get; }
    public string Callsign { get; }
    public string Name { get; }
    public bool IsPlayer { get; }
    public BotBrain? Brain { get; set; }

    public Vector Pos;
    public double Heading;
    public double DesiredHeading;
    public double Warp;
    public double DesiredWarp;

    public double Energy;
    public double Hull;       // accumulated damage; destroyed at Stats.MaxHull
    public double Shield;
    public bool ShieldsUp;
    public bool Cloaked;

    public int Armies;
    public int Kills, Deaths, Captures;

    public bool Alive;
    public int RespawnTimer;
    public bool Stranded;     // no friendly planet to respawn at

    public Planet? Orbiting;
    public double OrbitAngle;
    public double OrbitDist;
    public OrbitAction Action;
    public int ActionTimer;

    public int PhaserCooldown, TorpCooldown, ActiveTorps;
    public Ship? LastAttacker;
    public int LastAttackTick = -9999;

    public double HullFraction => Hull / Stats.MaxHull;
    public double EffectiveMaxWarp => Math.Max(1, Stats.MaxWarp * (1 - 0.5 * HullFraction));
    public string Label => $"{Callsign} {Name}";

    public Vector Velocity => Orbiting != null
        ? Geo.Dir(Heading) * (OrbitDist * Math.PI / 180 * GameWorld.OrbitAngularSpeed)
        : Geo.Dir(Heading) * (Warp * GameWorld.WarpUnit);
}

public sealed class Torpedo
{
    public Vector Pos;
    public Vector Vel;
    public int Life;
    public required Ship Owner { get; init; }
    public Team Team { get; init; }
    public double Damage { get; init; }
    public bool Dead;
}

public sealed class Beam(Vector from, Vector to, Team team, int life, bool fromPlanet)
{
    public Vector From { get; } = from;
    public Vector To { get; } = to;
    public Team Team { get; } = team;
    public int MaxLife { get; } = life;
    public bool FromPlanet { get; } = fromPlanet;
    public int Life = life;
}

public sealed class Explosion(Vector pos, int maxAge, double radius, Team team)
{
    public Vector Pos { get; } = pos;
    public int MaxAge { get; } = maxAge;
    public double Radius { get; } = radius;
    public Team Team { get; } = team;
    public int Age;
}

public sealed record GameMessage(string Text, Team? Team, int Tick);

public enum GameResult { None, Victory, Defeat }

/// <summary>
/// Fleet size versus worlds held (compared with the empire's starting count). In every rule, ships already
/// flying are never removed; the rule only decides which lost ships are replaced and whether new ones are built.
/// </summary>
public enum FleetRule
{
    /// <summary>Every lost ship is replaced; fleets never change size.</summary>
    Fixed,
    /// <summary>The limit tracks worlds held in both directions; shipyards build new ships up to it.</summary>
    ScaleWithWorlds,
}

public sealed class GameSettings
{
    public Team PlayerTeam { get; set; } = Team.Federation;
    public int Opponents { get; set; } = 3;
    public int ShipsPerEmpire { get; set; } = 4;
    public int AlliedBots { get; set; } = 3;
    public int Difficulty { get; set; } = 3;
    /// <summary>Share of all worlds an empire must hold to win by conquest; 0 = elimination only. Clamped to 51-100.</summary>
    public int ConquestPercent { get; set; } = 60;
    /// <summary>How long the conquest share must be held continuously.</summary>
    public int ConquestHoldSeconds { get; set; } = 30;
    /// <summary>How an empire's fleet size responds to the worlds it holds.</summary>
    public FleetRule FleetRule { get; set; } = FleetRule.Fixed;
    public int? Seed { get; set; }
    public bool PlayerAutopilot { get; set; }   // used by --selftest

    public GameSettings Clone() => (GameSettings)MemberwiseClone();
}
