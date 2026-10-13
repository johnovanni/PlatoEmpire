using System.Windows;
using PlatoEmpire.Game;

namespace PlatoEmpire.Tests;

/// <summary>Builders for deterministic, controllable game worlds.</summary>
internal static class TestWorld
{
    /// <summary>A small seeded world: Federation player vs one rival empire with one ship, no allies.</summary>
    public static GameWorld Create(Action<GameSettings>? configure = null)
    {
        var settings = new GameSettings
        {
            Seed = 42,
            PlayerTeam = Team.Federation,
            Opponents = 1,
            ShipsPerEmpire = 1,
            AlliedBots = 0,
            Difficulty = 3,
            // Pinned so other tests are never disturbed by ships being built or held back, whatever the default.
            FleetRule = FleetRule.Fixed,
        };
        configure?.Invoke(settings);
        return new GameWorld(settings);
    }

    /// <summary>Like <see cref="Create"/> but every bot is switched off so tests fully control the action.</summary>
    public static GameWorld Quiet(Action<GameSettings>? configure = null)
    {
        var w = Create(configure);
        foreach (var s in w.Ships) s.Brain = null;
        return w;
    }

    public static Team Rival(GameWorld w) => w.ActiveTeams.First(t => t != w.PlayerTeam);

    public static Ship Enemy(GameWorld w) => w.Ships.First(s => s.Team != w.PlayerTeam);

    /// <summary>A non-home planet currently owned by <paramref name="team"/>.</summary>
    public static Planet ColonyOf(GameWorld w, Team team) => w.Planets.First(p => p.Owner == team && !p.IsHome);

    /// <summary>The point in the galaxy furthest from any planet (out of reach of planetary defenses).</summary>
    public static Vector DeepSpace(GameWorld w)
    {
        Vector best = default;
        double bestDist = -1;
        for (double x = 1000; x <= 9000; x += 250)
        for (double y = 1000; y <= 9000; y += 250)
        {
            var p = new Vector(x, y);
            double d = w.Planets.Min(pl => (pl.Pos - p).Length);
            if (d > bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    public static void Park(Ship s, Vector pos, double heading = 0)
    {
        s.Orbiting = null;
        s.Action = OrbitAction.None;
        s.Pos = pos;
        s.Heading = s.DesiredHeading = heading;
        s.Warp = s.DesiredWarp = 0;
    }

    /// <summary>Takes a ship out of the game without triggering a respawn.</summary>
    public static void Remove(Ship s)
    {
        s.Alive = false;
        s.RespawnTimer = 0;
        s.Stranded = false;
    }

    /// <summary>Puts a ship into orbit of <paramref name="p"/>.</summary>
    public static void Orbit(GameWorld w, Ship s, Planet p)
    {
        Park(s, p.Pos + new Vector(0, -GameWorld.OrbitRadius));
        Assert.True(w.EnterOrbit(s));
    }

    public static void Run(GameWorld w, int ticks)
    {
        for (int i = 0; i < ticks; i++) w.Step();
    }

    /// <summary>Gives the player's team <paramref name="playerWorlds"/> planets, the rival one, and the rest to independents.</summary>
    public static void AssignWorlds(GameWorld w, int playerWorlds, Team? holder = null)
    {
        var owner = holder ?? w.PlayerTeam;
        var other = owner == w.PlayerTeam ? Rival(w) : w.PlayerTeam;
        for (int i = 0; i < w.Planets.Count; i++)
        {
            var p = w.Planets[i];
            p.Owner = i < playerWorlds ? owner : i == w.Planets.Count - 1 ? other : Team.Neutral;
            p.RegenTimer = 0;
        }
    }
}
