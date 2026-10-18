namespace PlatoEmpire.Game;

/// <summary>
/// Tunes enemy bot behaviour. Allied bots always fly at <see cref="Ally"/> so that raising
/// the difficulty makes the opposition tougher rather than making your wingmen carry you.
/// Difficulty changes how bots play, never how hard their weapons hit (ISS-002).
/// </summary>
public sealed record DifficultyProfile(
    int Level,
    string Name,
    string Description,
    int ReactionTicks,          // ticks between tactical decisions
    double AimErrorDeg,         // std-dev of aiming error
    bool LeadTargets,           // aim torpedoes at predicted intercept point
    double FireChance,          // per-tick chance of firing when a shot is available
    bool UsesPhasers,
    double RetreatHull,         // hull-damage fraction at which the bot runs for repairs
    bool SmartShields,          // raise shields only when threatened (saves energy)
    bool Dodges,                // sidestep incoming torpedoes
    bool StrategicCaptures,     // deliberately ferry armies to weakened planets
    double EngageRange,         // how far away bots will go looking for a fight (capped: chasing far-off ships costs conquests)
    bool UsesCloak,
    double DoctrineStrength,    // how fully bots follow their race's doctrine (0-1; ISS-001)
    bool FocusFire,             // gang up on the enemy allies are already attacking, damaged ships and carriers
    bool RespondsToConquest,    // attack the worlds and ships of a rival close to winning by conquest
    bool CoordinatesInvasions)  // fetch armies for worlds allies are bombing, and land them there
{
    public static readonly DifficultyProfile[] Levels =
    [
        new(1, "Cadet", "Sluggish bots with wild aim that barely use their race's strengths. Learn the ropes.",
            30, 28, false, 0.03, false, 0.90, false, false, false, 1400, false,
            0.4, false, false, false),
        new(2, "Ensign", "Bots shoot straighter, use phasers and shields, and partly play to their race's strengths.",
            20, 16, false, 0.05, true, 0.80, true, false, false, 1800, false,
            0.7, false, false, false),
        new(3, "Lieutenant", "Bots lead their shots, ferry armies to invade and play to their race's strengths.",
            12, 9, true, 0.08, true, 0.70, true, false, true, 2200, true,
            1.0, false, false, false),
        new(4, "Commander", "Quick reactions and dodging. Bots gang up on damaged ships and carriers, and on any empire close to conquest.",
            7, 5, true, 0.12, true, 0.62, true, true, true, 2200, true,
            1.0, true, true, false),
        new(5, "Admiral", "Near-perfect gunnery. Bots coordinate invasions: one bombs while another brings the armies.",
            4, 2, true, 0.18, true, 0.55, true, true, true, 2200, true,
            1.0, true, true, true),
    ];

    public static DifficultyProfile Ally => Levels[2];

    public static DifficultyProfile Get(int level) => Levels[Math.Clamp(level, 1, Levels.Length) - 1];
}
