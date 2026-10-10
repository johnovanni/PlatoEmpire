namespace PlatoEmpire.Game;

public enum Team { Federation = 0, Klingon = 1, Romulan = 2, Orion = 3, Neutral = 4 }

/// <summary>Per-race ship characteristics. Each empire flies a different hull, as in the PLATO original.</summary>
public sealed class RaceStats
{
    public required string Name { get; init; }
    public required string Plural { get; init; }
    public required char Letter { get; init; }
    public required string Description { get; init; }
    public required string[] ShipNames { get; init; }
    public required string[] PlanetNames { get; init; }
    public int MaxWarp { get; init; }
    public double TurnRate { get; init; }       // degrees per tick
    public double Accel { get; init; }          // warp per tick
    public double MaxEnergy { get; init; }
    public double EnergyRegen { get; init; }    // per tick
    public double MaxHull { get; init; }
    public double MaxShield { get; init; }
    public double PhaserDamage { get; init; }
    public double PhaserRange { get; init; }
    public double TorpDamage { get; init; }
    public double TorpSpeed { get; init; }      // units per tick
    public int TorpLife { get; init; }          // ticks
    public int MaxTorps { get; init; }
    public int ArmyCapacity { get; init; }
    public bool CanCloak { get; init; }

    public static readonly Team[] Empires = [Team.Federation, Team.Klingon, Team.Romulan, Team.Orion];

    static readonly Dictionary<Team, RaceStats> All = new()
    {
        [Team.Federation] = new RaceStats
        {
            Name = "Federation", Plural = "Federation", Letter = 'F',
            Description = "Balanced heavy cruiser. Strong shields, good phasers, carries 6 armies.",
            ShipNames = ["Enterprise", "Excalibur", "Lexington", "Hood", "Potemkin", "Yorktown", "Intrepid", "Constellation"],
            PlanetNames = ["Earth", "Vulcan", "Andor", "Tellar", "Centauri"],
            MaxWarp = 8, TurnRate = 6, Accel = 0.15, MaxEnergy = 1000, EnergyRegen = 2.4,
            MaxHull = 100, MaxShield = 100, PhaserDamage = 32, PhaserRange = 700,
            TorpDamage = 22, TorpSpeed = 28, TorpLife = 50, MaxTorps = 8, ArmyCapacity = 6,
        },
        [Team.Klingon] = new RaceStats
        {
            Name = "Klingon", Plural = "Klingons", Letter = 'K',
            Description = "Battlecruiser. Tough hull and the hardest-hitting phasers, weaker shields.",
            ShipNames = ["Gr'oth", "Klothos", "Devisor", "Fek'lhr", "Kor", "Kang", "Koloth", "Kumara"],
            PlanetNames = ["Klinzhai", "Kaleb", "Morath", "Tarsus", "Hakon"],
            MaxWarp = 8, TurnRate = 7, Accel = 0.18, MaxEnergy = 900, EnergyRegen = 2.2,
            MaxHull = 115, MaxShield = 80, PhaserDamage = 38, PhaserRange = 650,
            TorpDamage = 22, TorpSpeed = 28, TorpLife = 45, MaxTorps = 8, ArmyCapacity = 6,
        },
        [Team.Romulan] = new RaceStats
        {
            Name = "Romulan", Plural = "Romulans", Letter = 'R',
            Description = "Bird of Prey. Slow, but fires heavy plasma torpedoes and can cloak (C).",
            ShipNames = ["Praetor", "Vreenak", "Decius", "Khazara", "Haakona", "Terix", "Belak", "Valdore"],
            PlanetNames = ["Romulus", "Remus", "Achernar", "Eridani", "Aldebaran"],
            MaxWarp = 7, TurnRate = 5, Accel = 0.12, MaxEnergy = 1100, EnergyRegen = 2.5,
            MaxHull = 95, MaxShield = 110, PhaserDamage = 26, PhaserRange = 600,
            TorpDamage = 32, TorpSpeed = 24, TorpLife = 55, MaxTorps = 6, ArmyCapacity = 5, CanCloak = true,
        },
        [Team.Orion] = new RaceStats
        {
            Name = "Orion", Plural = "Orions", Letter = 'O',
            Description = "Pirate raider. Fastest and most agile, lightly built, carries 8 armies.",
            ShipNames = ["Corsair", "Marauder", "Raider", "Buccaneer", "Privateer", "Freebooter", "Brigand", "Rover"],
            PlanetNames = ["Orion", "Rigel", "Antares", "Betelgeuse", "Deneb"],
            MaxWarp = 9, TurnRate = 8, Accel = 0.22, MaxEnergy = 850, EnergyRegen = 2.6,
            MaxHull = 85, MaxShield = 85, PhaserDamage = 30, PhaserRange = 700,
            TorpDamage = 20, TorpSpeed = 30, TorpLife = 45, MaxTorps = 8, ArmyCapacity = 8,
        },
        [Team.Neutral] = new RaceStats
        {
            Name = "Independent", Plural = "Independents", Letter = 'I',
            Description = "Unaligned worlds.",
            ShipNames = ["-"], PlanetNames = ["Organia", "Sherman", "Capella", "Altair"],
        },
    };

    public static RaceStats For(Team t) => All[t];
}
