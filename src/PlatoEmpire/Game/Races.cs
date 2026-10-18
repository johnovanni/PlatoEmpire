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
            Description = "Balanced heavy cruiser. Medium speed and firepower, strong shields, carries 6 armies.",
            ShipNames = ["Enterprise", "Excalibur", "Lexington", "Hood", "Potemkin", "Yorktown", "Intrepid", "Constellation",
                         "Constitution", "Defiant", "Exeter", "Farragut", "Republic", "Valiant", "Kongo", "Eagle",
                         "Endeavour", "Essex", "Saratoga", "Reliant", "Ahwahnee", "Bozeman", "Challenger", "Columbia",
                         "Discovery", "Excelsior", "Gettysburg", "Hathaway", "Horatio", "Lakota", "Melbourne", "Merrimac",
                         "Monitor", "Pegasus", "Princeton", "Stargazer", "Sutherland", "Thunderchild", "Venture"],
            PlanetNames = ["Earth", "Vulcan", "Andor", "Tellar", "Centauri"],
            MaxWarp = 8, TurnRate = 6, Accel = 0.15, MaxEnergy = 1000, EnergyRegen = 2.4,
            MaxHull = 100, MaxShield = 100, PhaserDamage = 32, PhaserRange = 700,
            TorpDamage = 22, TorpSpeed = 28, TorpLife = 50, MaxTorps = 8, ArmyCapacity = 6,
        },
        [Team.Klingon] = new RaceStats
        {
            Name = "Klingon", Plural = "Klingons", Letter = 'K',
            Description = "Battlecruiser. Federation-class speed and firepower; nimbler, with lighter shields.",
            ShipNames = ["Gr'oth", "Klothos", "Devisor", "Fek'lhr", "Kor", "Kang", "Koloth", "Kumara",
                         "Amar", "B'Moth", "Bortas", "Ch'Tang", "Hegh'ta", "Gorkon", "K'mpec", "Negh'Var",
                         "Pagh", "Rotarran", "Vor'cha", "Qaw'qay", "Buruk", "Ch'Tak", "Drovana", "Hor'Cha",
                         "Kla'Diyus", "Koraga", "Ki'tang", "M'Char", "Malpara", "Maht-H'a", "Ning'tao", "Orantho",
                         "Qu'Vat", "Toh'Kaht", "Vorn", "Y'tem", "T'Ong", "Sompek", "K'Vort"],
            PlanetNames = ["Qo'noS", "Kaleb", "Morath", "Tarsus", "Hakon"],
            MaxWarp = 8, TurnRate = 7, Accel = 0.18, MaxEnergy = 1000, EnergyRegen = 2.4,
            MaxHull = 100, MaxShield = 90, PhaserDamage = 32, PhaserRange = 700,
            TorpDamage = 22, TorpSpeed = 28, TorpLife = 50, MaxTorps = 8, ArmyCapacity = 6,
        },
        [Team.Romulan] = new RaceStats
        {
            Name = "Romulan", Plural = "Romulans", Letter = 'R',
            Description = "Bird of Prey. The slowest ship, with the strongest phasers and plasma torpedoes. Can cloak (C).",
            ShipNames = ["Praetor", "Vreenak", "Decius", "Khazara", "Haakona", "Terix", "Belak", "Valdore",
                         "Devoras", "Genorex", "Goraxus", "T'Met", "Talvath", "D'deridex", "Tomed", "Thrai",
                         "Rateg", "Mogai", "Norexan", "Vastam", "Aj'rmr", "Belhok", "Dividices", "Eletrix",
                         "Erinhe", "Hrrhaen", "Ihaekh", "Llaiir", "Mhiessan", "Narada", "Rhien", "Vrax",
                         "Lleiset", "Khellian", "Ra'Kal", "Hnaev", "Tlaru", "Siuren", "Tal'Rihan"],
            PlanetNames = ["Romulus", "Remus", "Achernar", "Eridani", "Aldebaran"],
            MaxWarp = 7, TurnRate = 5, Accel = 0.12, MaxEnergy = 1000, EnergyRegen = 2.4,
            MaxHull = 95, MaxShield = 100, PhaserDamage = 35, PhaserRange = 650,
            TorpDamage = 26, TorpSpeed = 24, TorpLife = 55, MaxTorps = 6, ArmyCapacity = 5, CanCloak = true,
        },
        [Team.Orion] = new RaceStats
        {
            Name = "Orion", Plural = "Orions", Letter = 'O',
            Description = "Pirate raider. The fastest and most agile ship, with the weakest weapons. Carries 8 armies.",
            ShipNames = ["Corsair", "Marauder", "Raider", "Buccaneer", "Privateer", "Freebooter", "Brigand", "Rover",
                         "Reaver", "Plunderer", "Cutlass", "Scimitar", "Jackal", "Viper", "Harrier", "Vulture",
                         "Rogue", "Smuggler", "Blackjack", "Cutthroat", "Barracuda", "Blackbird", "Cobra", "Desperado",
                         "Falchion", "Hellcat", "Highwayman", "Interloper", "Kestrel", "Mongoose", "Outlaw", "Predator",
                         "Prowler", "Ravager", "Renegade", "Sabre", "Shrike", "Stiletto", "Wolverine"],
            PlanetNames = ["Orion", "Rigel", "Antares", "Betelgeuse", "Deneb"],
            MaxWarp = 9, TurnRate = 8, Accel = 0.22, MaxEnergy = 850, EnergyRegen = 2.6,
            MaxHull = 85, MaxShield = 85, PhaserDamage = 28, PhaserRange = 650,
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

    /// <summary>
    /// Name for this race's <paramref name="number"/>th ship (1-based). If a fleet ever outgrows the name list, names
    /// are reused with Roman numerals: the 40th Federation ship is "Enterprise II", the 79th "Enterprise III".
    /// </summary>
    public string ShipName(int number)
    {
        int index = (number - 1) % ShipNames.Length, round = (number - 1) / ShipNames.Length;
        return round == 0 ? ShipNames[index] : $"{ShipNames[index]} {ToRoman(round + 1)}";
    }

    public static string ToRoman(int value)
    {
        (int Value, string Numeral)[] numerals =
            [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
             (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var sb = new System.Text.StringBuilder();
        foreach (var (v, numeral) in numerals)
            for (; value >= v; value -= v) sb.Append(numeral);
        return sb.ToString();
    }
}
