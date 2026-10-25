# Issues

Bugs and change requests for the PLATO Empire recreation. Each issue is analysed and given a proposed fix here
first; code changes start only once the issue is approved.

**Status values:** Open · Approved · Fixed (awaiting verification) · Closed · Deferred · Won't fix

**Requirements and tests:** this project has no formal requirements document. Issues reference the behaviour
documented in [original-vs-recreation.md](original-vs-recreation.md) and the README, and the existing xUnit test
classes in `tests/PlatoEmpire.Tests`.

| ID | Title | Type | Status |
|---|---|---|---|
| [ISS-001](#iss-001-bots-dont-play-to-their-races-strengths) | Bots don't play to their race's strengths | Enhancement (balance) | Closed |
| [ISS-002](#iss-002-higher-difficulty-relies-on-a-damage-bonus-instead-of-smarter-play) | Higher difficulty relies on a damage bonus instead of smarter play | Enhancement | Closed |
| [ISS-003](#iss-003-torpedo-detonation-area-damage-and-burst-fire-are-missing) | Torpedo detonation, area damage and burst fire are missing | Enhancement (fidelity) | Deferred |
| [ISS-004](#iss-004-ship-names-can-repeat-in-large-grow--shrink-fleets) | Ship names can repeat in large Grow & shrink fleets | Change request | Closed |
| [ISS-005](#iss-005-shipyards-can-build-one-ship-past-the-fleet-limit) | Shipyards can build one ship past the fleet limit | Bug (minor) | Closed |
| [ISS-006](#iss-006-the-app-has-no-icon) | The app has no icon | Change request | Fixed (awaiting verification) |
| [ISS-007](#iss-007-the-window-can-open-larger-than-the-screen-with-its-title-bar-out-of-reach) | The window can open larger than the screen, with its title bar out of reach | Bug | Fixed (awaiting verification) |

---

## ISS-001: Bots don't play to their race's strengths

- **Type:** Enhancement (balance)
- **Status:** Closed 2026-10-17 at the user's request; released in v1.1.0. (Approved and implemented 2026-10-17.)
- **Reported:** 2026-10-17, from the gameplay review. Originally raised for Orions only; widened on 2026-10-17 to
  cover all four races.

### Problem

Every race's bots play the same way, so the race differences only show up as stat numbers. The Orions suffer most:
they're the weakest race in the recreation. In the `--races` balance test (40 four-empire games, all bots at
Lieutenant) they averaged **3.7 worlds** at the end against 6.0–7.1 for the other races, won no games, and were
eliminated twice. In the original game the Orions usually won the tournaments.

### Cause

`BotBrain` gives every race the same behavior; only Romulan cloaking differs.

The Orions are hit hardest because their strengths are all about mobility (fastest speed, acceleration, turning and
torpedoes; 8-army troop capacity; fastest energy regeneration), and no bot behavior uses them. Orion bots bomb planets
from orbit and fight even battles like everyone else, where their weak weapons, hull and shields lose. Their
logistics advantage is large and unused: combining troop capacity and speed, an Orion moves about 1.5× as many
armies per minute as a Federation or Klingon ship and about 2× a Romulan.

The other races' strengths are also underused: Federation shields and phaser range, Klingon agility, and Romulan
firepower (beyond the cloak ambush).

### Affected

- `Game/BotBrain.cs`: planning (`Think`), target choice (`PickEnemy`), retreat rules and fighting (`Fight`).
- [original-vs-recreation.md](original-vs-recreation.md), "Race balance testing": the documented Orion weakness.

### Tests

- **No test fails.** This is a balance problem, not a rule violation.
- **Test gap:** `RaceTests` only checks the race stat numbers. Nothing checks that each race's bots play to their
  strengths, and the `--races` balance check isn't part of the automated test run.

### Proposed fix

Add a per-race **doctrine** that adjusts bot behavior on top of the difficulty level. Each empire is a single race,
so every doctrine must still cover the whole conquest cycle (bomb, ferry, capture, defend). Doctrines **shift
priorities**; they don't lock a race out of any job.

| Race | Strengths (current stats) | Weaknesses | Doctrine |
|---|---|---|---|
| **Federation** | Medium speed and weapons, strongest shields (100) with Romulans, longest phaser range (700) with Klingons, 6 armies | No standout specialty | **Steady line.** The best ship for sitting in orbit under fire, so it leads **bombing** and **defends** home worlds. Fights at the edge of its phaser range, and stays near allies rather than chasing alone |
| **Klingon** | Medium speed and weapons, the most agile medium ship (turn 7, acceleration 0.18), 6 armies | Lighter shields (90) | **Aggressive hunter.** Engages from further away, presses attacks on damaged ships, and pursues them. Uses its agility to circle at close range. Prioritizes enemy **troop carriers** and ships bombing Klingon worlds. Retreats a little later than average |
| **Romulan** | Strongest phasers (35) and torpedoes (26), cloak, strong shields (100) | Slowest ship (warp 7) and slowest turning; only 5 armies | **Ambush and siege.** Cloaked approach and ambush (already built). A good **bomber**, thanks to its firepower and shields. Doesn't chase faster ships (especially Orions) beyond short range. Fights near its own and contested worlds instead of crossing open space |
| **Orion** | Fastest ship (warp 9), quickest acceleration and turning, fastest torpedoes and energy regeneration, 8 armies | Weakest weapons (28 / 20), hull (85) and shields (85) | **Raider and transport.** **Ferries armies first**, capturing worlds that allies have bombed down. Avoids even fights, retreats early and breaks off when shields are low. **Hit and run** against slower ships from the edge of their phaser range. **Raids** poorly defended rear worlds and **intercepts** loaded troop carriers |

**How it's built:** a `RaceDoctrine` record of tuning values that `BotBrain` reads alongside the difficulty
profile, so each race's numbers live in one place and can be tuned with the `--races` test. For example:
preference weights for bombing versus ferrying, retreat threshold offset, engagement range, preferred fighting
distance, how far to chase faster ships, target priority for damaged ships and carriers, and whether to stay near
allies.

### Acceptance

- New unit tests show each doctrine in action:
  - **Orion:** chooses ferrying over bombing when a capturable world exists; disengages from a healthy Romulan.
  - **Klingon:** prefers a damaged enemy or a loaded troop carrier over a healthy, empty ship.
  - **Romulan:** stops chasing an Orion that pulls away beyond short range.
  - **Federation:** chooses bombing over ferrying when both are available, and fights from near its phaser range
    rather than closing in.
- In the `--races` test:
  - every race averages between 4.5 and 7.5 worlds;
  - no race wins more than half of the finished games;
  - no race is eliminated in more than 10% of games.
- Results are recorded in original-vs-recreation.md.

### Resolution

**Implemented as proposed**, in `Game/Doctrine.cs` (the `RaceDoctrine` record and the four doctrines) and
`Game/BotBrain.cs`. Two refinements came out of testing:

- **Romulan chase limit:** only applies to faster ships that are **pulling away**. As first written it applied to
  any faster ship more than 900 units away, and since every other race is faster, Romulans would almost never have
  started a fight.
- **Federation bombing preference** is tested comparatively (Federation bots ferry under half the time, Orions over
  75%) instead of "always bombs", because a race that never ferries could never capture anything.

**Two bugs found and fixed along the way:**

1. **Plans flip-flopped.** The new ferry-or-bomb choice was re-rolled on every planning step, so bots switched
   between the two every half-second and finished neither. The same re-rolling made bombing targets hop between
   planets; that part predates this issue. Bots now keep an unfinished bombing or ferry job while it's still valid.
2. **An Orion pinned itself against the galaxy edge.** It broke off a fight when its shields ran low, then rejoined
   on the next planning step, over and over. Meanwhile the edge avoidance only checked the course the bot *wanted*,
   not where the ship was actually heading. Orions now stay broken off until their shields recover to 60%, and edge
   avoidance also checks the ship's actual heading.

**Tests added:** `DoctrineTests` (14 tests: each race's doctrine, plus plan stickiness for every race), and Orion
variants of `BotTests.Bot_fighting_a_target_on_the_edge_stays_off_the_edge`. Some older tests assumed every bot
attacks any enemy; they now use a Klingon bot explicitly, because Orion bots deliberately avoid healthy,
better-armed ships. All 179 tests pass.

**Balance results** (`--races`, 40 games, all bots at Lieutenant):

| Race | Before | After |
|---|---|---|
| Federation | 6.0 worlds, 1 win | 6.4 worlds |
| Klingon | 6.8 worlds, 1 win | 6.3 worlds |
| Romulan | 7.1 worlds, 2 wins | 6.6 worlds |
| Orion | 3.7 worlds, eliminated 2× | 4.8 worlds |

Every race is inside 4.5–7.5 worlds and none was eliminated. **However, none of the 40 games finished** (before: 4),
so the "no race wins more than half" check passes only because there were no winners. With the races evenly
matched, equal-skill bot empires rarely break through. In the `--compare` test, finishes within 40 minutes went from
3 to 2 of 30 with fixed fleets, and from 12 to 10 of 30 with Grow & shrink fleets. A human player is expected to
tip the balance; ISS-002 (smarter difficulty) may also change this.

**To verify:** play-test, especially as and against Orions, then push.

---

## ISS-002: Higher difficulty relies on a damage bonus instead of smarter play

- **Type:** Enhancement
- **Status:** Closed 2026-10-17 at the user's request; released in v1.1.0. (Approved 2026-10-17 to follow ISS-001; implemented 2026-10-17.)
- **Reported:** 2026-10-17, from the gameplay review

### Problem

Raising the difficulty mostly makes enemy bots aim better, react faster and hit harder. The "hit harder" part is a
straight stat bonus: enemy bot weapons do 0.7× damage at Cadet, rising to 1.25× at Admiral. It breaks the fairness
principle that bots play by the same rules as the player, and the game-design review found it makes harder levels
feel unfair rather than smarter.

### Cause

`DifficultyProfile.DamageMultiplier` is applied to every enemy bot's torpedoes and phasers in
`GameWorld.DamageMultiplier`. Strategy barely changes between levels: from Lieutenant up, the main differences are
speed of reaction and accuracy.

### Affected

- `Game/Difficulty.cs` (`DamageMultiplier` and the per-level tactics flags).
- `Game/GameWorld.cs` (`DamageMultiplier`).
- `Game/BotBrain.cs` (tactics gated by difficulty).
- README "Difficulty" section, and the setup screen's level descriptions.

### Tests

- **No test fails.** Two existing tests enforce the current behavior and would change:
  - `DifficultyTests.Enemy_bot_weapons_scale_with_difficulty` requires the bonus.
  - `DifficultyTests.Each_level_is_at_least_as_hard_as_the_last` requires the multiplier to rise with each level.

### Proposed fix

1. Remove the damage multiplier: every ship's weapons do their race's normal damage at every level.
2. Make higher levels play smarter instead:
   - **Commander and above:** focus fire on damaged enemies and troop carriers; respond to the conquest countdown by
     attacking the leader's worlds; retreat before being destroyed rather than after.
   - **Admiral:** coordinate invasions (one ship bombs while another brings armies), and use race doctrines (ISS-001)
     fully. Lower levels apply the doctrines more loosely.
   - **Cadet and Ensign** stay simple, with slow reactions and poor aim.
3. Update the level descriptions shown on the setup screen and in the README.

### Acceptance

- Bot weapons do identical damage at every difficulty level (replaces the existing test).
- In bot-only games where the player's team stays at Lieutenant, rivals at Commander and Admiral win clearly more
  often than rivals at Cadet and Ensign, measured with `--selftest` or `--compare`.

### Dependency

Builds on the doctrine mechanism from ISS-001; best done after it.

### Resolution

**Implemented as proposed**, in `Game/Difficulty.cs`, `Game/Doctrine.cs`, `Game/BotBrain.cs` and `Game/GameWorld.cs`:

- **No damage bonus:** the multiplier is gone; every ship's weapons do their race's normal damage at every level.
- **Doctrine strength:** Cadet applies race doctrines at 40%, Ensign at 70%, Lieutenant and above in full.
  `RaceDoctrine.Scaled` blends the numbers toward default behavior; the on/off tactics (avoiding even fights, hit
  and run, raiding, giving up chases, breaking off) apply only from half strength. Lieutenant is unchanged, so the
  ISS-001 balance results still hold (rerun: identical).
- **Commander and above:** focus fire (a bonus for enemies that allies are already attacking, damaged ships and
  carriers); conquest response (while a rival holds the conquest countdown, bombing and invasion targets are limited
  to that rival's worlds, and its ships are preferred targets).
- **Admiral:** coordinated invasions. While more allies are bombing than bringing armies, an Admiral fetches armies;
  a loaded Admiral lands them on a world an ally is bombing.
- The setup screen's level descriptions and the README describe the new levels.

**A pre-existing problem found while measuring.** Once the damage bonus was removed, Commanders and Admirals proved
*weaker* than Lieutenants: Admiral bots captured about half as many worlds (30 versus 68 in a 4-game sample).
Switching the new tactics off made no difference, so the cause was older. Testing the old settings one at a time
showed **engagement range** was responsible: at 3,200 units, Admirals spent their time chasing distant ships instead
of conquering. With Lieutenant's 2,200 they captured 88. The damage bonus had been hiding this. **Engagement range is
now capped at 2,200 for Lieutenant and above**, and smarter target choice does the job range used to.

**Tests:** `SmartDifficultyTests` (11 tests). Each compares two levels to show the new behavior: doctrine strength,
Cadet Orions attacking where skilled ones don't, focus fire, conquest response, and both parts of coordinated
invasions. In `DifficultyTests`, the damage-bonus test is replaced by "bot weapons do the same damage at every level"
(torpedoes and phasers, levels 1, 3 and 5), and the level-ordering test now checks the new tactics and allows equal
engagement ranges. All 206 tests pass.

**Acceptance:** the original plan (finish rates in `--compare`) turned out not to work. Too few four-empire games
finish, and harder rivals mostly take worlds from each other rather than from you. A new **`--difficulty`** command
plays one-on-one games: your side at Lieutenant against one rival empire, equal ships, 16 games per level.

| Rival level | Rival wins | Your wins | Worlds at the end (you : rival) |
|---|---|---|---|
| Cadet | 0 / 16 | 14 / 16 | 15.0 : 6.4 |
| Ensign | 2 / 16 | 13 / 16 | 14.1 : 7.9 |
| Lieutenant | 6 / 16 | 6 / 16 | 11.4 : 11.7 |
| Commander | 11 / 16 | 3 / 16 | 9.4 : 13.4 |
| Admiral | 13 / 16 | 2 / 16 | 7.6 : 14.4 |

Each level beats the one below, and equal levels come out even.

**To verify:** play-test at Commander and Admiral (and Cadet, which no longer has its 30% damage handicap, so it
plays somewhat stronger than before).

---

## ISS-003: Torpedo detonation, area damage and burst fire are missing

- **Type:** Enhancement (fidelity to the original)
- **Status:** Deferred (decision 2026-10-17)
- **Reported:** 2026-10-17, from the gameplay review
- **Decision:** On hold because the extra commands would complicate the game's controls. May be reconsidered
  later. If revisited, area damage alone needs no new controls and could be considered separately.

### Problem

The original Empire IV had three torpedo features the recreation lacks
(see [original-vs-recreation.md](original-vs-recreation.md), "Weapons and ship systems"):

- **Detonation:** detonate enemy torpedoes (`d`) and your own (`D`). Detonating enemy torpedoes was the main defense
  against them.
- **Area damage:** "Torp damage was area-of-effect", so one attacker could damage a whole group of ships orbiting a
  planet.
- **Burst fire:** `P` fired a burst of 3 torpedoes.

### Cause

Not implemented. Torpedoes in the recreation hit only the ship they touch, and can't be detonated.

### Affected

- `Game/GameWorld.cs`: torpedo update, a new detonate command, burst fire.
- `Game/BotBrain.cs`: bots using detonation defensively and burst fire offensively.
- `MainWindow.xaml.cs`: new keys and typed commands; the help screen.
- `UI/TacticalView.cs`: detonation and area-blast effects.
- README and original-vs-recreation.md (these rows move from ❌ to ✅).

### Tests

- **No test fails.** These are missing features.
- **Test gap:** none of the torpedo tests in `CombatTests` cover detonation, area damage or bursts.

### Proposed fix

1. **Area damage:** a torpedo that hits explodes and damages every enemy ship within 60 units, falling off with
   distance. As in the original, teammates' torpedoes still never hurt you. **Torpedoes keep detonating
   automatically on contact**; manual detonation (below) is an extra option, not a replacement. Torpedoes that
   reach the end of their range still vanish harmlessly.
2. **Detonation:**
   - **X:** detonate all enemy torpedoes within 400 units of your ship, for an energy cost. They burst harmlessly.
   - **Shift+X:** detonate all your own torpedoes in flight. Each explodes with area damage, which can catch a ship
     dodging past a torpedo.
3. **Burst fire:** **Shift+T** (and Shift+right-click) fires 3 torpedoes in a narrow spread, using 3 tubes and the
   matching energy.
4. **Typed commands:** `det`, `det own` and `burst 45`.
5. **Bots** detonate incoming torpedoes when they can't dodge them (higher difficulty levels), and use bursts
   against slow or orbiting targets.

**Key choice:** the original used `d`, `D` and `P`, but in this recreation **D** is beam down and **P** is phasers.
X and Shift+T avoid changing existing controls.

### Acceptance

- New `CombatTests` cover:
  - area damage hitting two nearby enemies but not a teammate;
  - detonation removing enemy torpedoes in range and spending energy;
  - detonating your own torpedoes damaging ships near them;
  - a burst firing 3 torpedoes and refusing when there aren't 3 free tubes.
- The help screen and README list the new controls; original-vs-recreation.md is updated.

---

## ISS-004: Ship names can repeat in large Grow & shrink fleets

- **Type:** Change request
- **Status:** Closed 2026-10-17 at the user's request; released in v1.1.0. (Approved 2026-10-17 with a change:
  beyond 39, reuse names with Roman numerals.)
- **Reported:** 2026-10-17, by the user: "Can we generate 39 names for each race?"

### Problem

Each race has 20 ship names. With **Grow & shrink** fleets, a side can be allowed up to 39 ships, so the 21st ship
and later reuse names ("R1 Praetor", then "R21 Praetor").

### Cause

GameWorld.AddShip takes names in order from RaceStats.ShipNames and starts over when the list runs out. The
largest possible fleet is 8 starting ships × 24 worlds ÷ 5 starting worlds = 38.4, rounded up to **39**.

### Affected

- Game/Races.cs: the four ShipNames lists.
- RaceTests.Each_race_has_twenty_distinct_ship_names (would become 39).

### Tests

- **No test fails**; 20 names per race is the current, tested design.
- **Test gap:** nothing checks the name count against the largest possible fleet.

### Proposed fix

1. Extend each race's list to **39 names**, keeping the existing 20 in the same order.
2. Replace the "twenty names" test with one that derives the largest possible fleet from the game's own limits
   (8 starting ships, all worlds held) and checks that every race has at least that many distinct names. If the
   limits ever change, the test fails instead of names silently repeating.
3. **Small related fix:** shipyards don't count stranded ships (destroyed ships waiting because their empire had no
   worlds) as part of the fleet. Normally a stranded ship returns before the shipyard acts: stranded ships retry
   every 30 ticks, the shipyard runs every 150 ticks (always also a retry tick), and ship updates run before the
   shipyard. **The gap:** if an empire with no worlds captures one on exactly a shipyard tick, and the capturing
   ship is updated *after* a stranded ship, that stranded ship has already been checked and found no world, so the
   shipyard builds a brand-new ship instead. That's one ship over the limit per occurrence. It's very rare, and
   running past 39 names would need it to happen repeatedly to an empire that then holds nearly every world.
   Counting stranded ships as part of the fleet closes the gap.

**Proposed new names (19 per race, in order after the current 20):**

- **Federation:** Ahwahnee, Bozeman, Challenger, Columbia, Discovery, Excelsior, Gettysburg, Hathaway, Horatio,
  Lakota, Melbourne, Merrimac, Monitor, Pegasus, Princeton, Stargazer, Sutherland, Thunderchild, Venture.
- **Klingon:** Buruk, Ch'Tak, Drovana, Hor'Cha, Kla'Diyus, Koraga, Ki'tang, M'Char, Malpara, Maht-H'a, Ning'tao,
  Orantho, Qu'Vat, Toh'Kaht, Vorn, Y'tem, T'Ong, Sompek, K'Vort.
- **Romulan:** Aj'rmr, Belhok, Dividices, Eletrix, Erinhe, Hrrhaen, Ihaekh, Llaiir, Mhiessan, Narada, Rhien, Vrax,
  Lleiset, Khellian, Ra'Kal, Hnaev, Tlaru, Siuren, Tal'Rihan.
- **Orion:** Barracuda, Blackbird, Cobra, Desperado, Falchion, Hellcat, Highwayman, Interloper, Kestrel, Mongoose,
  Outlaw, Predator, Prowler, Ravager, Renegade, Sabre, Shrike, Stiletto, Wolverine.

### Acceptance

- Every race has at least as many distinct names as the largest possible fleet (39 today).
- A fleet grown to 39 ships has no repeated names.
- An empire that captures a world on a shipyard tick, while its stranded ships wait, revives a stranded ship
  rather than building a new one (a test that recreates the timing exactly).

### Resolution

**Approved with a change** (2026-10-17): 39 names per race, and if a fleet ever needs more, "somehow", names are
reused with Roman numerals. Item 3 (the shipyard gap) wasn't part of the approval; it moved to
[ISS-005](#iss-005-shipyards-can-build-one-ship-past-the-fleet-limit).

- `Game/Races.cs`: each race now has the 39 names listed above, the original 20 first and in the same order.
  `RaceStats.ShipName(n)` names the nth ship; after the list runs out it reuses names with numerals, so the 40th
  Federation ship is "Enterprise II" and the 79th "Enterprise III". `RaceStats.ToRoman` formats the numerals.
- `Game/GameWorld.cs`: new ships are named with `ShipName`.
- **Tests:** `RaceTests` now checks that every race has at least as many distinct names as the largest possible
  fleet, worked out from the game's own limits (39 today), instead of a fixed count of 20. New tests cover the
  numeral naming (ships 1, 39, 40, 41, 78, 79 and 118), Roman numeral formatting, and uniqueness across 195 ships.
  All 193 tests pass.

---

## ISS-005: Shipyards can build one ship past the fleet limit

- **Type:** Bug (minor)
- **Status:** Closed 2026-10-17 at the user's request; released in v1.1.0. (Approved and fixed 2026-10-17.)
- **Reported:** 2026-10-17, split out of ISS-004 (item 3)

### Problem

With **Grow & shrink** fleets, an empire can occasionally build one more ship than its fleet limit allows.

### Cause

`GameWorld.RunShipyards` counts a fleet as its living ships plus destroyed ships waiting to return, but **not**
stranded ships (destroyed ships waiting because their empire had no worlds). Normally a stranded ship returns before
the shipyard acts: stranded ships retry every 30 ticks, the shipyard runs every 150 ticks (always also a retry
tick), and ship updates run before the shipyard. If an empire with no worlds captures one on exactly a shipyard
tick, and the capturing ship is updated *after* a stranded ship, that stranded ship has already been checked and
found no world, so the shipyard builds a brand-new ship instead.

### Affected

- `Game/GameWorld.cs`: `RunShipyards`.
- The rule in the README and original-vs-recreation.md that fleets are limited by worlds held.

### Tests

- **No test fails.** **Test gap:** `FleetLimitTests` has no test for an empire regaining a world while it has
  stranded ships.

### Proposed fix

Count stranded ships as part of the fleet in `RunShipyards`, so a regained world revives a stranded ship instead of
building a new one.

### Acceptance

- A test that recreates the timing exactly: an empire with stranded ships captures a world on a shipyard tick, and
  a stranded ship returns instead of a new ship being built.

### Resolution

- **Test first:** `FleetLimitTests.Capturing_a_world_on_a_shipyard_tick_revives_a_stranded_ship_instead_of_building_one`
  recreates the exact timing. An empire that started with 8 ships has lost every world: 7 ships are stranded, and
  the last (updated after them) captures a world on a shipyard tick. Before the fix it **failed**, with a 9th ship
  built. It now passes: no ship is built, and a stranded ship returns within a second.
- **Fix:** `GameWorld.RunShipyards` now counts every ship the empire owns, flying or not, so stranded ships count
  toward the fleet.
- All 194 tests pass.

---

## ISS-006: The app has no icon

- **Type:** Change request
- **Status:** Open
- **Reported:** 2026-10-25, while making icons for the qapow.com apps.

### Problem

The game shows the generic Windows icon in its title bar, on the taskbar and on `PlatoEmpire.exe` in Explorer. The
owner wants an icon of a Federation ship drawn in orange on black, with its shields up and the saucer at the top.

### Cause

No icon was ever added: `PlatoEmpire.csproj` has no `ApplicationIcon`, and `MainWindow` has no `Icon`.

### Affected

- `src/PlatoEmpire/PlatoEmpire.csproj`: the exe icon.
- `src/PlatoEmpire/MainWindow.xaml`: the title bar and taskbar icon.
- No game behaviour, rules or documentation change.

### Tests

- **No test fails.** This is new artwork, not a rule violation.
- **Test gap:** nothing checks that the app has an icon. It needs a check because a wrong resource path fails
  silently: the window falls back to the generic icon.

### Proposed fix

- Add `src/PlatoEmpire/Assets/PlatoEmpire.ico`, which has 16, 24, 32, 48, 64, 128 and 256 px images. It is
  rendered from an SVG drawing: a Federation ship in the game's plasma orange (`#FF9A1F`, glow `#FFD080`) on black,
  saucer to the top, inside a dashed shield bubble. The SVG source sits next to the .ico for future edits.
- Set `<ApplicationIcon>Assets\PlatoEmpire.ico</ApplicationIcon>` and add the file as a `<Resource>` in the csproj.
- Set `Icon="Assets/PlatoEmpire.ico"` on `MainWindow`.

### Acceptance

- `PlatoEmpire.exe` shows the ship icon in Explorer, and the running game shows it in the title bar and taskbar.
- A test fails if the icon resource cannot be loaded from the assembly.

### Resolution

- **Tests first:** `AppIconTests.Window_icon_is_embedded_with_every_size_from_16_to_256` loads
  `assets/platoempire.ico` from the assembly's WPF resources and checks it decodes with 16 to 256 px images.
  `AppIconTests.Exe_has_an_icon` checks that `PlatoEmpire.exe` carries a Win32 icon resource. Both **failed** before
  the fix (no resource; no exe icon) and pass now.
- **Fix:** added `Assets/PlatoEmpire.ico` (with its SVG source `Assets/PlatoEmpire.svg`), `ApplicationIcon` and a
  `Resource` item in `PlatoEmpire.csproj`, and `Icon="Assets/PlatoEmpire.ico"` on `MainWindow`.
- **Checked by running the game:** the ship icon shows on the exe, in the title bar and on the taskbar.
- All 208 tests pass.
- Released in v1.2.0.

---

## ISS-007: The window can open larger than the screen, with its title bar out of reach

- **Type:** Bug
- **Status:** Fixed (awaiting verification). Approved 2026-10-25 (with the request to start maximized).
- **Reported:** 2026-10-25 by the owner: the main window sometimes opens too big for the screen and is hard to move.
  The owner also asked for the game to start maximized.

### Problem

On screens with less than about 920 device-independent pixels of usable height, the window opens taller than the
screen, and its title bar can end up above the top edge, so it can't be dragged. Examples are 1920×1080 at 125 %
scaling (about 816 usable) or 150 % (about 680), and 1366×768 at 100 % (about 728). The window can't be shrunk to
fit either, because its minimum size (1100×720) is also larger than some of these screens.

### Cause

`MainWindow.xaml` sets a fixed `Width="1440" Height="920"`, `MinWidth="1100" MinHeight="720"` and
`WindowStartupLocation="CenterScreen"`. Centering a window taller than the work area gives it a negative top, and
nothing limits the window to the screen.

### Affected

- `src/PlatoEmpire/MainWindow.xaml` and `MainWindow.xaml.cs`: startup window size, state and position.
- No game behaviour changes. The game screen already lays itself out for any window size.

### Tests

- **No test fails.** **Test gap:** nothing checks the main window's startup state or that its size fits the screen.

### Proposed fix

- Start the window maximized (`WindowState="Maximized"`), as the owner asked.
- Keep the normal (restored) size within the screen: in the constructor, limit `Width`/`Height` and
  `MinWidth`/`MinHeight` to `SystemParameters.WorkArea`, and centre the window in the work area (CenterScreen centres
  on the whole screen, so the taskbar can still cover the bottom). Then *Restore down* gives a window that is fully
  visible, with a reachable title bar.

### Acceptance

- A test checks that the main window starts maximized.
- A test checks that on a small work area (1280×680) the window's normal and minimum sizes fit inside it, and that
  on a large one the designed 1440×920 size is kept.
- A test checks that the restored window sits entirely inside the work area.
- Running the game: it opens maximized, and *Restore down* gives a window that can be moved.

### Resolution

- **Tests first:** `MainWindowTests.Main_window_starts_maximized` and
  `MainWindowTests.Normal_size_shrinks_to_fit_a_small_screen` **failed** before the fix (the window started `Normal`
  at 1440×920). `Normal_size_is_kept_on_a_large_screen` passed before and after, as intended.
- **Fix:** `MainWindow.xaml` sets `WindowState="Maximized"`. The constructor calls the new `MainWindow.FitTo`,
  which limits the normal and minimum sizes to `SystemParameters.WorkArea`.
- **Found while checking:** after *Restore down* on this PC (1920×1080 at 125 %), the title bar was reachable, but
  the bottom 56 px sat behind the taskbar, because `CenterScreen` centres on the whole screen. `FitTo` now centres
  the window in the work area itself. The test `Restored_window_sits_inside_the_work_area` was written with this
  change, not before it.
- **Checked by running the game:** it opens maximized, and *Restore down* gives a window at 0,0–1852,1012 physical
  px inside the 1920×1020 work area.
- All 212 tests pass.
- Released in v1.2.0.
