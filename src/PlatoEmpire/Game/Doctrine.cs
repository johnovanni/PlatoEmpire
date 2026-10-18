namespace PlatoEmpire.Game;

/// <summary>
/// How a race's bots play to their ship's strengths (ISS-001). Applied on top of the difficulty profile.
/// Doctrines shift priorities; they never lock a race out of a job, since every empire must bomb, ferry
/// armies, capture and defend on its own.
/// </summary>
public sealed record RaceDoctrine
{
    public required string Name { get; init; }

    /// <summary>Chance of ferrying armies rather than bombing when a capturable world exists.</summary>
    public double FerryChance { get; init; } = 0.6;

    /// <summary>Added to the difficulty's retreat threshold (fraction of hull damage). Negative retreats earlier.</summary>
    public double RetreatOffset { get; init; }

    /// <summary>Also retreat when shields fall below this fraction during a fight (0 = never).</summary>
    public double BreakOffShields { get; init; }

    /// <summary>Scales how far away the bot will go looking for a fight.</summary>
    public double EngageRangeScale { get; init; } = 1;

    /// <summary>Preferred distance once in a close fight.</summary>
    public double FightRange { get; init; } = 400;

    /// <summary>Gives up on a faster target that pulls further away than this (0 = always chase).</summary>
    public double ChaseLimit { get; init; }

    /// <summary>Target-score bonus per unit of the target's damage fraction (higher = hunts damaged ships).</summary>
    public double DamagedTargetWeight { get; init; } = 400;

    /// <summary>Target-score bonus for a ship carrying armies.</summary>
    public double CarrierTargetWeight { get; init; } = 800;

    /// <summary>Target-score bonus for an enemy orbiting one of this empire's worlds.</summary>
    public double DefendBonus { get; init; }

    /// <summary>Skips healthy targets with stronger weapons, unless they carry armies or are attacking its worlds.</summary>
    public bool AvoidsEvenFights { get; init; }

    /// <summary>Against slower ships, holds just outside their phaser range and fires torpedoes.</summary>
    public bool HitAndRun { get; init; }

    /// <summary>Prefers worlds with no enemy ships nearby when choosing what to bomb or capture.</summary>
    public bool RaidsRear { get; init; }

    /// <summary>Target-score penalty for enemies far from any of its worlds or allies (stays with the fleet).</summary>
    public double LoneChasePenalty { get; init; }

    /// <summary>Target-score penalty for enemies far from any world (prefers fighting near planets).</summary>
    public double OpenSpacePenalty { get; init; }

    /// <summary>Steady line: leads bombing and defends, fights from near its long phaser range, stays with the fleet.</summary>
    public static readonly RaceDoctrine Federation = new()
    {
        Name = "Steady line",
        FerryChance = 0.35,
        EngageRangeScale = 0.85,
        FightRange = 525,
        DefendBonus = 1200,
        LoneChasePenalty = 1000,
    };

    /// <summary>Aggressive hunter: engages further out, presses damaged ships and carriers, retreats later.</summary>
    public static readonly RaceDoctrine Klingon = new()
    {
        Name = "Aggressive hunter",
        FerryChance = 0.5,
        RetreatOffset = 0.08,
        EngageRangeScale = 1.25,
        FightRange = 300,
        DamagedTargetWeight = 1000,
        CarrierTargetWeight = 1500,
        DefendBonus = 1000,
    };

    /// <summary>Ambush and siege: bombs, ambushes from cloak, doesn't chase faster ships, fights near worlds.</summary>
    public static readonly RaceDoctrine Romulan = new()
    {
        Name = "Ambush and siege",
        FerryChance = 0.4,
        EngageRangeScale = 0.8,
        FightRange = 450,
        ChaseLimit = 900,
        DefendBonus = 800,
        OpenSpacePenalty = 1000,
    };

    /// <summary>Raider and transport: ferries first, avoids even fights, hit and run, raids, hunts carriers.</summary>
    public static readonly RaceDoctrine Orion = new()
    {
        Name = "Raider and transport",
        FerryChance = 0.9,
        RetreatOffset = -0.2,
        BreakOffShields = 0.25,
        CarrierTargetWeight = 1500,
        DefendBonus = 600,
        AvoidsEvenFights = true,
        HitAndRun = true,
        RaidsRear = true,
    };

    /// <summary>Default behavior every doctrine is measured against (and scaled toward at low difficulty).</summary>
    static readonly RaceDoctrine Default = new() { Name = "Default" };

    /// <summary>
    /// This doctrine applied with <paramref name="strength"/> (0-1, from the difficulty level, ISS-002): numbers are
    /// blended toward the default behavior, and the on/off tactics (avoiding even fights, hit and run, raiding,
    /// giving up chases, breaking off) only apply from half strength up.
    /// </summary>
    public RaceDoctrine Scaled(double strength)
    {
        if (strength >= 1) return this;
        var d = Default;
        double Blend(double from, double to) => from + (to - from) * strength;
        bool on = strength >= 0.5;
        return this with
        {
            FerryChance = Blend(d.FerryChance, FerryChance),
            RetreatOffset = Blend(d.RetreatOffset, RetreatOffset),
            BreakOffShields = on ? BreakOffShields : d.BreakOffShields,
            EngageRangeScale = Blend(d.EngageRangeScale, EngageRangeScale),
            FightRange = Blend(d.FightRange, FightRange),
            ChaseLimit = on ? ChaseLimit : d.ChaseLimit,
            DamagedTargetWeight = Blend(d.DamagedTargetWeight, DamagedTargetWeight),
            CarrierTargetWeight = Blend(d.CarrierTargetWeight, CarrierTargetWeight),
            DefendBonus = Blend(d.DefendBonus, DefendBonus),
            AvoidsEvenFights = on && AvoidsEvenFights,
            HitAndRun = on && HitAndRun,
            RaidsRear = on && RaidsRear,
            LoneChasePenalty = Blend(d.LoneChasePenalty, LoneChasePenalty),
            OpenSpacePenalty = Blend(d.OpenSpacePenalty, OpenSpacePenalty),
        };
    }

    public static RaceDoctrine For(Team team) => team switch
    {
        Team.Federation => Federation,
        Team.Klingon => Klingon,
        Team.Romulan => Romulan,
        _ => Orion,
    };
}
