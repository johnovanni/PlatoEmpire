# EMPIRE — a PLATO recreation (C# / WPF)

[![Tests](https://github.com/johnovanni/PlatoEmpire/actions/workflows/tests.yml/badge.svg)](https://github.com/johnovanni/PlatoEmpire/actions/workflows/tests.yml)

A single-player recreation of **Empire** (the `empire` lesson on the CDC PLATO system, John Daleske, 1973):
real-time interstellar war between the Federation, Klingons, Romulans and Orions, rendered in PLATO plasma-orange.

```
dotnet run -c Release --project src/PlatoEmpire
```

## Gameplay
- Choose your empire, 1–3 rival empires, ships per rival, allied bots and difficulty.
- Bomb enemy worlds down to 4 armies, beam up armies from your worlds, beam them down to capture.
- An empire with no worlds and no ships falls.
- **Win** by eliminating every rival, or by **conquest**: hold a set share of all worlds (60%, 75% or 90%, chosen at setup; default 60%) for 30 seconds straight. Rivals can win the same way, and a banner counts down whenever any empire is close. Conquest can also be turned off.
- **Fleet size vs. worlds held** (chosen at setup): an empire's fleet limit is its starting fleet scaled by the worlds it holds versus its starting count, rounded up. With **Grow & shrink**, shipyards build a new ship every 5 seconds while an empire is below its limit, so expanding empires field bigger fleets. With **Fixed** (the default, as in the original game), fleets never change. Ships already flying are never removed; lost ships just aren't replaced beyond the limit. Your own ship always returns but counts toward the limit.
- Each race flies a different hull: the Federation is balanced, Klingons have heavy phasers, Romulans have plasma torpedoes and a cloak, and Orions are fast troop raiders.

## Difficulty
Five levels (Cadet → Admiral) that you can change at any time with `[` / `]` or the side panel. Difficulty changes enemy reaction time, aim error, target leading, rate of fire, phaser and shield use, torpedo dodging, invasion strategy, weapon damage and engagement range. Allied bots always fly at Lieutenant level.

## Controls
Press **H** in game. Briefly: left-click = course, right-click/Space = torpedo, Shift-click/P = phasers,
0–9 = warp, S shields, O orbit, B bomb, U/D beam armies up/down, C cloak, Enter = typed PLATO-style commands (`w 6 90`, `t 45`, ...).

## Testing
- `dotnet test` runs the xUnit suite in `tests/PlatoEmpire.Tests`. It covers geometry and aiming, difficulty scaling, galaxy setup, combat, orbit/bomb/beam/capture rules, both win conditions and bot behaviour, plus an 8-minute all-bot game.
- `PlatoEmpire.exe --selftest report.txt` plays 40-minute bot-only games at every difficulty and writes a summary.
- `PlatoEmpire.exe --compare report.txt` plays 90 bot-only games, 30 under each fleet rule, then compares how often and how fast games finish.
- `PlatoEmpire.exe --snapshot <folder>` renders the tactical and galaxy views to PNG files.
