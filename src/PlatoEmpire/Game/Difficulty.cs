namespace PlatoEmpire.Game;

/// <summary>
/// Tunes enemy bot behaviour. Allied bots always fly at <see cref="Ally"/> so that raising
/// the difficulty makes the opposition tougher rather than making your wingmen carry you.
/// </summary>
public sealed record DifficultyProfile(
    int Level,
    string Name,
    string Description,
    int ReactionTicks,       // ticks between tactical decisions
    double AimErrorDeg,      // std-dev of aiming error
    bool LeadTargets,        // aim torpedoes at predicted intercept point
    double FireChance,       // per-tick chance of firing when a shot is available
    bool UsesPhasers,
    double RetreatHull,      // hull-damage fraction at which the bot runs for repairs
    bool SmartShields,       // raise shields only when threatened (saves energy)
    bool Dodges,             // sidestep incoming torpedoes
    bool StrategicCaptures,  // deliberately ferry armies to weakened planets
    double DamageMultiplier, // scales bot weapon damage
    double EngageRange,      // how far away bots will go looking for a fight
    bool UsesCloak)
{
    public static readonly DifficultyProfile[] Levels =
    [
        new(1, "Cadet", "Sluggish bots with wild aim. Learn the ropes.",
            30, 28, false, 0.03, false, 0.90, false, false, false, 0.70, 1400, false),
        new(2, "Ensign", "Bots shoot straighter, use phasers and manage shields.",
            20, 16, false, 0.05, true, 0.80, true, false, false, 0.85, 1800, false),
        new(3, "Lieutenant", "Bots lead their shots and ferry armies to invade.",
            12, 9, true, 0.08, true, 0.70, true, false, true, 1.00, 2200, true),
        new(4, "Commander", "Quick reactions, torpedo dodging, coordinated invasions.",
            7, 5, true, 0.12, true, 0.62, true, true, true, 1.10, 2600, true),
        new(5, "Admiral", "Ruthless. Near-perfect gunnery and hard-hitting weapons.",
            4, 2, true, 0.18, true, 0.55, true, true, true, 1.25, 3200, true),
    ];

    public static DifficultyProfile Ally => Levels[2];

    public static DifficultyProfile Get(int level) => Levels[Math.Clamp(level, 1, Levels.Length) - 1];
}
