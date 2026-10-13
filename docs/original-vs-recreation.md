# The original PLATO Empire vs. this recreation

This recreation was first built from general knowledge, not from documentation of the original game. This page records
what is actually documented about the original, how the recreation compares, and which rules and numbers were invented.

Most comparisons are against **Empire IV** (1976), the most-played version. It's the one Xtrek and Netrek grew from,
and the closest to this recreation (one ship per player, steering by direction and warp speed).

## Sources

| Source | Notes |
|---|---|
| John Daleske's site: [timeline](https://web.archive.org/web/20221225184956/https://www.daleske.com/plato/empire.php), [Empire III](https://web.archive.org/web/20221225153940/http://www.daleske.com/plato/empire3.php), [Empire IV](https://web.archive.org/web/20230109043807/http://daleske.com/plato/empire4.php) | Written by the game's designer. Most reliable source. **The live domain has lapsed and now redirects to a suspicious download page — use the Internet Archive links only.** |
| [Wikipedia: Empire (1973 video game)](https://en.wikipedia.org/wiki/Empire_(1973_video_game)) | Gameplay description and version history. Usually doesn't say which version a detail applies to. |
| [Tropedia: PLATO Empire](https://tropedia.fandom.com/wiki/PLATO_Empire) | Fan wiki. Lists Empire IV ship features. |

Not found: the Empire IV in-game help text, the source code, or any numbers (ranges, damage, speeds, capacities).
Everything below marked **Invented** has no documented basis.

## Versions of the original

| Version | Year | Players / teams | Summary |
|---|---|---|---|
| Empire I | 1973 | 8 / 8 | Turn-based economic strategy. Became Silas Warner's *Conquest*. |
| Empire II | 1973 | 50 / 8 | First tactical version: one ship per player, 8×8 sectors. |
| Empire III | 1973–74 | 50 / 7 + rogue team | Redesign with momentum physics and thrust; your ship stays centred on screen. Still playable on Cyber1. |
| Empire IV | 1976 | 30 ships / 4 teams | Simplified steering (direction and warp) and fixed ship types per team. Became Xtrek and Netrek. |
| Empire IV tournament | 1981 | 32 / 4 | Tournament version by Steve Peltz; the Orions usually won. |

## Comparison

Status key: ✅ matches the original · ⚠️ partly matches · ❌ differs from the original · ➕ added in this recreation.

### Teams and ships

| Topic | Original (documented) | This recreation | Status |
|---|---|---|---|
| Teams | Federation, Romulan, Orion, and Klingon, renamed **Kazari** before publication for copyright reasons (back to Klingon on NovaNET in 1991) | Federation, Klingon, Romulan, Orion | ✅ (uses "Klingon") |
| Race balance | Orions: "weakest weapons but are fast". Romulans: "strongest weapons, but are the slowest". Federation and Kazari: medium speed and strength | Same ordering, enforced by `RaceTests` | ✅ |
| Ship stat numbers | Not documented | Speeds, turn rates, energy, hull, shields and weapon numbers in `Races.cs` | **Invented** |
| Troop capacity | Not documented | 6 Federation, 6 Klingon, 5 Romulan, 8 Orion | **Invented** |
| Cloaking | Empire IV had a cloaking device ("C toggle Cloaking device"). Nothing says which races had it | **Romulans only** | ❌ Star Trek lore; a deliberate choice |
| Cloak detection | Not documented | Enemies within **300** units can see a cloaked ship (originally 450). Firing drops the cloak, and planets fire on cloaked ships orbiting them | **Invented** |
| Cloak energy | Not documented | **150** energy to engage, then **3.5 per tick** while on, more than any ship regenerates (2.4). A stopped cloaked ship lasts about 25 seconds; at full speed with shields, about 10 | **Invented** |
| Cloak tactics (bots) | Not documented | Romulan bots cloak only when an enemy ship or a defended planet is nearby, and stay cloaked into a fight, holding fire until within 500 units (an ambush) | **Invented** |
| Ship drawings | Each team's ship had its own look and characteristics. Empire III's small ship became the Empire IV Orion | Line-drawn ship per race | ⚠️ in spirit |

### The galaxy

| Topic | Original (documented) | This recreation | Status |
|---|---|---|---|
| Size | Empire IV: 64×64 grid of sectors, "many screens in size" | 10,000 × 10,000 units, no sectors | ⚠️ |
| Planets | 25 planets. Each team has **3 adjacent home planets** near a corner. 2 neutral worlds between neighbouring teams and 5 in the centre (13 neutral) | 24 planets: **5 per team** in its quadrant, 4 neutral near the centre | ❌ |
| Starting armies | **50** per team planet, **25** per neutral planet | Home 30, other team planets 10–16, neutral 5–9 | ❌ much smaller, for faster games |
| Planet types | Orbiting a friendly "Class-M" planet raised the refuelling rate; some planets were uninhabitable | Every planet is the same | ⚠️ |
| Map edge | Empire III players asked that space not "wrap" around | Space has hard edges; bots steer away from them | ✅ |

### Armies and conquest

| Topic | Original (documented) | This recreation | Status |
|---|---|---|---|
| Actions | Bomb enemy armies, beam friendly armies up, beam armies down to take over planets | Same (B, U, D keys) | ✅ |
| Bombing pace | Bombing a 50-army planet "could take five to ten minutes" | One army every half-second | ❌ much faster |
| Bombing floor | Not documented | Bombing stops at 4 armies | **Invented** (from Netrek) |
| Capture rule | "Beam down armies to take over planets"; exact rule not documented | Each army kills one defender; the next one lands and captures | **Invented** |
| Army growth | Implied ("building up armies on one's friendly planets") | Planets regrow armies over time, faster on home worlds | **Invented** rates |
| Planet defenses | Not documented | Planets shoot at enemy ships nearby | **Invented** |
| Coup | Empire IV: "C Coup to recapture home planet" | Not implemented | ❌ missing |

### Weapons and ship systems

| Topic | Original (documented) | This recreation | Status |
|---|---|---|---|
| Phasers | "Fire in a cone shape and do damage immediately" | Narrow cone, instant hit, damage falls off with range | ✅ |
| Torpedoes | Dodgeable; limited number in flight (max 90 in total); can't hurt teammates | Same | ✅ |
| Torpedo area damage | "Torp damage was area-of-effect" | A torpedo damages only the ship it hits | ❌ |
| Detonation | Detonate enemy torpedoes (d) or your own (D) | Not implemented | ❌ missing |
| Torpedo burst | "P fire burst of 3 Photon torpedoes" | Not implemented | ❌ missing |
| Fuel | Limited and "regenerated fairly slowly"; max warp burns it "in minutes"; firing and moving both cost fuel | Same idea | ✅ |
| Energy split | "A Allocate energy" between weapons and engines | Not implemented | ❌ missing |
| Engine temperature | Engines overheat if run too hard ("e see engine temperature") | Not implemented | ❌ missing |
| Shields | Raise and lower shields | S toggles shields; they absorb 85% of damage | ⚠️ numbers invented |
| Repair | Damage repair; "R double Repair efforts" | Automatic repair, faster in friendly orbit | ⚠️ |
| Other commands | Tractor beam, self-destruct, war and peace declarations, messages, emergency distress signal | Not implemented | ❌ missing |

### Game flow and controls

| Topic | Original (documented) | This recreation | Status |
|---|---|---|---|
| Steering | Empire IV: a direction and a warp speed, no momentum (Empire III had momentum) | Same as Empire IV | ✅ |
| Warp | Keys 0–9 set "warp 0-10" | Keys 0–9 set warp 0–9, with a top speed per race | ✅ |
| Heading convention | **0 = right, 90 = up**, counter-clockwise | **0 = up, 90 = right**, clockwise (compass style) | ❌ affects typed commands |
| Controls | Typed commands only; PLATO had no mouse | Typed commands plus mouse and keyboard | ➕ |
| Respawning | New ship at your home system, or any home system your team has conquered | New ship at your home world, or any world your team holds, after 4 seconds | ⚠️ |
| Winning | One team takes over **all** the planets | Eliminate every rival, or (optional, on by default) hold 60/75/90% of the worlds for 30 seconds | ⚠️ conquest share **invented** |
| Players | Up to 30 people, at most 15 per team | One human plus bots | ➕ single-player by design |
| Computer ships | Occasional "robot players", such as the planet-eating Doomsday Machine | Bots fly every ship except yours | ➕ |

## Added in this recreation (not in the original)

- **Single-player bots** with five difficulty levels, changeable mid-game.
- **Conquest share** win condition (60/75/90% held for 30 seconds), as an alternative to taking every planet.
- **Grow & shrink fleets** (optional): fleet size follows the worlds an empire holds.
- **Mouse control**, a galaxy map you can click, and zoom.
- **Bots steering away from the galaxy edge.**

## Race balance testing

The documented ordering (Romulans strongest and slowest, Orions fastest and weakest, Federation and Klingons medium)
is fixed, but every number behind it is invented. `PlatoEmpire.exe --races` plays 40 four-empire games with every
bot at the same skill (Lieutenant), rotating which race the autopilot flies. Results while tuning the Romulans:

| Race | Before tone-down | Energy and shields cut | Weapon margins narrowed (current) |
|---|---|---|---|
| Romulan | 8 wins, 9.3 worlds | 3 wins, 9.0 worlds | 2 wins, 7.1 worlds |
| Klingon | 0 wins, 5.5 worlds | 0, 5.5 | 1, 6.8 |
| Federation | 1 win, 6.0 worlds | 0, 6.0 | 1, 6.0 |
| Orion | 0 wins, 2.7 worlds, eliminated 7× | 0, 3.1, eliminated 4× | 0, 3.7, eliminated 2× |

"Worlds" is the average held when the game ended. Changes made: Romulan energy 1100 → 1000, regeneration
2.5 → 2.4, shields 110 → 100, phasers 38 → 35, torpedoes 32 → 26; Orion phasers 26 → 28, torpedoes 18 → 20.

Orions are still the weakest. In the original's tournaments they usually won, likely because human players
exploited their speed. The bots don't use speed tactically, so Orion strength in this recreation is limited by
the AI as much as by the numbers.