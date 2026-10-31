# Session log

Read this before starting new work on Igi's QoL Modpack. Newest entry first. Each entry says what changed, which files, how it was verified, and what is still open. Feature status lives in the overview table of [`docs/qol-modpack-plan.md`](docs/qol-modpack-plan.md); per-feature config is in the `README.md` table.

How to verify (unchanged from the phase plans): build with `dotnet build -c Release src/IgiQoL`, copy the newest autosave to `Saves/igiqol-dev.save` (+ `.meta`), set `[Dev] Enabled = true` in `igi.qol.cfg`, launch through `/tmp/igiqol-launch.sh "Self-check complete|switched off"` (recreate it from `PHASE_2B_RULES_CONTACTS_PLAN.md`, Conventions), then grep `LogOutput.log` for `CHECK`. Afterwards set `[Dev] Enabled = false` and delete the dev save and the helper.

## State before this log (up to 2026-10-30)

Phases 0, 1, 2a and 2b are done; see `PHASE_2A_MOD_WINDOW_PLAN.md` and `PHASE_2B_RULES_CONTACTS_PLAN.md` (execution notes at the end of each). Shipped features: Core, Dev, TractorCaps, JumpsRemaining, FleetVitals, MissileRetarget, DronesOnArrival, EchoGates, EchoOperations, Window, FleetStrip, EngagementDistance, EchoPriority, Operations, Notes, ItemRulesTab, EchoCargo, Contacts, TextScale. Still open from the wishlist: per-weapon targeting rules (#4), missile evasion (#8), ship separation (#9), drone modes (#11), loadouts (#14), crafting roll-up (#18), contact edge markers (#2).

## 2026-10-31: end of the thirteenth loop session

This session added `EchoGates` `RecruitPrisoners`, `HireOnlyWhatFits` (a fix for a game bug: ECHO paid for crew that couldn't fit aboard, on by default) and `RebalanceCrew`, the repair wait in `QuickStationSteps`, and `FleetVitals` `BarHeight`, and saw `EchoCargo` selling work in play. Final full harness run: 34 active, 154 pass, 0 fail, 5 skip (the same five as before: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`, `DroneModes/gatherer-assist`). Plain launch to the main menu: `33 active, 1 disabled` (Dev), no `switched off`. `[Dev] Enabled = false`, `OnlyChecks` empty, `EchoSoakSeconds = 0`, other `[Dev]` values unchanged. New options in your cfg: `RecruitPrisoners = false`, `RebalanceCrew = false`, `HireOnlyWhatFits = true`, `BarHeight = 4`. The dev save, `/tmp/igiqol-launch.sh` (wait loop `seq 1 1900` for the 1200 s soaks) and the unzipped save copy in `/tmp/vg` are deleted. Your own saves were not written (mtimes still 2026-10-30 22:53-22:54).

To try them: ECHO tab, section "ECHO decisions": "Recruit willing prisoners before hiring", "Make room for short crew types" (both need ECHO's buy-crew setting in the Autopilot tab's crew settings), "Hire only crew that fits aboard" (already on). Settings tab, "Fleet shield and armor bars": "Bar height (px)".

Next candidates: boarding results with `RebalanceCrew` on (more Marines per boarding); `EchoCargo` `SellAbove` and the fleet auto-deposit path in play; the older in-play leftovers (salvage and mining mission kinds, `CrewOnDock` and `EchoRetreat` in content that hurts, `FleetHolds` `Overflow`, the docked forge roll-up).

## 2026-10-31: `EchoCargo` selling seen in play (Phase 2b leftover)

No code change. A 20-minute ECHO soak on a dev save copy with three Sell rules added to its item rules (category TradeGoods, Turret / Standard, Module / Standard): at every dock ECHO sold the trade goods stack by stack ("Selling 52 Nutrient Paste", "Selling 16 Remote Hacking Tool", ...) and the Standard turrets and modules ("Selling 1 Scanner Mk.XV", "Selling 1 Core Buster Mk.XV"), while other stacks, including higher-rarity turrets (Tractor Beam Mk.XV, DT Chaingun Mk.XV), went to storage as usual. Credits +2.21M over the soak. Plan overview row 15 updated. Not seen: `SellAbove`, and the fleet auto-deposit path (`autoDepositFleetCargo` is off in your save).

## 2026-10-31: fleet bar height (`FleetVitals` `BarHeight`)

Why: you said the fleet vitals were hard to see; the bars were fixed at 4 px (the game's own hull bar).

Changed `src/IgiQoL/Features/FleetVitals/FleetVitals.cs`: config `BarHeight` (2-12, default 4); `FleetVitals.Layout()` stacks shield, hull and armor from the hull bar's original top at the configured height and grows the row, the merc contract's collapsed height and FleetStrip's tile row by the difference from the last layout, so a change applies to the live fleet panel at once (`SettingChanged` → every `FleetVitals.Layout`). Check `bar-height`; `layout` now reads the configured height. README row, plan §12.

Verified: `OnlyChecks = FleetVitals,FleetStrip,Contacts` with `BarHeight = 8`: all pass (`layout` bars 8 px, row 57; `bar-height` 8 → 12: row 77 → 89, strip moved 12; `FleetStrip/layout` tiles 4 px under the armor slot). Screenshot `igiqol-contact-markers.png` shows the 8 px bars in the fleet panel with the tile row under them. Your cfg is back at 4; set it on the Settings tab ("Fleet shield and armor bars", "Bar height (px)").

## 2026-10-31: ECHO makes room for short crew types (`EchoGates` `RebalanceCrew`, plan #40)

Why: with the dev flagship full of Gunners (40 against a 23 share) ECHO could never reach the 60% Marine share you set, and `PurchaseCrewIfNeeded` returns at once on a full ship. Boardings ran with 32 Marines instead of 54.

Changed `src/IgiQoL/Features/EchoGates/EchoGates.cs`: config `RebalanceCrew` (off); `EchoGates.Rebalance(ship, station)` moves crew above their share to the crew reserve, no more than the short types can get right away (reserve, willing prisoners with `RecruitPrisoners`, the personnel center within your credits) minus the free places and within the reserve's room, biggest surplus first, never a type with no share; prefix on `IdleManager.PurchaseCrewIfNeeded`. `Brigs(ship)` factored out of `RecruitWilling`. Log `[EchoGates] moved 17 Gunner, 5 Deckhand from Eclipse to the crew reserve ...`. Check `rebalance`. README row, plan overview row 40 and §40.

Verified: `OnlyChecks = EchoGates` 8 pass, 0 fail, 1 skip; a 20-minute soak with the three crew options on: first dock moved 17 Gunners and 5 Deckhands, recruited 18 Marines, filled Prospectors and Salvagers; later docks moved 1 Deckhand and refilled 2 lost Gunners from the reserve.

Not checked: boarding results with the fuller Marine complement.

## 2026-10-31: ECHO pays only for crew that fits (`EchoGates` `HireOnlyWhatFits`, plan #39); repair wait (`QuickStationSteps`)

Why: the `RecruitPrisoners` soak below recruited only 3-4 Marines per dock (the free crew space) while credits dropped by another 10-14 Marines at 16,000 cr each. Cause, in the game: `IdleManager.TryHireCrewType` asks for a type's whole shortfall against the flagship's crew ratios, `TransferFromReserve` removes all of it from the reserve and `BuyCrewFromStation` charges for all of it, but `SpaceShipData.AddCrew` only adds what fits and neither caller uses its return. The dev flagship had 40 Gunners for a 25% share, so the Marine shortfall (14-22) was always bigger than the free places (3-8). This also explains the "42-53 Marines hired" in the eleventh session's soaks: mostly paid for, not aboard.

Changed `src/IgiQoL/Features/EchoGates/EchoGates.cs`:
- Config `HireOnlyWhatFits` (on; the only `EchoGates` option whose default differs from the game, said so in its text and the feature description). Prefix replacing `IdleManager.TransferFromReserve` (cap at free crew space; also runs the prisoner recruiting, see below) and prefix replacing `IdleManager.BuyCrewFromStation` (cap at free space, still returns what's missing, so a dock where nothing fits counts as "nothing done" and ECHO moves on). Log line `[EchoGates] hiring N of M <type>: only N fit aboard <ship>`.
- `QuickStationSteps` now also covers repairs: `FindActivityPostfix` sets `repairPending` when a docked decision ends with the flagship's hull or armour short; a new postfix on `IdleManager.Update` sets `updateTimer` to 2 s on the first frame both are full (log `[EchoGates] repairs done: ...`). Description updated.
- Checks `hire-fits` (10 free, 13 asked: reserve gives 10 and keeps 3, station sells 10 for 70,000 cr; the game's method on the same ship charged 91,000 cr for 13 with 10 aboard) and `repair-wait` (hull 90% → 12 s, full → 2 s, then nothing more).

Verified: `OnlyChecks = EchoGates`: 7 pass, 0 fail, 1 skip (`dungeon-min`, as always). A/B soaks, 20 min each from the same dev save, `RecruitPrisoners` on: game purchase 624,000 cr on crew (35 Marines paid for, none aboard), `HireOnlyWhatFits` 70,400 cr (11 recruits, nothing paid for nothing). Table in plan §39.

Not checked: a repair wait in a soak (hull stayed at 100% again); the cap on the reserve transfer in a soak (the dev reserve had only 4 Marines).

## 2026-10-31: ECHO recruits willing prisoners (`EchoGates` `RecruitPrisoners`, idea list "better AFK mechanics", plan #38)

Why: ECHO tops up the flagship's crew from the reserve and then the personnel center at full price; it never recruits prisoners, who cost 40% of the crew price once willing (`SpaceShipData.RecruitWillingByType`). Your save had 134 willing prisoners in the Eclipse and Hurricane brigs (21 Marines in Hurricane's).

Changed `src/IgiQoL/Features/EchoGates/EchoGates.cs`: config `RecruitPrisoners` (off); `EchoGates.RecruitWilling(type, amount, ship)` recruits from the ship's own brig, the active fleet's brigs, then the station brig, capped by free crew space and credits, charged via `RemoveCreditsForCrew`; called from the `TransferFromReserve` prefix for what the reserve didn't give, only when the flagship's crew settings allow purchases. Log `[EchoGates] recruited N willing <type> prisoners into <ship> for X cr`. Check `recruit-prisoners` (2 willing in the flagship's brig + 1 in the station brig fill a shortfall of 3 at 3 × 2,800 cr; 0 with the option off or purchases off). README row, plan overview row 38 and §38.

Verified: the check above, and the real path in the soaks (`TryHireCrewType` → "Hiring 8 Marine" after 4 from the reserve and 4 recruited).

## 2026-10-31: end of the twelfth loop session

This session added `EchoGates` `QuickStationSteps` (measured with two 20-minute ECHO soaks) and a "never pick up" value (-1) for item rule pickup limits. Final full harness run: 34 active, 149 pass, 0 fail, 5 skip (the same five as before: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`, `DroneModes/gatherer-assist`). Plain launch to the main menu: `33 active, 1 disabled` (Dev), no `switched off`. `[Dev] Enabled = false`, `OnlyChecks` empty, `EchoSoakSeconds = 0`, other `[Dev]` values unchanged; `QuickStationSteps = false` (the game's timing). The dev save and `/tmp/igiqol-launch.sh` are deleted (the helper's wait loop was `seq 1 1800` for the 1200 s soaks). Your own saves were not written (mtimes still 2026-10-30).

To try them: ECHO tab, section "ECHO decisions", "Quick station steps". Item rules tab: type -1 as a pickup limit, for example on an "Add a category + rarity rule" row for Turret / Standard.

Next candidates: the wait after a docked repair finishes (ECHO checks repairs once per decision, up to 12 s late; no soak hit it because hull stayed at 100%); the older in-play leftovers (salvage and mining mission kinds, `CrewOnDock` and `EchoRetreat` in content that hurts, `FleetHolds` `Overflow`, `EchoCargo` selling, the docked forge roll-up).

## 2026-10-31: quick station steps (`EchoGates` `QuickStationSteps`, idea list "better AFK mechanics", plan #36)

Why: ECHO makes one decision per 12 s (8-10 s with Prompt Engineering), and a station visit is a chain of one-step decisions (docking request, turn in, unload, hire, take a mission, leave). In a 20-minute soak with the game's timing, arrival to departure took 49-68 s per visit.

Changed `src/IgiQoL/Features/EchoGates/EchoGates.cs`: config `QuickStationSteps` (off) and `QuickStepSeconds = 2` (the game's floor in `SetActivityDelay`). Prefix/postfix on `IdleManager.FindActivity`: while docked, the prefix records missions held, flagship crew, flagship cargo volume and credits; the postfix sets `updateTimer` to 2 s when one changed and ECHO isn't travelling. Postfix on `SpaceStationInterior.Start`: once docked with ECHO on, the timer drops to 2 s. Decisions that change nothing (waiting for repairs, an empty board) keep the game's timer, so the number of decisions per visit and the autopilot mastery experience they grant are unchanged. Log lines `[EchoGates] quick station step after '<status>'` and `[EchoGates] docked: next step in 2 s instead of N s`. Check `EchoGates/quick-steps`. Feature description, README row, plan overview row 36 and §36.

Verified: two 20-minute soaks from the same dev save copy (`MissionsAtOnce` 1, all mission kinds). Game timing: 4 visits, 49/68/51/68 s, 12 docked samples of 80, 4 missions turned in, +1.22M credits. `QuickStationSteps`: 6 visits, 28/32/25/33/19/20 s, 5 docked samples, 6 missions turned in, +1.20M credits (both ran two installation explorations; the missions drawn diverge after the first visit). What's left of a visit is the docking flight (about 20 s from arrival to the station interior) and unloading.

Not checked: a visit that waits for repairs (hull stayed at 100% in both soaks); Prompt Engineering's shorter timers (the dev save has none).

## 2026-10-31: never pick up (`TractorCaps` pickup limit -1, wishlist "Avoids automatically tractoring certain items", plan #37)

Why: the pickup limit stopped auto-tractoring only after N items, and 0 meant no limit, so there was no way to leave an item alone entirely.

Changed: `Rules/ItemRules.cs` (`ItemRule.Never = -1`, loading accepts it), `Features/TractorCaps/TractorCaps.cs` (`Allows` returns false for Never unless the item is exempt; the mission-item and used-ammo exemptions of the value filter moved into `Exempt` and apply to both; new check `never`; description), `Features/ItemRulesTab/ItemRulesTab.cs` (the field accepts -1; tab intro and column tooltip explain it). README `TractorCaps` row, plan overview row 37 and §37.

Verified: `TractorCaps/never` (cap -1: EmptyCell refused, the flagship's PlasmaCannon Ammo still allowed). A throwaway probe (removed) spawned 3 EmptyCell loot next to the flagship in space with an item rule at -1: `CanBeAutoTractoredBy` false and still floating after 10 s; with the rule gone it was tractored 2.1 s later. Note for future probes: the game never auto-tractors loot spawned with `jettisoned = true`. Screenshot `igiqol-window-itemrules.png` shows the new intro text.

## 2026-10-31: end of the eleventh loop session

This session ran ECHO soaks to see where AFK time goes and added the `EchoMissions` feature (several missions per trip, mission kind switches). Final full harness run: 34 active, 147 pass, 0 fail, 5 skip (the same five as before: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`, `DroneModes/gatherer-assist`). Plain launch to the main menu: `33 active, 1 disabled` (Dev), no `switched off`. `[Dev] Enabled = false`, `OnlyChecks` empty, `EchoSoakSeconds = 0`, `Screenshot = true` again (it was off for the soaks), other `[Dev]` values unchanged; `[EchoMissions]` is at the game's values (`MissionsAtOnce = 1`, every kind on). The dev save, `/tmp/igiqol-launch.sh` and the unzipped save copy in `/tmp/vg` are deleted. Your own saves were not written (mtimes still 2026-10-30 22:53-22:54).

To try it: ECHO tab, section "ECHO missions": set "Missions ECHO takes at once" to 3, and if you want credits, leave only "Combat: Clear Pirate Base" on (for a combat flagship). Your saved ECHO sessions ran with Salvage preferred, where the kinds are "Recover (item)" and "Salvage (material)"; those weren't compared.

Next candidates: the salvage and mining kinds in a soak with a ship that prefers them; whether ECHO's other station steps (docking request, hiring, pick-up, each one decision of about 12 s) can share decisions the way turn-ins now do; the older in-play leftovers (a soak in content that hurts for `CrewOnDock` and `EchoRetreat`, `FleetHolds` `Overflow`, `EchoCargo` selling, the docked forge roll-up).

## 2026-10-31: mission kinds for ECHO (`EchoMissions` kind switches, idea list "better AFK mechanics", plan #35)

Why: ECHO's missions are drawn at random among the kinds for the flagship's preferred activity, and the game only lets you rule out the two operation kinds and couriers. The soaks below showed how unequal the kinds are: an installation exploration ("Clear Pirate Base") found 0.3-1.3M credits aboard, the other combat missions paid 5-50K.

Added `src/IgiQoL/Features/EchoMissions/EchoMissions.cs`: one switch per idle-able kind (all on = the game), labelled with the game's mission name, e.g. "Combat: Clear Pirate Base", "Salvage: Recover (item)", "Mining: Support (ore) Mining" (names read from the game's translations in a probe run). Prefix/finalizer on `MissionBoard.GenerateIdleMission` marks ECHO's draws; a prefix on the static `MissionGenerator.GenerateMission(gen, ...)` returns no mission for a kind that is off, before the generator creates a location, and the game draws again (10 draws per decision). With every kind off, ECHO skips missions while it holds none. Couriers stay on the Autopilot tab's own switch. Checks `EchoMissions/kinds` (only Clear Pirate Base on: 6 of 6 offers are Clear Pirate Base), `kinds-none`, `tab` (screenshot `igiqol-window-echo-missions.png`: the section with the at-once row and 10 kind rows, nothing cut off).

Verified: a 30-minute soak with only Clear Pirate Base on and `MissionsAtOnce` 3 ran 7 explorations and ended +2.98M credits, against +1.21M with the game's settings and +1.86M with 3 at once and all kinds (table in plan §35; the board's draws are seeded, so all three runs met the same first explorations). Marines hired back: 42, 44 and 53. Experience and reputation weren't compared.

## 2026-10-31: several missions per ECHO trip (`EchoMissions` `MissionsAtOnce`, idea list "better AFK mechanics", plan #34)

Why: a 30-minute ECHO soak on the dev save (missions on, game settings) spent 42% of its time travelling and 20% docked, sampled every 15 s. Every mission was a round trip from Tower I: take one, fly out, fly back, dock, turn it in, take the next. Your saved ECHO sessions are the same kind (138 missions in an 18.5-hour session).

Added the new feature `EchoMissions` (ECHO tab, section "ECHO missions", registered after `EchoGates`) with `MissionsAtOnce` (1-5, default 1 = the game), in `src/IgiQoL/Features/EchoMissions/MissionBatch.cs`. With 3: docked at a board, ECHO takes missions until it holds 3, in one decision, and puts the tracker on the nearest; when a mission is done or a location has nothing left, it flies to the nearest open mission in the system instead of home; back home, every finished mission is turned in in one decision. It still goes home first for a full hold, ammo it can restock, crew below the hire threshold, or a flagship under half hull or armour. Ammo and crew trips (calls with a message or `overrideClosests`) are untouched. Patches: prefix `DecideMissionActivity`, prefix `IdleTravelToSpaceStation`, postfix `GetIdleMission`, postfix `TryClaimCompletedMission` (all `IdleManager`). Checks `EchoMissions/missions-at-once` (held 1 → 3, two missions taken, tracker on the nearest) and `missions-at-once-off` (limit 1 hands the decision to the game). README `EchoMissions` row, plan overview rows 34-35 and §34-35.

Verified: soak A/B from the same dev save copy (table in plan §34). Game: 9 missions, 18 trips, 9 dockings, +1.21M credits. `MissionsAtOnce = 3`: 9 missions finished (2 still to turn in at the end), 15 trips of which 2 were cargo runs (an installation exploration's loot filled the hold to 1060/1060), 4 dockings, +1.86M credits. A 15-minute run after adding the turn-in postfix: "Turning in 3 missions" in one decision instead of three decisions 12 s apart.

Changed on the way: the setting started in `EchoGates` and moved to its own feature once the kind switches joined it (the `[EchoGates] MissionsAtOnce` line was removed from your cfg). The first `missions-at-once-off` check called the game's own decision with missions held, which undocked the flagship and flew it out; `Window/station-button` then skipped ("not docked") and `Separation/live` failed on the left-over travel destination. The check now calls the prefix alone.

Looked at and dropped this session: ECHO picking the nearest salvage, combat or mining location instead of the first in the system's list (in Red Gate the first wreck field is 17.5 map units from Tower I, the nearest 7.4), dropped because with "prefer missions" on and a mission board at the station `TryFindNewMission` returns true even when no mission comes up, so ECHO never reaches those picks there, and your ECHO runs missions. Fleet ships falling back to recharge shields: an enemy keeps its auto-target until it dies or leaves range (`AbstractTargetingModule.UpdateAutoTarget` returns while it has one), so a ship that backs off pulls its attacker along. A distance filter for ECHO's missions: every idle mission generator places its location in the station's own system. Sorting the missions window by distance: you hold 5 missions. A list of Umbral hacking targets: your save has 31 of the 34 infected stations "Growing The Reach" asks for.

Not checked: a mission in another system among the batch (left to the game); `MissionsAtOnce` with "prefer missions" off; salvage and mining kinds in play (the dev flagship prefers combat).

## 2026-10-31: end of the tenth loop session

This session added `MissionDistance`. Final full harness run: 33 active, 142 pass, 0 fail, 5 skip (the same five as before: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`, `DroneModes/gatherer-assist`). Plain launch to the main menu: `32 active, 1 disabled` (Dev), no `switched off`. `[Dev] Enabled = false`, `OnlyChecks` empty, other `[Dev]` values unchanged; the dev save and `/tmp/igiqol-launch.sh` are deleted. Your own saves were not written (mtimes still 2026-10-30 22:53-22:54).

Next candidates: unchanged from the ninth session (a soak in content that hurts for `CrewOnDock` and `EchoRetreat`; a "recruit willing prisoners into the reserve" service once the station brig fills; the older in-play leftovers). Possibly also sorting the missions window by distance, now that the number exists; I left it out because it changes the game's own order.

## 2026-10-31: mission distance (`MissionDistance`, idea list "operation menus and instruction clarity", plan #33)

Why: the mission board, the missions window (side panel Missions tab, `MissionUI`) and the HUD tracker (`FocusedMissionDisplay`) name a mission's location but never its system or distance, so you press Locate per mission to see how far it is. `TravelManager.JumpDistanceToPoi` already counts the jump gates on the game's own shortest route.

Added `src/IgiQoL/Features/MissionDistance/`:
- `MissionDistance.cs`: `Jumps(poi)` (0 in this system, -1 without a route; cached per location until the system changes or 10 s pass), `Short` ("here", "this system", "2 jumps", non-breaking spaces so the tracker doesn't wrap "1 / jump"), `Long` ("Red Gate, 2 jumps"), `RowJumps(mission)` (0 unless `GetActivePoi()` is in another system). Patches: postfix on the private `MissionListRow.Init` adds a chip to the row's `Text/Tags` row with the game's `JumpGate` icon and the number, tooltip = location name + `Long`; postfix on `MissionDetailPane.AddLocationLine` appends " (Red Gate, 2 jumps)" to the location line; postfix on `MissionDetailPane.GetState` adds the current location's guid so an open pane rebuilds after a jump; postfix on `FocusedMissionDisplay.GetLocationLines` rebuilds its lines with the distance.
- `MissionDistanceCheck.cs`: `jumps` (Tower I → "here", Asteroid Field T-20 → "this system", a POI in The Fallow Rift → "The Fallow Rift, 2 jumps", equal to `GalaxyMapData.GetJumpDistances`), `tracker` (the patched private method on an accepted mission), `board-rows` and `board-detail` (opens the board with `GoToLocation(MissionBoard)`), `board-far` (a station board in another system is generated with `RegenerateMissions` when empty and emptied again in the cleanup; one of its missions is built as a row at the top of Tower I's board and must carry the tag), `window-rows` (opens the side panel's Missions tab, compares every row's tag and tooltip, and fails if a tag leaves a progress bar under 40 px). Registered after `StationServices`, before `EchoRetreat`; docks itself if needed.
- README row, plan overview row 33 and §33.

Changed on the way: the first version tagged every row ("this system" / "here"). Tower I's 9 board missions were all in its own system, the tag cut "Combat" off in the 250 px list, and in the missions window's 190 px list "38 jumps" squeezed the progress bar to a stub. Rows now only get a tag for other systems, as icon + number; the bar keeps 44 of 84 px (screenshots `igiqol-mission-board-distance.png`, `igiqol-missions-window-distance.png`).

Verified: the six checks above (window: "Assignment: Forgemaster" tagged 38 with tooltip "Zorix, 38 jumps", "The Intertrade Challenge" 30 "Tau Sarus, 30 jumps", three local missions untagged). A throwaway probe (removed) flew one jump to Daelis Array in The Veiled Field: the tracker then read "Location: Fractured Crossroads (Red Gate, 1 jump)", so the cache follows the system change.

Not checked: wormhole routes (`JumpDistanceToPoi` counts only jump gates, so a route through a wormhole shows fewer jumps than travel stops); a mission whose location has no known route (no tag and no suffix by design).

## 2026-10-31: end of the ninth loop session

This session ran a 25-minute ECHO soak on your save to look for AFK problems, then added two `StationServices` services (Replace crew, Unload brigs) and fixed the `EventLog` operation line. Final full harness run: 32 active, 136 pass, 0 fail, 5 skip (the same five as before: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`, `DroneModes/gatherer-assist`), plus a 15-minute soak with `CrewOnDock` and `BrigOnDock` on. No plain launch to the main menu this time; every run reported `32 active, 0 disabled, 0 failed` and no `switched off`. `[Dev] Enabled = false`, `OnlyChecks` empty, `EchoSoakSeconds = 0`, `CrewOnDock` / `BrigOnDock` back to false (your other `StationServices` on-dock options were already off); the dev save and `/tmp/igiqol-launch.sh` are deleted. Your own saves were not written.

What the first soak showed (25 min, Tower I, missions only): ECHO ran 6 missions (2 pirate bases, 2 boardings, asteroid and salvage reclaims) for +1.36M credits; hull stayed at 100%, nothing got stuck. Installation explorations take 3.5-4 min each. Two real findings, both fixed below: the operation line counted credits as items ("1266694 items from 20 loot kinds"), and "Brig full" after a 30-prisoner boarding, with both of your ships' brigs full (68 prisoners each, 134 willing to join) and the station brig empty.

Next candidates: a soak in content that hurts (to see `CrewOnDock` replace real hull-hit deaths and `EchoRetreat` fire on its own); whether the station brig's 96 places need a "recruit willing prisoners into the reserve" service once they fill; the older in-play leftovers (`FleetHolds` `Overflow`, `EchoCargo` selling, the docked forge roll-up, `OrePreference` with a mining fleet).

## 2026-10-31: fleet crew replacement and brig unloading (`StationServices`, plan #32)

Why: every hull hit of 2% or more can kill a crew member on any of your ships (`SpaceShipData.TryKillCrewFromHullDamage`, 2-12% per hit), but ECHO only hires for the flagship (`IdleManager.PurchaseCrewIfNeeded` on `currentSpaceShip`, by its ratios), and the personnel center only hires into the flown ship or the reserve. A fleet ship's losses never came back unless you moved crew from the reserve type by type. Separately, full brigs make boarding captives float away ("Brig full").

Added:
- `Features/StationServices/CrewLosses.cs`: deaths per ship guid and crew type in the save (`features.StationServices.crewLost`). Postfix on `TryKillCrewFromHullDamage` counts a death on any ship in `GamePlayer.spaceShips`; postfix on `CrewData.AddCrew` subtracts any later addition of that type to that ship (by you, ECHO, a loadout or the service), so nothing is replaced twice.
- `Services.Crew(run)`: per fleet ship and type, up to the ship's free crew space: reserve (free), willing prisoners in the ship's brig, then the station brig (40% of `CrewType.cost`, as `RecruitWillingByType`), then the docked station's personnel center (`EnsureCrewPopulated`, `TryPurchaseCrew`, `GetBuyPrice`). Quotes track reserve, brig and stock use so the button shows what a click does. A ship that ends up full has its count cleared.
- `Services.Brig(run)`: every fleet ship's brig into `GamePlayer.stationPrisoners` up to `maxBrig` (96 in your save), willing counts carried like `CrewReserve.TransferShipBrigPrisoners`; what doesn't fit stays aboard with a problem line.
- `StationServices.cs`: config `CrewOnDock`, `BrigOnDock` (off); both run in the on-dock pass after restock; `Summary` gained "replaced N crew (cr)" and "moved N prisoners to the station brig"; `Notify` takes `params`; on-dock problem notices show once per station visit (the first full run showed "The station brig is full (96): 40 prisoners stay aboard Hurricane." 10 times in 10 s).
- `AirlockServices.cs`: "Replace crew [N cr]" and "Unload brigs" buttons; the row ordering works on an array of all five. `Airlock.LayoutButtonRow` never wraps, and five buttons plus Trade Routes ran into Undock, so once a button's right edge passes the Undock button (found by its label) the buttons switch to short labels (Repair, Ammo, Sell, Crew, Brig) for the rest of that airlock.
- `StationServicesCheck.cs`: `crew-track` (real `TryKillCrewFromHullDamage` calls until one kills; count +1, adding one back → 0), `crew-reserve`, `crew-brig`, `crew-hire`, `brig`, `crew-on-dock`, the crew click in `airlock`, and `airlock-full` (all five shown, no overlap with Undock, screenshot `igiqol-airlock-services-all.png`).
- README row, plan overview row 32 and §32.

Verified (dev save at Tower I): `crew-track` (Hurricane: a Gunner died, 86 → 85, count 1, back to 0), `crew-reserve` (3 Gunners back from the reserve, no credits spent), `crew-brig` (2 Gunners recruited from the Hurricane's brig for 2 × 2,800 cr), `crew-hire` (2 hired, stock 34 → 32, 2 × 7,000 cr), `brig` (4 prisoners, 3 willing, all in the station brig), `crew-on-dock`, `airlock`, `airlock-full` (`compact=True overflow=False`, labels `Repair 83.0K cr`, `Ammo 5,364 cr`, `Sell +65 cr`, `Crew`, `Brig`). In the 15-minute soak with both options on, the first dock moved 96 prisoners to the station brig (40 stayed aboard the Hurricane), and a later 23-prisoner boarding fit aboard without "Brig full". The later "moved 2 prisoners" docks in that soak ran on brigs the harness had left changed: the checks snapshotted brigs and crew several times and the harness runs cleanups first-to-last, so the last snapshot won. Fixed by snapshotting once at the start of `StationServicesCheck.Run`; rerun of the StationServices checks: 18 pass, 0 fail.

Not checked: a real hull-hit death being replaced on dock (no ship took a 2% hit in either soak); a station without a personnel center (the problem text is in place).

## 2026-10-31: credits in the operation line (`EventLog`)

`EventLog.OperationText` summed every loot line, and credits found aboard come as a loot line built by the "Credits" item builder, so "315800 items from 27 loot kinds" was mostly credits (the credit balance rose by 315,611 during that operation). Credits (`builderIdentifier == "Credits"`) are now left out of the item count and kinds and added as ", 1.21M credits". `EventLogCheck` `operation` has a credits line in its test entry; verified in the soak ("152 items from 19 loot kinds, 1.21M credits").

## 2026-10-31: end of the eighth loop session

This session measured performance (`[Dev] ProfileSeconds`, nothing in the mod needed fixing) and added `EchoRetreat`. Final harness run: 32 active, 129 pass, 0 fail, 5 skip (the same five as before: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`, `DroneModes/gatherer-assist`). Plain launch to the main menu: `31 active, 1 disabled` (Dev), no `switched off`. `[Dev] Enabled = false`, `OnlyChecks` empty, `ProfileSeconds = 0`, `ProfileEnemies = 12`, `Separation` `Default`/`OtherShips` back to false, other `[Dev]` values unchanged; the dev save, `/tmp/igiqol-launch.sh` and `/tmp/dev.json` are deleted. Your own saves were not written.

Build note: the system `dotnet` has no SDK; from a non-fish shell (the agent's bash) run `~/.dotnet/dotnet build -c Release src/IgiQoL`. The launch helper's wait loop is now `seq 1 900` so a full harness (about 190 s) plus a soak fits.

Looked at and dropped: buying missing crafting materials for `CraftRollup` (station shops only sell fuel, ammo, mines, beacons and usables, `Source.Galaxy.MapSearch/ShopOfferings.cs`); an ECHO mission filter by distance or reward (ECHO generates its idle missions on the spot with `MissionBoard.GenerateIdleMission`, Easy difficulty unless Prompt Engineering's mission skill; no data yet on how far they send it); a "claim all missions" button (ECHO already claims its own, and you claim yours with one click on the mission board).

Next candidates: an ECHO soak with `EchoRetreat` on in a fight the fleet can lose; an ECHO mining run with `OrePreference` once a ship with mining turrets or drones is in the fleet; the in-play leftovers from earlier sessions (`FleetHolds` `Overflow` in a long AFK session, `EchoCargo` selling, the docked forge roll-up, `RepairOnDock` with ECHO); accessibility items in plan §22 once you name a screen.

## 2026-10-31: ECHO retreat (`EchoRetreat`, idea list "better AFK mechanics", plan #30)

Why: ECHO never leaves a fight. The flagship fights to 0 hull, then the game's emergency jump (`SpaceShip.TryEmergencyJump`) sends it to the nearest station with weapons off; every hull hit of 2% or more rolls a crew death (`TryNotifyCrewKilled`). Your save's `Register` counts 111 emergency jumps, and the 10 autopilot sessions it keeps had 50 in 48 hours.

Added `src/IgiQoL/Features/EchoRetreat/`:
- `EchoRetreat.cs`: `SettingsTab = "ECHO"`, config `FlagshipHullPercent` and `FleetHullPercent` (0-90, 0 = off). Postfix on `IdleManager.Update` while `autoPlay`; every 0.5 s `Tick`: `CanLeaveHere()` (a location that isn't a station, no travel, no emergency jump, no waypoints, no running operation) and `LowShip()` (flagship, then fleet ships here that aren't travelling or away for repairs). `Retreat`: `SetIdleCombatMode(false)`, `TravelManager.CanWeTravel()` at most every 10 s (it pops a notice on each no), `TravelToClosestSpacestation()`, `idleTravelTarget` set, status "Retreating to Tower I for repairs: Eclipse hull at 20%", log line `[EchoRetreat] ...`. The Autopilot tab's "no travel" makes it say so every 30 s and stay. Registered last, so its check (which flies away from the station) runs after the docked checks.
- `EchoRetreatCheck.cs`: `tab`, `low-ship`, `at-station`, then flies to the first Combat/Mining/Salvage location in the system and runs `retreat` and `dock-repair` with ECHO on (ECHO's own next decision is held off with `updateTimer = 30` so its "going home" can't win the race; the first full run failed on exactly that).
- `EchoPriority.BuildEcho`: "ECHO retreat" section after ECHO decisions.
- README row, plan overview row 30 and §30-31.

Verified: `EchoRetreat/tab` (screenshot `igiqol-window-echo-retreat.png`: heading, description, both rows), `low-ship` (threshold 50: flagship at 40% → "Eclipse hull at 40%", 60% → none, threshold 0 → none; Hurricane at 30% with fleet threshold 50 → "Hurricane hull at 30%"), `at-station` (no retreat next to Tower I), `retreat` (at Asteroid Field T-20 with the flagship at 20% and threshold 30: ECHO left for Tower I with the status above), `dock-repair` (ECHO docked at Tower I by itself, a hangar repair job for the flagship was running, hull 31% and climbing).

Not checked: a real fight where the hull drops on its own; what ECHO does when the nearest friendly station has no hangar (the same as after a vanilla emergency jump).

## 2026-10-31: performance measured (`[Dev] ProfileSeconds`, idea list "optimizations", plan #31)

Added `src/IgiQoL/Core/DevProfile.cs` and `[Dev] ProfileSeconds` (0 = off) / `ProfileEnemies` (12): after the checks it undocks, spawns hostile hulls with `SelfCheckContext.SpawnEnemies`, times every Harmony prefix/postfix/finalizer this mod owns and every `Update`/`LateUpdate`/`FixedUpdate` of its MonoBehaviours (a second Harmony instance `igi.qol.profile` patches those methods with a Stopwatch prefix/postfix, removed afterwards), records frame times and turns on the game's own `SimMarker` totals. Report: `BepInEx/config/IgiQoL/dumps/profile.txt` and the log.

Results (table in plan §30-31): 12 spawned (5.9 hostiles on average, 58 units): mean frame 8.76 ms at the game's 120 fps cap, the mod 0.081 ms (0.9%). 40 spawned with `Separation` `Default` + `OtherShips` on (27.6 hostiles, 81 units): mean 11.47 ms, p99 22.5 ms, the mod 0.269 ms (2.3%), led by the `TargetPriority` `currentTarget` getter postfix (471 calls/frame, 0.25 µs each, mostly the profiler's own Stopwatch cost) and the `Separation` `ApplyThrust` prefix (5.3 µs per call with `OtherShips`). The game's instrumented script sections add up to about 2 ms per frame. Nothing was changed: no mod method is worth optimizing, and the rest of the frame is rendering and physics. The game ships `Behaviour.Benchmark.FleetBenchmark` (console `fleetbench`, Test Arena only), so its developers profile this themselves.

## 2026-10-31: end of the seventh loop session

This session added warp fuel restock (`StationServices` `FuelCells`), `OrePreference`, asteroid ores in the Contacts tab plus `MarkOres` edge markers, asteroid and wreck targets with a "Whenever ready" rule in `EchoKit`, `EventLog` (with operation results), `FleetHolds` and the `TractorCaps` value-per-m3 filter; harness gained `[Dev] EchoSoakSeconds` and protection against exceptions in nested check coroutines. Final harness run: 31 active, 124 pass, 0 fail, 5 skip (the same five as before). Plain launch to the main menu: `30 active, 1 disabled` (Dev), no `switched off`. `[Dev] Enabled = false`, `OnlyChecks` empty, `EchoSoakSeconds = 0`, other `[Dev]` values unchanged; the dev save, `/tmp/igiqol-launch.sh` and `/tmp/dev.json` (unzipped save used to count hardpoints) are deleted. Your own saves were not written (mtimes still 2026-10-30).

Harness notes: when you recreate the launch helper, raise its wait loop (`seq 1 300`) above the soak length if you use `EchoSoakSeconds`. A 600 s soak from the dev save runs about two missions and one installation operation. Checks that need space call `SelfCheckContext.EnsureInSpace()`; `SpawnAsteroid(position, ore)` / `RemoveAsteroid(data)` give a check asteroids anywhere.

Looked at and dropped: operation-panel clarity (the game's own texts already explain every stealth, ammo, integrity and morale option, e.g. "Silent: -20% attack, -25% casualties"); a scroll for the per-ship popup (no ship in your save has more than 5 hardpoints); ECHO's salvage item order (`SalvageData.availableItemContent` lets every available item be extracted, so the order barely matters).

Next candidates: an ECHO mining run with `OrePreference` once a ship with mining turrets or drones is in the fleet (`EchoSoakSeconds` can do it); `FleetHolds` `Overflow` in a long AFK session; firing Crackshot Drone / LB-RTR / SF-CRK from a ship that has them; the in-play leftovers from earlier sessions (`EchoCargo` selling, the docked forge roll-up, `RepairOnDock` with ECHO).

## 2026-10-31: edge markers for preferred-ore asteroids (`Contacts` `MarkOres`)

Changed `Features/Contacts/ContactMarkers.cs`: a fourth marker kind, `Ore` (8 slots, "Mining" icon): asteroids from `OrePreference.SceneAsteroids()` that `OrePreference.HasPreferred(data, surface, core)` accepts, tinted by the listed ore's rarity; click → `Contacts.Mine`. Shown only while `MarkOres` (on) and the Preferred ores list isn't empty. `Contacts.cs`: config `MarkOres`, check `ore-markers`.

Verified: `Contacts/ore-markers` (a preferred Amberic asteroid 400 units below the ship is marked on the bottom edge; a Cerrax one 400 units to the side isn't), plus the other Contacts checks.

## 2026-10-31: loot filter by value per m3 (`TractorCaps` `MinCreditsPerM3`)

Why: the loot filter (idea list "expanding on loot filters") only had per-item, category and category + rarity pickup limits; cheap bulky loot still fills the hold.

Changed:
- `Features/TractorCaps/TractorCaps.cs`: config `MinCreditsPerM3` (int, 0 = off). `TooCheap(unit, item)`: `sellValue < N × m3`, except items with `RequiredItemCountForMissions > 0` and ammo the tractoring unit uses. `Allows` now takes the unit; the `CanBeAutoTractoredBy` postfix passes it. ECHO's `TryLootBeforeLeaving` asks `CanBeAutoTractoredBy` too, so it doesn't wait for filtered loot.
- `Features/ItemRulesTab/ItemRulesTab.cs`: a "Loot pickup limits" section with the feature's options above "ECHO cargo rules". Before this the Item rules tab showed no TractorCaps options, and since the feature sets `SettingsTab = "Item rules"` the Settings tab only had its on/off switch.
- README `TractorCaps` row rewritten (it still described the old JSON-file rules).

Verified: `TractorCaps/value` (Empty Cell, 64 cr per 0.5 m3 = 128/m3: line 129 → skipped, line 128 → picked up, 0 → picked up), `gate`, `ItemRules/tab` (8 rows; screenshot shows "Skip loot worth less per m3 than" under its heading).

## 2026-10-31: shared fleet holds (`FleetHolds`, idea list "cargo management", plan #29); operation results in the event log

Why: `TractorModule.Update` calls `IdleManager.TriggerInventoryFull` as soon as any of your ships (`IsPlayer()`) sees loot that doesn't fit its own hold, and `TractorBeam.HandlePartialItemPickup` does the same when a stack only partly fits. ECHO then flies the whole fleet home (`HandleOutsideStation` → `IdleTravelToSpaceStation`), even when the flagship is nearly empty. `CanFitInCargo` only checks room for one unit.

Added `src/IgiQoL/Features/FleetHolds/`:
- `FleetHolds.cs`: config `Overflow` (off). Postfix on the private `TractorModule.CanFitInCargo(item)` for your ships (not carrier fighters or work cameos): returns at once while the whole stack fits; otherwise, at most every 2 s per ship, `MakeRoom` moves this ship's cargo, biggest stacks first, with the game's `PlayerCargo.MoveStack` to the flagship and then your other active-fleet ships here (`PlayerCargo.CanManage`, most room first) until the stack plus 25% of the hold is free, and re-answers the fit. `Keep`: favourites, ammo the ship uses (`DoWeUseAmmo`), usables, warp fuel, items with `RequiredItemCountForMissions > 0`. Logs `[FleetHolds] Hurricane passed 320 m3 on: ...`.
- `FleetHoldsCheck.cs`; registered after `OrePreference`.

Event log: postfix on `DungeonActivityLog.Record` adds an "Op" line per finished operation (ECHO's or yours): location, outcome, defenders killed/captured, crew killed/wounded, items found. New filter "Operations".

Verified (`OnlyChecks = EventLog,FleetHolds`): `FleetHolds/overflow` (Hurricane filled to 4.7 m3 free with a favourite stack and 10 of its own ammo added; 5 Cerrax loot: game alone "doesn't fit", with Overflow 32 Argenthyte (320 m3) went to Eclipse and it fits; favourite and ammo stayed), `throttle` (refilled at once: no second hand-over within 2 s), `EventLog/operation` (a recorded entry gives "ECHO: Mining Outpost at Check Field: succeeded; 4 defenders killed, 1 captured; 2 crew killed, 3 wounded; 6 items from 1 loot kinds"). A 600 s ECHO soak (EventLog only) ran two missions and an installation operation; the log read cleanly ("Calculating..." gone, one line per unloaded item kind).

Not checked: ECHO staying out longer in a real session with `Overflow` on (the soak's holds never filled).

## 2026-10-31: event log (`EventLog`, idea list "better AFK mechanics", plan #28); harness: ECHO soak and nested-coroutine protection

Why: notifications fade after a couple of seconds, the event lines above the HUD after 5 s (`EventLogManager` keeps no history; its `ItemLogEntry` for pickups is never used), and ECHO's status is a single line (`IdleManager.UpdateActivity` → `SidePanel.SetIdleStatus`). After an AFK stretch there was no way to see what happened.

Added `src/IgiQoL/Features/EventLog/`:
- `EventLog.cs`: up to 1000 entries in memory (not saved). Prefix on `NotificationBuilder.Show` (text and colour; the same text within 10 s counts up), prefix on `EventLogManager.NewEvent`, prefix on `SidePanel.SetIdleStatus` while `autoPlay` (only changes, "Calculating..." skipped), postfixes on `GamePlayer.StartAutopilotSession` / `EndAutopilotSession`, `GameEvents.Arrived` for the flown ship, postfix on `TractorableItem.AddItemToShipInventory` for your ships (same item within 60 s merges; session totals per item).
- `EventLogTab.cs`: "Log" tab (order 8): filter (Everything, Notifications, Event lines, ECHO, Arrivals, Loot), Clear, a pickup summary line with sell value (tooltip lists items), then up to 150 rows newest first: time, kind, text in the notice's colour; loot rows have the item tooltip.
- `EventLogCheck.cs`; registered after `CombatLog`.

Harness (`Core/DevFeature.cs`):
- `Protected` now steps through the coroutines a check yields itself, so an exception in a nested coroutine fails that check and the run continues; abandoned iterators are disposed so their `finally` blocks run. Before, such an exception stopped the harness and the game never quit (it happened with `StationServices/airlock`, see the fuel entry).
- `[Dev] EchoSoakSeconds` (0 = off): after the checks and their cleanups, turns ECHO on through `SidePanel.instance.autopilotTab.ToggleChanged()` for that many seconds, logs `[Dev] soak t=...: status=... at=... docked=... hull=... cargo=... credits=...` every 15 s, then every event log line. The launch helper's wait loop must be longer than the soak (raise `seq 1 300`).

Verified: `notice` (two identical notifications → one line "(x2)"), `event`, `echo` (status A, "Calculating...", A → one line), `loot` (two ore stacks through the game's pickup: one line x7, session total +7), `tab` (screenshot `igiqol-window-log.png`). A 240 s soak on the dev save: ECHO docked at Tower I, unloaded Hurricane, hired 22 Marines, flew to Fractured Crossroads for "Reclaim Asteroid Belt", fought, picked up Caldras x20 and Argenthyte x11, returned, turned the mission in ("Mission Completed" notice logged), took "Clear Pirate Base" and left; every step showed up in the log. That soak showed "Calculating..." between each status and a line per unloaded stack, which is why "Calculating..." is now skipped. Full harness: 30 active, 119 pass, 0 fail, the usual 5 skips.

## 2026-10-31: ECHO abilities on asteroids and wrecks (`EchoKit`)

Why: the EchoKit entry left asteroid- and wreck-targeted abilities out ("ECHO can't pick"), and every rule needed a fight, so nothing could fire while ECHO mines or salvages. The game has three such abilities in its skill trees: Crackshot Drone (asteroid), LB-RTR (wreck, `SalvageCargoLootboxBot`) and SF-CRK (wreck, `SalvageCreditDrone`).

Changed `src/IgiQoL/Features/EchoKit/`:
- `EchoKit.cs`: `KitWhen.Ready` ("Whenever ready", appended so saved rule names still parse) holds without a fight. `Fire` handles `Asteroid` (the flagship's `MiningModule.priorityTarget` while it still has ore) and `Salvage` (`WreckFor`: nearest active `SalvageContainer` whose `lootboxExtracted` or `creditsExtracted` is 0, by payload type, the same test as `AbilityButton.SelectTargetAndActivate`; other salvage payloads get no target, as on the HUD). `CanAutomate` now only excludes drone-targeted abilities. The self-check undocks first.
- `EchoKitSection.cs`: "Whenever ready" choice; help text names the asteroid and wreck targets.

Verified (`OnlyChecks = Contacts,EchoKit`: 11 pass): `rules` (Whenever ready holds out of a fight), `wreck-target` (LB-RTR prefab from the skill trees, a wreck spawned 4 units away: picked while its lootbox is there, passed over after `lootboxExtracted = 1`), and the earlier `ability`, `deploy`, `pick-up`. Also `Contacts/markers` passes when run alone now.

Not checked: firing Crackshot Drone, LB-RTR or SF-CRK from a ship that has them (the dev flagship has only Battle Stations! and Rapid Shield Recovery).

## 2026-10-31: asteroid ores in the Contacts tab (`Contacts`, idea list "local area scanner")

Why: finding the asteroid with the ore you want means hovering asteroids one by one; the Contacts tab listed boardables, wrecks and loot but no asteroids.

Changed `src/IgiQoL/Features/Contacts/Contacts.cs`: `ContactsList` collects the location's `Asteroid`s once a second (with the rest) and groups them by ore and layer: surface ore always, core ore when `Asteroid.GetTooltipScanLevel() > 0` (Basic Scanning), the amount column only at level 2 (Advanced Scanning), matching what the game's asteroid tooltip reveals. The "Asteroid ores (N asteroids)" section shows only where there are asteroids; rows are "Cerrax, surface | 2 asteroids", rarity-coloured, the ore's contents as tooltip, " | preferred" for ores listed in `OrePreference`, the nearest distance and a Mine button. `Contacts.Mine` is `Asteroid.OnMouseDown` without the mouse-over-UI check (loadout warning, `FleetCommands.IssueClickWork` for the click-work candidates, `MissionTrigger.TargetAsteroid`). Shared harness helpers moved to `SelfCheckContext`: `EnsureInSpace()` (undock when a run limited by `OnlyChecks` starts docked), `SpawnAsteroid(position, ore)`, `RemoveAsteroid(data)`; `OrePreferenceCheck` uses them. `Contacts/markers` now undocks first: it failed when run alone because markers hide while docked.

Verified: `Contacts/ores` (two Cerrax and one Amberic asteroid spawned, Amberic set as preferred: rows "Cerrax, surface | 2 asteroids" and "Amberic, surface | 1 asteroid | preferred"; screenshot `igiqol-window-contacts-ores.png`, rows and Mine buttons aligned with the other sections).

Not checked: the Mine button with a ship that has mining turrets (the dev fleet has none, so the game answers with its "no mining turret" warning).

## 2026-10-31: preferred ores (`OrePreference`, idea list "better ECHO AFK mechanics", plan #27)

Why: ECHO mines whatever asteroid is nearest (`IdleManager.FindClosestMineableAsteroid`) at the first mining location in the system that has any ore (`FindSuitableMiningPoi` → `IsSuitableForMining`), and your ships' mining modules take the nearest asteroid in their list (`AbstractTargetingModule.FindClosestFilteredTarget`). You can't send an AFK fleet after the ore a recipe needs.

Added `src/IgiQoL/Features/OrePreference/`:
- `OrePreference.cs`: per-save record `features.OrePreference` (`mode` Off / Prefer / Only, `ores` = ore item identifiers). Patches: prefix on `IdleManager.FindClosestMineableAsteroid` (nearest asteroid with a listed ore the module can mine; none here → null in Only, and in Prefer when another location in the system still has one, so ECHO leaves through its usual station stop); prefix/postfix on `FindMiningActivity` sets a `strict` flag (Only always; Prefer when the last mining location or another one still has a listed ore) that a prefix on `IsSuitableForMining` uses to accept only locations with a listed ore; prefix on `FindClosestFilteredTarget` for `MiningModule`s of your ships and drones (listed ore in the module's list first, then any listed-ore asteroid here it can mine, i.e. join one someone works; Only → none). It takes `__runOriginal` and stays out when an earlier prefix (DroneModes) already picked. Core ores count only with `SkilltreeNode.miningBasicScanning`, matching the asteroid tooltip. Scene asteroid list cached for 1 s, "another location has it" for 10 s.
- `OrePreferenceSection.cs`: ECHO tab section after ECHO abilities: mode dropdown, the listed ores in rarity colour with the ore's "contains" text as tooltip and an x, and an "Add an ore" dropdown of all 69 `OreItemData.regularOres` sorted by level (names are unique; shown as "Dense Cerrax (level 7)"). Adding an ore while the mode is Off switches it to Prefer.
- `OrePreferenceCheck.cs`; registered after `EchoKit`, before `Loadouts`. The check undocks when docked.

Verified (`[Dev] OnlyChecks = OrePreference,StationServices`: 17 pass, 0 fail, 0 skip): `ores` (69), `roundtrip`, `locations` (Red Gate has 2 mining locations; Only OreRare30: "Asteroid Field T-20" suitable, "Site T-74 Mining Site" not, though the game alone says yes; the game's own `FindSuitableMiningPoi` returns T-20; Prefer with an ore no location has doesn't restrict), `echo-pick` and `auto-pick` (two asteroids spawned next to the flagship through `AddPersistable`, near = Cerrax, 16 units away = Amberic, and a "Mining Drone Laser" swapped into the last drone slot for a real `MiningModule`: Off → near, Prefer Amberic → far, Only on an absent ore → none; the drone's own pick also joins the far asteroid when it isn't in its list, and Prefer on an absent ore falls back to near), `tab` (screenshot `igiqol-window-echo-ores.png`: rows aligned, ore names in rarity colour).

Not checked: a real ECHO mining session (the dev fleet has no mining turrets; the check drove the game's pick functions with a drone's mining module), core ores with the scanning skill.

## 2026-10-31: warp fuel restock (`StationServices` `FuelCells`)

Why: travel burns warp fuel from the flown ship's cargo only (`TravelManager.UseWarpFuel` reads `currentSpaceShip.cargo`), and vanilla ECHO keeps 2 cells only with the Prompt Engineering warp fuel skill (`IdleManager.TryRestockAtStation`). Shops stock 3 cells each (Plasma, or Ion above level 2).

Changed `src/IgiQoL/Features/StationServices/`:
- `Services.cs`: `RestockFuel` at the end of `Restock`: tops the flown ship up to `FuelCells` cells (a part-used cell counts as one, as in `Inventory.HasEnoughWarpFuel`), armory first, then the credit shops, both sorted by fuel multiplier (Hyper 3.3, Ion 3.0, Plasma 2.8) then price; a quote books the cargo space its picks would take. `ServiceReport.cells` counts the cells within `count`. Short reasons: "none for sale here", "the shops here have no more", credits, cargo space, level, reputation.
- `StationServices.cs`: config `FuelCells` (int 0-50, default 0 = off); `Summary` says "loaded 600 rounds and 4 warp fuel cells (...)".
- `AirlockServices.cs`: the restock button reads "Restock fuel" when only fuel is missing; its tooltip names the fuel target.
- `StationServicesCheck.cs`: `fuel` and `fuel-short`. Also fixed the `airlock` check: `GoToLocation(Airlock)` replaces the airlock and the old one is destroyed only at the end of the frame, so the check grabbed the old `AirlockServices` and hit a null button (NullReferenceException, harness hung). It now waits until `AirlockServices.Instance` sits on `SpaceStationInterior.instance.currentAirlock`. This only bit when the check ran without the earlier checks (OnlyChecks), not in play.

Verified: `fuel` (flagship and armory fuel stripped, 1 Plasma cell put in the armory, `FuelCells` 0 → no cells quoted; 4 → quote 4, run 4: 1 from the armory, 3 Ion cells bought for 15.6K cr, the best kind on sale; summary "loaded 4 warp fuel cells (15.6K cr)"), `fuel-short` (50 → "46 warp fuel cells short, none for sale here."), and the other StationServices checks.

## 2026-10-31: end of the sixth loop session

This session extended `Separation` to enemy and allied ships and measured it in spawned brawls (entry below), and added `[Dev] OnlyChecks`. Final harness run: 28 active, 104 pass, 0 fail, 5 skip (the same five as the fifth session: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`, `DroneModes/gatherer-assist`). `[Dev] Enabled = false`, `OnlyChecks` empty, other `[Dev]` values unchanged; the dev save and `/tmp/igiqol-launch.sh` are deleted. Your own saves were not written.

Harness note: `[Dev] OnlyChecks = Separation` (comma list of feature ids) runs just those checks, about 2 minutes instead of the full set. A check run alone still starts docked, so it has to undock itself if it needs space (`SpaceStationInterior.instance.ExitSpacestation()`, then wait for `HudManager.Instance.showHud`).

Looked at and dropped: "deposit everything but favourites" is already vanilla (`PlayerCargo.DepositFleetCargo`, the fleet-cargo auto-deposit toggle keeps favourites aboard). "Crew top-up for the fleet" needs a target mix per ship that the game doesn't have for fleet ships (ECHO's crew ratios only exist for the flagship and default to all deckhands); `Loadouts` already restores a saved crew mix and hires the difference.

Next candidates: ECHO abilities with asteroid or wreck targets; warp fuel restock in `StationServices` (ECHO keeps 2 cells with the Prompt Engineering skill, `IdleManager.TryRestockAtStation`); scanner and operation-menu clarity from the idea list; an in-play look at `EchoCargo` selling, the docked forge roll-up, `RepairOnDock` with ECHO, and mining/salvage drones in Ship's target with a ship that has mining or salvage turrets.

## 2026-10-31: enemy and allied ships keep distance too (`Separation` `OtherShips`), measured in big fights

Why: the idea list names ships ramming into each other in large battles, and `Separation` only moved your own ships. Physics: player ships stop colliding with friendly units after 2 s of contact (`AbstractUnit.OnCollisionStay2D` → `IgnoreCollision`), enemies never do, so stacks of enemies and allies grind into your fleet. There's no collision damage.

Changed `src/IgiQoL/Features/Separation/Separation.cs`:
- Config `OtherShips` (off): ships that aren't yours (enemies and allies, not wrecks, not carrier fighters) get the same push while they fight. `IsOn(SpaceShip)` picks the per-ship setting for your ships and `OtherShips` for the rest; the `ApplyThrust` prefix uses it instead of returning early for non-player ships.
- `ComputePush` now pushes only when the hull outlines (`surfaceCollider.Distance`) are closer than `Gap`, by the gap still missing; the bounding circles (half the hull length) only pre-filter. Eclipse (5.0 × 2.1 half-extents) and Hurricane (5.4 × 2.3) side by side 7.3 units apart have a 4-unit hull gap but overlapping circles; the first version pushed them anyway.
- Descriptions of the feature and `Gap` updated.

Measured with a throwaway probe (deleted): 12 hostile hulls of sizes 1-4 plus 4 allies spawned next to the fleet, 33 s rounds, hull overlaps sampled twice a second, plus the share of fighting ships' turrets with a target in range. Vanilla 1.03 overlapping pairs per sample and 79% in range (13 rounds); shipped version 0.52 and 73% (7 rounds); two other variants (push turned sideways to the target, and sideways at a fixed distance from it) did no better. Table in plan §7-9. So it about halves the stacking and costs a few points of weapon uptime; defaults stay off.

Added `[Dev] OnlyChecks` (`Core/DevFeature.cs`): runs only the listed features' self-checks.

Verified (harness 104 pass): `Separation/apart` (centres 7.3 apart, circles reach 12.4, hull gap 3.98 → no push), `other-ships` (a spawned Arbiter dropped onto the flagship: no state with `OtherShips` off, a push with it on, its game destination restored after `ApplyThrust`), and the earlier `settings-roundtrip`, `push` (now 3.7-3.9 units instead of 11.9, since only the outline gap counts), `live`. README `Separation` row and the `[Dev]` paragraph updated; plan overview row 9 and §7-9.

Not checked: how it feels in a real conquest battle; whether the weapon-uptime cost shows up as longer fights (the probe didn't log damage).

## 2026-10-31: end of the fifth loop session

This session finished the drone leftovers of wishlist item "better drone behavior settings" (entry below). Final harness run: 28 active, 102 pass, 0 fail, 5 skip (the usual four: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`, plus the new `DroneModes/gatherer-assist`, because neither dev ship has salvage or mining turrets). Plain launch to the main menu: `27 active, 1 disabled` (Dev), no `switched off`. `[Dev] Enabled = false`, other `[Dev]` values unchanged; the dev save and `/tmp/igiqol-launch.sh` are deleted. Your own saves were not written (mtimes unchanged since 2026-10-30 22:54).

Harness notes: the fleet kills the TargetPriority check's spawned enemies within a couple of minutes, and the longer DroneModes check made `DroneModes/guard` and `Separation/live` skip for lack of enemies. `SelfCheckContext.SpawnEnemies(count)` (new, the console's SpawnEnemy spawn, moved out of `CombatLog`) is now called by `CombatLog/dealt`, `DroneModes/guard` and `Separation/live` when they find none. The DroneModes check spawns a wreck (`SalvageData` through `MapPointOfInterest.AddPersistable`, removed again with `RemovePersistable`) and swaps the flagship's last drone slot to a salvage drone for `gatherer-guard`, then rebuilds the original drones.

The wishlist is now done apart from missile evasion (measured and dropped) and "text size and UI accessibility: rest needs specifics". Next candidates: ECHO abilities with asteroid or wreck targets; more one-click station actions (warp fuel restock, crew top-up for the fleet, deposit everything but favourites); scanner and operation-menu clarity from the idea list; an in-play look at `Separation` in a large fight, `EchoCargo` selling, the docked forge roll-up, `RepairOnDock` with ECHO, and mining/salvage drones in Ship's target with a ship that has mining or salvage turrets.

## 2026-10-31: drones stay with another ship; mining, salvage and repair versions of the drone modes (`DroneModes`, plan #11 leftovers)

Why: plan #11 left "guard a chosen fleet ship", "assist mining or salvage on the ship's target" and "recall below N% drone hull" open. Before this, Guard and Ship's target only changed combat and painter drones; mining, salvage and repair drones only got the follow distance.

Changed:
- `Core/ShipSettings.cs`: `droneAnchor` (guid of another of your ships; null = own ship), saved as `droneAnchor` next to `droneMode` (dropped when the mode is Game). Loadouts carry it through `ShipSettings.Snapshot`.
- `Features/DroneModes/DroneModes.cs`: `TryGet` also returns the anchor (`FindShip`: the flagship or an `activeFleet` ship with that guid that is here, not destroyed or travelling; otherwise the drones' own ship). `AnchorTarget` reads the anchor's module of the drone's kind (combat for combat and painter drones, mining, salvage), `CanWork` asks the drone's module (`IsTargetMineable`, which includes the driller attach limit; `IsTargetSalvagable`; `IsValidTarget`, which for repair drones means missing HP of the kind they repair). `ApplyBehaviorMode` prefix: follow distance and Guard drop measured from the anchor; per-frame retargeting stays limited to combat and painter drones. `FindClosestFilteredTarget` prefix now covers every drone module: Guard takes the nearest target within the radius of the anchor (mining and salvage drones join the anchor's job when nothing free is in reach, since the game's lists leave out targets someone already works on); Ship's target for mining and salvage takes the anchor's asteroid or wreck when the drone can work it, else Guard's pick; repair drones take the anchor first whenever it is damaged (Guard and Ship's target), else Guard's pick. Mining, salvage and repair drones only change at their next pick, so drillers and grinders are never pulled off a job.
- `UI/ShipPopup.cs`: "Drones stay with" row under "Drones" (This ship + the flagship and fleet ships; a removed ship shows as "(a ship not in your fleet)"), visible when a mode is set; the "Drones" tooltip describes the per-kind behaviour.
- `Features/FleetStrip/FleetStrip.cs`: popup check counts 2 dropdowns for DroneModes.
- `Core/SelfCheckContext.cs`: `SpawnEnemies` (see harness notes); `CombatLog` and `Separation` checks use it.
- README DroneModes row; plan overview row 11 and §10-11 status.

Dropped: recall below N% drone hull. A docked drone keeps its damage (the bay has no repair; `HandleDroneReturn` only hides it), a destroyed drone is rebuilt at full hull for free after `rebuildTime`, and a docked drone is sent out again at once while the bay wants drones out (`HandlePostUnloadBehavior`). A recall would swap a short rebuild for a damaged drone that waits in the bay.

Verified (harness 102 pass, 0 fail, 5 skip): `popup-anchor` (options "This ship, Hurricane"; screenshot `igiqol-ship-popup-drones.png` shows the row under "Drones" without overlap), `anchor-follow` (flagship drone with anchor Hurricane, 12 units from Hurricane, radius 4: destination on Hurricane; `droneAnchor` in the record), `anchor-fallback` (unknown guid → own ship), `repair-anchor` (Eclipse's Shield Repair drone guarding Hurricane at 50% hull and shield picks Hurricane in Guard and Ship's target with an empty list), `gatherer-guard` (salvage drone in the swapped slot, spawned wreck 6.3 units away: radius 5.3 → none, 7.3 → the wreck), and the earlier `follow`, `guard`, `ship-target`, `hud-toggle`, `settings-roundtrip` checks.

Not checked: `gatherer-assist` (Ship's target joining the anchor's asteroid or wreck) needs a ship with salvage or mining turrets, which the dev fleet doesn't have; the code path is the same `AnchorTarget` + `CanWork` pair the repair and combat checks run. Nothing seen in play yet.

## 2026-10-31: end of the fourth loop session

This session added refinery jobs to `CraftRollup` (entry below). Final harness run: 28 active, 97 pass, 0 fail, 4 skip (the same four: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`). Plain launch to the main menu: `27 active, 1 disabled` (Dev), no `switched off`. `[Dev] Enabled = false`, other `[Dev]` values unchanged; the dev save and `/tmp/igiqol-launch.sh` are deleted.

Next candidates: drone leftovers (guard another fleet ship, assist mining or salvage, recall below N% hull); ECHO abilities with asteroid or wreck targets; more one-click station actions (warp fuel restock, crew top-up for the fleet, deposit everything but favourites); scanner and operation-menu clarity from the idea list; an in-play look at `Separation` in a large fight, `EchoCargo` selling, the docked forge roll-up (now also its refinery canisters with "deposit in cargo" on) and `RepairOnDock` with ECHO.

## 2026-10-31: refinery jobs in the crafting roll-up (`CraftRollup`, plan #18 leftover)

Why: weapon and module recipes need hundreds of canisters, and the roll-up only listed them under "Bring:" even when the station's materials storage held ore that refines into them.

Changed `src/IgiQoL/Features/CraftRollup/`:
- `Rollups.cs`: `RollupRefine` (ore, amount, started, job) and `RollupPlan.refines`, `refineSeconds`, `RefinesStarted`, `HasWork`. `Builder.PlanRefining` runs after the forge tree: for each missing canister it takes the reachable ore with the best yield of that material (`Refinery.GetAvailableItems(false, true)`, so no favourites and no ore your missions need; `disableAutoRefine` ore skipped), `ceil((short - carried) / (yield × yield multiplier))` units capped at what's there, and settles all shortfalls at the end so by-products and `pendingYield` count. Fees and refinery time go into the plan totals. `Advance` starts refine steps first (`Refinery.StartJob(ore, n, excludeFavourited: true)`) once ore, fee, output room and a refinery slot are there; new blockers "N <ore>", "room in the materials storage", "a free refinery slot". `ShortBlocker` treats a canister as on its way while a refine step yielding it is unstarted or still in `refinery.jobs`. `Status` reads "N of M forge jobs queued, K of L refinery jobs started". Save gains `refines` per plan; Load drops only a refine step whose ore no longer exists; `Relink` pairs refine steps with refinery jobs by ore and initial amount.
- `RollupPanel.cs`: summary "Roll-up: 3 parts, 00:18:13 forge time, 1 ore to refine, 00:14:01 refinery time, 46.5K cr. Bring: 303 Carbon"; the tooltip lists each ore with the materials it yields, then the forge jobs. The button also works when only refining is needed (`HasWork`).
- `CraftRollup.cs`: description; prefix/postfix on `RefineryJob.CompleteOneItem` plus prefix on `Refinery.DepositOutputItem` send a roll-up refine job's canisters to materials storage (while it has room) even with "deposit in cargo" on, as forge parts already were. Self-check gained a `Snapshot` helper (storage counts of canisters, every ore and the plan's products, credits, forge and refinery jobs, `pendingYield`, `autoRefine`) that both run checks restore from; `CheckRun` now also progresses the refinery.

Verified (harness 97 pass, 0 fail, 4 skip; Pillar of Progress, Tractor Beam [Small]): `refine-plan` (all ore and canisters removed: no refine step and 387 Titanium missing; 6 OreUncommon112 added (176.3 Titanium per unit): 3 units planned as computed, Titanium no longer missing, and its Tungsten by-product dropped Tungsten from the list too), `panel-refine` and `panel-refine-running` (screenshots `igiqol-forge-rollup-panel-refine.png`, `...-running.png`, four lines fit the strip next to the button), `refine-save` (refine job started at once and relinked after `Load`), `refine-run` (refinery + forge progressed 12 × 120 s: plan finished, target queued). The earlier roll-up checks still pass.

Not checked in play: the docked case with "deposit in cargo" on (canister routing), and a station whose refinery is full of your own jobs (the roll-up should wait with "a free refinery slot").

## 2026-10-31: end of the third loop session

This session added `StationServices` (entry below). Final harness run: 28 active, 92 pass, 0 fail, 4 skip (the same four: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`). Plain launch to the main menu: `27 active, 1 disabled` (Dev), no `switched off`. `[Dev] Enabled = false`, other `[Dev]` values unchanged; the dev save and `/tmp/igiqol-launch.sh` are deleted.

Harness notes: `StationServices` is last in the registry and expects to be docked (it docks itself if not). If you kill a harness run, its cleanups don't run; the next run's checks were made tolerant of that where it mattered (Notes). Don't chain the launch after `grep ... Build succeeded` with `&&`: grep succeeds on an error line too, so check for `error` separately.

Next candidates: crafting roll-up queuing refinery jobs for missing canisters; drone leftovers (guard another fleet ship, assist mining or salvage, recall below N% hull); ECHO abilities with asteroid or wreck targets; more one-click station actions (warp fuel restock, crew top-up for the fleet, deposit everything but favourites); an in-play look at `Separation` in a large fight, `EchoCargo` selling, the docked forge roll-up and `RepairOnDock` with ECHO.

## 2026-10-31: station services (`StationServices`, idea list "fewer clicks", plan #26)

Why: after a trip you visit the hangar per ship to pay for repairs, the shop per ammo type, and the shop again to sell, and ECHO never pays for repairs: `IdleManager.HandleAtSpaceStation` sits on "@IdleWaitRepairs" until the free auto-repair (1 s per tick, 200 ticks) is done. Paid repair (`Source.Spacestation.PersonalHangar.StartRepair`, 0.1 s per tick) takes about 20 s. Vanilla ECHO restocks only the flagship's ammo (`TryRestockAmmo`), and nothing applies the Item rules' Sell actions when you dock yourself.

Added `src/IgiQoL/Features/StationServices/`:
- `Services.cs`: three services with a quote mode (no side effects) and a run mode, both returning a `ServiceReport` (lines, problems, credits, count). Repair: every damaged ship of flagship + `activeFleet` (work-bay ships skipped, ships with a paid job skipped), amount and price as the hangar UI computes them (`max(hull, armour damage)`, reputation `GetRepairCostDiscount`), started with `StartRepair(..., autoRepair: false, cost)`; needs the PersonalHangar facility. Restock: each ship's `GetAmmoTypesRequired()` (ECHO's ammo-minutes target, capped to cargo share and free space), armory first (`Cargo.Move`), then the station's credit shops in `SpaceStation.shopInventory` order (stacks with a `costItem` are skipped), bought through `InventoryInteractionManager.BuyAmount(stack, n, null, ship.cargo, false)` so the game's rep, level, credit and `OnPurchase` handling apply; amounts are capped to credits and cargo space first. Sell: per stack, `CargoRules.Plan` (EchoCargo) with the Item rules dock action; deposits go to `Cargo.DepositTarget`, sales mirror `InventoryInteractionManager.SellAmount` without its shop screen (credits, item into `ss.shopInventory` with buyback, out of cargo). Protected: favourites, Usable items with no storage override, ammo the owning ship uses.
- `StationServices.cs`: feature, config `[StationServices]` `RepairOnDock`, `RestockOnDock`, `SellOnDock` (off), `AirlockButtons` (on); postfix on `PlayerCargo.AutoDepositOnDock` (called right after `EnterSpacestation` for you and ECHO, not on loading a save inside a station) starts a coroutine that waits for `SpaceStationInterior.instance`, then runs sell → repair → restock and shows one toast ("Docked: repairing 1 ship (57.2K cr)") plus the first problem of each. With ECHO on and `EchoCargo` active, on-dock selling is left to ECHO.
- `AirlockServices.cs`: postfix on `Airlock.Start` clones `homeStationButton` three times into the airlock's own button row (`PlaceBelowCharacters`, laid out by `LayoutButtonRow`), kept at the end of `buttonRow` so vanilla's situational buttons come first. Refreshed once a second from the quotes: hidden with no work, greyed via `Airlock.SetButtonState` when work exists but can't be done (reason in the tooltip), label with the total ("Repair fleet 94.3K cr", "Sell by rules +65 cr"), tooltip with every line.
- `StationServicesCheck.cs`; registered last (after `Loadouts`, whose check leaves you docked; it docks itself otherwise).
- `EchoCargo` help text mentions the new on-dock and button selling.

Fixed on the way:
- `Rules/ItemRules.Resolve` threw `ArgumentNullException` when any per-item rule existed and a stack's type has no `identifier` (seen: a Usable "Plasma Fuel Cell" in Hurricane's cargo). Per-item lookup is now skipped for such types. `TractorCaps` and `EchoCargo` call `Resolve` too, so they could have hit it as well.
- `Notes/file-roundtrip` failed after a harness run was killed before its cleanup: the left-over "selfcheck" note tab was found first. The check now removes old "selfcheck" tabs before adding its own.

Verified (harness 92 pass, 0 fail, the usual 4 skips; 28 active with Dev on): `repair` (flagship at 60% hull: 127,233 damage quoted and charged 76,340 cr, paid job with 0.60 cr/unit), `repair-once` (not quoted again while the paid job runs), `repair-progress` (hull 190,849 → 242,448 in 3 s), `restock` (prefers a fleet ship: Hurricane's Havoc Missiles, 600 needed, 10 from the armory, 590 bought for 2,950 cr, quote equal to the run; fleet ships have their `SpaceShip` unit while docked; an earlier run with the flagship did the same for 596 Plasma Cells), `sell` (7 Empty Cells, +448 cr, shop 0 → 7), `sell-above` (4 stored, 5 sold), `sell-favourite` (favourite stack kept), `on-dock` (`AutoDepositOnDock` with `RepairOnDock` on started a paid repair and toasted "Docked: repairing 1 ship (57.2K cr)"), `airlock` (buttons in the row right of Trade Routes, clear of Undock; the Sell click sold and hid itself; screenshot `igiqol-airlock-services.png`).

Not checked in play: a real dock with ECHO and `RepairOnDock` (whether ECHO leaves as soon as the paid repair finishes), a station with many vanilla airlock buttons (Buy system, Release, Overview, Build outpost) plus all three of ours, where the row could reach the Undock button.

## 2026-10-31: end of the second loop session

This session added `CraftRollup`, `Loadouts`, contact edge markers (in `Contacts`), `CombatLog`, `Guide` and `EchoKit`, plus the `[Dev]` dumps `DumpRecipes` and `DumpMissiles`; missile evasion was measured and dropped. The wishlist is done except that item. Final harness run: 27 active, 83 pass, 0 fail, 4 skip (the same four as before: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`). Plain launch to the main menu: `26 active, 1 disabled` (Dev), no `switched off`. `[Dev] Enabled = false` with the session's other `[Dev]` values back as they were; the dev save and `/tmp/igiqol-launch.sh` are deleted.

Harness notes for the next session: the dev save loads docked; `Window` undocks it, `Loadouts` (last in the registry) docks again through `SpacestationExteriorManager.EnterSpacestation` and leaves in its cleanup, so checks that need space must sit before `Loadouts`. The TargetPriority check's spawned enemies keep fighting the fleet for the rest of the run (useful for `CombatLog/live`). The mod window now has 9 tabs and is 1014 px wide at 100% text.

Next candidates (idea list and leftovers): crafting roll-up queuing refinery jobs for missing canisters; drone leftovers (guard another fleet ship, assist mining or salvage on the ship's target, recall drones below N% hull); ECHO abilities with asteroid or wreck targets while mining or salvaging; fewer clicks for common station actions (repair, restock, sell) from the mod window; an in-play look at `Separation` in a large fight, `EchoCargo` selling and the docked forge roll-up.

## 2026-10-31: ECHO abilities and deployables (`EchoKit`, idea list, plan #25)

Neither ECHO nor any NPC activates abilities or usables in vanilla (`IdleManager`, `Source.SpaceShip.Auto` have no calls); the HUD path is `AbilityButton.ActivateAbility` → `ActivatedAbility.TriggerPayload`, and cargo use is `UsableItem.OnUse` then removing one from the stack (`InventoryInteractionManager.UseItem`/`ConsumeItem`). Deployables are `DefensiveTurretItem` subclasses (`ShieldPylonItem`, `TauntPylonItem`, `ExplosiveMineItem`, plain turrets); retrieval is `DefensiveTurret.OnMouseUpAsButton` (a `TractorableItem` added to the turret and pulled in).

Added `src/IgiQoL/Features/EchoKit/`:
- `EchoKit.cs`: rules per ability identifier and per deployable item identifier (`KitWhen` Off / InCombat / ShieldBelow / ArmourBelow / HullBelow / TargetedBy, plus a value), saved under `features.EchoKit`. A 0.5 s game-time coroutine, gated on ECHO on (or `AlsoWithoutEcho`), in space, not travelling. Abilities: `isReady`, then `TriggerPayload()` or `TriggerPayload(target)` (ECHO's `CombatModule.priorityTarget`, or the fleet ship with the lowest hull fraction); toggles are tracked so the mod only switches off what it switched on. Deployables: first matching stack in flagship cargo, skipped while `DefensiveTurretItem.IsOnCooldown`, then `OnUse` and remove one; 2 s between drops. After 5 quiet seconds, turrets it dropped are tractored back with the flagship's `TractorModule.SetManualTarget`, the same way the game's click does.
- `EchoKitSection.cs`: the rule rows (ability name, toggle mark, cooldown, dropdown, threshold) appended to the ECHO tab by `EchoPriority.BuildEcho`; abilities with asteroid, wreck or drone targets are listed as not automatable.
- Config `[EchoKit]`: `AlsoWithoutEcho` (false), `PickUpTurrets` (true).

Verified (harness 83 pass, 0 fail, 4 skip): `roundtrip`; `rules` (threshold and fight gating); `ability` (the flagship's "Battle Stations!" fired, 120 s cooldown started); `deploy` (a Shield Pylon added to cargo went out: cargo 1 → 0, a `DefensiveTurret` with that item in space); `pick-up` (tractored back within 20 s, cargo back to 1). Not checked: hostile- and friendly-targeted abilities (the dev flagship has none), decoys and mines in a real fight.

## 2026-10-31: combat guide (`Guide`, idea list "clearer help text", plan #24)

Added `src/IgiQoL/Features/Guide/Guide.cs`: a "Guide" tab (order 85) built from game data and code: the hit pipeline as `DamageData.CalculateDamage`, `AbstractUnit.DamageResistMultiplier` (cap: at most 80% removed), `DamagePrevention`, `ShieldGeneratorModule.TakeDamage` and `ArmorModule.ApplyArmorResistance` do it; each damage type with the turret types that deal it (`AbstractCombatTurret.damageType` over `InventoryItemType.all` and every `EquipmentBuilder` prefab; torpedo bays added by hand as Heat from `TorpedoBayModule`); armour types with resist and weakness; the flown ship's tonnage (mass = tonnage × 10), acceleration, reachable top speed, sidestep, crit chance (3% + Precision part, `GetPrecisionCrit`) and per-type damage reduction; then the game's own `@EquipStat…Description` texts where they exist.

Finding: this build has one armour type, Armor Plating, 15% resist, weak to Corrosion (+25%), so Corrosion is the only type with an armour edge; shields treat all types alike. 23 weapon types across 7 damage types.

Verified (harness 78 pass, 0 fail, 4 skip): `Guide/data` (23 weapon types, 1 armour type, 7/7 damage types dealt), `Guide/tab`; screenshot `igiqol-window-guide.png` read back: lines wrap inside the window, damage types coloured with the game's own colours.

## 2026-10-31: combat log (`CombatLog`, idea list "battle parser", plan #23)

The wishlist is done apart from missile evasion (measured, not buildable as a dodge), so work moved to the loop-mode idea list.

Added `src/IgiQoL/Features/CombatLog/`:
- `CombatLog.cs`: prefix + postfix on `AbstractUnit.TakeDamage` (covers ships and drones; `SpaceShip.TakeDamage` calls the base). The prefix skips the early-return cases (disabled, destroyed, boardable) and records `absorbedByShield`, `absorbedByArmor` and hull HP; the postfix takes the differences, so one hit counts what it actually removed (overkill and splash re-use of a `DamageData` don't inflate it). Only hits where exactly one side is `Faction.player` count. Each hit goes into the current fight and the session under 8 views (dealt by weapon / damage type / your ship / enemy type, taken by enemy weapon type / damage type / enemy type / your ship). Rows are keyed by references (fitted item, drone definition, `unitDefinition`, `SpaceShipData`, pre-boxed `DamageType`) and get their label once, so a hit costs a few dictionary lookups. Kills: a hit that leaves the target destroyed, boardable or at 0 hull. Fights end after `FightGapSeconds` quiet; 10 are kept; `Duration` counts only seconds within fights. `WriteFile` appends finished fights (checked every 5 s) to `combat-log.txt`.
- `CombatLogTab.cs`: "Combat" tab (order 7): its two settings, scope and view dropdowns, Clear, a summary line and the top 25 rows, rebuilt once a second when something new came in.

Verified (harness 76 pass, 0 fail, 4 skip): `live` (the TargetPriority check's spawned enemies fight the fleet: 12,600 dealt over 2 weapon rows, 1 kill); `dealt` (a Plasma Cannon hit of 508.5 on a spawned enemy, row grew by exactly what its shield + armour + hull lost); `taken` (an enemy Laser hit on the flagship, 505.0 both ways); `tab` (4 rows, screenshot `igiqol-window-combat.png`); `file-text`. The check spawns one enemy itself when none is left, and runs before `Loadouts` (registry order) because that check docks.

## 2026-10-31: missile evasion measured and dropped (plan #8)

Added `[Dev] DumpMissiles` (`Core/DevFeature.cs`): missile flight values per launcher and thrust, mass and side acceleration of your fleet, to `dumps/missiles.txt`. Result (numbers in plan §7-9): missiles turn at 80°/s at 6-8 units/s (turn radius 4-6 units, Hailfire 10.7), ships sidestep at 0.3 units/s², about 1.6 units in 3 s. A sidestep can't make a homing missile miss, so no evasion feature was built; the wishlist item stays open only if you want ships to flee missiles, which changes combat more than a QoL pack should. No feature code changed.

## 2026-10-31: contact edge markers (`Contacts`, plan #2)

Added `src/IgiQoL/Features/Contacts/ContactMarkers.cs`: a full-screen layer under the HUD canvas (first sibling, so it draws below panels and windows) with 8 marker slots per kind (boardable ships, wrecks with something left, loot stacks). Twice a second it picks the nearest contacts of each enabled kind and sets the distance text; every frame it hides markers whose target is on screen and pins the rest to the edge with the game's `EdgeIndicator.IntersectViewportEdge`, pivot on the same point so the icon stays on screen. Hidden while docked or when the HUD is hidden. Icons `Fleet.Raid`, `Salvage`, `Fleet.Tractor`; loot is tinted by rarity. Clicking calls the Contacts tab's Board / Salvage actions or `TractorModule.SetManualTarget` on that stack.

Changed `Contacts.cs`: config `MarkBoardable`, `MarkWrecks`, `MarkLoot` (default on); `SettingsTab => "Contacts"`, so the counter and marker switches moved from the Settings tab to the top of the Contacts tab; the marker layer is created on save load next to the counter. README gained a Contacts row (it had none).

Verified (harness 71 pass, 0 fail, 4 skip): `Contacts/markers` spawns ore 400 units above the ship; after 1 s its marker is shown, anchored at (0.50, 1.00); screenshot `igiqol-contact-markers.png` shows the tractor icon with "400" at the top centre. `Window/window-open` still counts 31 settings rows.

## 2026-10-31: ship loadouts (`Loadouts`, wishlist "Loadouts", plan #14)

Added (`src/IgiQoL/Features/Loadouts/`):
- `Loadout.cs`: the saved fit. Weapons, modules (by `EquipmentSlot`) and boosters as the game's item JSON (`InventoryItemType.ToJson`), drone identifiers, officer guids, crew per type, skill preset name, the ship's QoL settings (`ShipSettings.Snapshot`, which holds the weapon rules) and optionally ECHO (`autopilotSettings.ToJson()` plus the `EchoPriority` list). Stored in the ship's mod record as `loadouts` plus `lastLoadout`.
- `LoadoutApply.cs`: data-level apply, gated on `SpaceStationInterior.instance` with the PersonalHangar facility and the hangar's own "assigned elsewhere" flags. Order: skill preset (exact name, point total checked against `GetSkillPoints`; the flown ship invests via `SetSelectedLoadout(index)`, others only get `skillLoadout`), equipment, officers, crew, drones, QoL settings, ECHO. Equipment pool = fitted pieces + armory + ship cargo; pass 1 matches by `Identity` (type + rarity + rolled stats as float), pass 2 takes the same type and rarity (keeps the fitted one if it qualifies, else highest level) and flags it; the hangar's size rules decide fit; replaced pieces go to the armory; a missing piece leaves the fitted one in place unless it moved to another slot. Crew: surplus to the reserve up to `maxReserve`, shortfall from the reserve, then `PersonnelCenter.TryPurchaseCrew` + `RemoveCreditsForCrew`. Drones: slot count from `DroneBayModule.GetDroneAmount(ship)`, locked drones dropped and reported. Then `RecalculateLevel`, `RebuildDerivedStatBoosts`, `GameplayManager.RefreshShipInSpace`, crew/officer notifications, hangar `RefreshIfOpen`, inventory `ReloadUI`.
- `LoadoutsTab.cs`: mod window tab "Loadouts" (order 25): ship dropdown (flown, fleet, others the hangar lists), dock status, save current fit (name + "Include ECHO settings"), per loadout Apply / Update / Rename / Delete (game `AlertPopup` confirmations), same-hull loadouts of your other ships with Apply / Copy here, and the last apply's report.
- `Loadouts.cs`: feature, `LoadoutStore`, an "L" button cloned from the hangar's "S" button at x = -196 (hidden while the decal "-" button is shown there) via postfix on `PersonalHangar.Start`, and a postfix on `GamePlayer.SetSpaceShipData` that restores the ECHO settings of the flown ship's last-applied loadout (only after a save is up).
- `Core/ShipSettings.cs`: `Save` split into `Write`, plus `Snapshot(guid)` / `Restore(guid, record)`.
- `UI/ModWindow.cs`: window width is now `max(760, 150 + 96 × tabs)`, so the seventh tab fits.

Verified (harness 68 pass, 0 fail, 4 skip; the check docks again with `SpacestationExteriorManager.EnterSpacestation` because the Window check undocks, and leaves afterwards): `roundtrip`; `apply-same` (nothing changes); `refit` (weapon and booster taken off and 5 gunners sent to the reserve, all restored, armory count back); `hire` (3 gunners removed outright: 3 hired at the Conquest station, stock 37 → 34, 21,000 cr); `swap` (weapons 1 and 2 swapped and back, armory untouched); `missing` (unknown weapon type reported, fitted one kept); `substitute` (changed roll: same type fitted and flagged); `echo-on-switch`; `tab`; `hangar-button`. Screenshots `igiqol-window-loadouts.png`, `igiqol-hangar-loadouts.png`. Two bugs found on the way and fixed: full item JSON can't identify a piece (a resonant booster's unlock progress changes while fitted), and stats parsed back from JSON differ from the live double unless compared as float.

Not checked in play: applying to a fleet ship other than the flagship, officers moving between ships (the dev flagship's 5 officers stayed put in every check), the skill preset switch (the checks kept the same preset).

## 2026-10-31: crafting roll-up (`CraftRollup`, wishlist "Crafting roll-up", plan #18)

Phase 0 first: `[Dev] DumpRecipes` (new, `Core/DevFeature.cs`) writes `dumps/recipes.txt`. Findings are in plan §18: forge-made ingredients are all `RefinedProduct` templates stored in materials storage; weapon/module recipes need hundreds of 3-4 refined products, which need only canisters.

Added:
- `src/IgiQoL/Features/CraftRollup/Rollups.cs`: planner and runner. `Build(forge, recipe, n)` walks item ingredients recursively (depth 6, cycle guard), takes stock net of what earlier branches claimed (`CountForgeItems` for items, `CountLocalItems` for canisters, the same counts `CraftingRecipe.ConsumeIngredientsForCrafting` uses), crafts `ceil(short / per craft)` of the recipe at this forge that makes the item, merges repeated recipes into one step, and lists what nothing makes as missing. `Advance` (every 1 s per plan from a postfix on `Forge.ProgressJobs`, which runs per frame where you are and every 10 s elsewhere) starts each unstarted step whose ingredients, fee and a forge slot are there via `Forge.StartJob`; the target is the last step, and the plan ends when it starts. Status text: the first short ingredient no queued or pending step makes, else "credits", else "a free forge slot". Plans saved under `features.CraftRollup.plans` (station guid, target id, steps with started flags); on load, started steps are relinked to queued jobs by recipe.
- `RollupPanel.cs`: inside `ForgeTabContents.notCraftableContent` (postfix on `SetSelectedRecipe`), replacing the vanilla "insufficient materials" label: summary text (reaches 30 px above the 70 px strip, left of the deposit-in-cargo toggle), an amount field (1-999) and a copy of the game's Craft button ("Craft missing parts" / "Stop roll-up"). Tooltip on text and button lists every job, total time, fees and what to bring. Refreshes once per second.
- Prefix on `ForgeJob.AddForgeItemToCargo`: output of a roll-up part job goes to the station's materials storage instead of docked cargo.
- Registered in `FeatureRegistry` (23 active with Dev on). README table row; plan overview row 18 and §18 notes.

Verified (harness 58 pass, 0 fail, 4 skip; the dev save sits at a Conquest station without a forge, so the check uses the first station with a forge, Pillar of Progress, remotely): `plan` (Tractor Beam [Small]: 168 Titanium Plate, 101 Tungsten Carbide, 101 Graphite Wafers, each equal to the shortfall); `panel` and `panel-running` (forge popup via `ForgePopup.Open`, screenshots `igiqol-forge-rollup.png`, `igiqol-forge-rollup-running.png`); `save` (record written, `Load` restores 4 steps with 2 started and jobs linked); `run` (missing canisters added, 18 × `ProgressJobs(30 s)` later the target job is queued and the plan gone); `parts-to-storage` (part job marked, target job not); `stalled-part` (cancelling a part job makes the status name that part). The check restores storage, credits and the forge queue afterwards.

Not checked in play: the docked case (output routing with "deposit in cargo" on, the strip next to the deposit toggle). Not done: queuing refinery jobs for missing canisters.

## 2026-10-31: end of the first loop session

Final harness run: 22 active, 51 pass, 0 fail, 4 skip (the four long-standing skips: `EchoGates/dungeon-min`, `EngagementDistance/live`, `EchoPriority/dropdown-to-list`, `EchoCargo/live-keep`, all for lack of a dungeon, target, Autopilot tab or dock in the dev save). Plain launch to the main menu: `21 active, 1 disabled` (Dev), no `switched off`. `[Dev] Enabled = false`, `[Separation] Default = false`; the dev save and `/tmp/igiqol-launch.sh` are deleted.

Next candidates, in wishlist order: missile evasion (#8, needs missile speed/turn measurements first), loadouts (#14), crafting roll-up (#18, needs the recipe dump), contact edge markers (#2), then the drone leftovers above. After the wishlist: the ideas list in the user's loop-mode request (battle parser, ECHO abilities and deployables, damage-type and ship-stat help text, scanner improvements, fewer clicks for common actions).

## 2026-10-30: keep distance in combat (`Separation`, wishlist "better AI ship behavior", plan #9)

Why: in combat `ShipNavigator` switches itself off (`ResolveNavigationMode` returns Direct) and `AbstractUnit.UpdateMovementForTarget` flies straight at a ring around the target, so ships stack on each other. Fleet ships don't even collide with each other.

Added:
- `src/IgiQoL/Features/Separation/Separation.cs`. Prefix/postfix on `AbstractUnit.ApplyThrust` (the per-physics-step steering toward `currentDestination`): for your ships with a target, no override destination and no hold position, the destination is shifted by a push for that call only and the game's own destination is restored afterwards. The push is the sum over `ShipNavigator.navigators` of "away" vectors from ships closer than both hull radii + `Gap`, each as long as the overlap, capped at one hull diameter + gap, recomputed every 0.25 s. A first version wrote the push into `currentDestination` after `UpdateTarget`; `ApplyThrust` clamps the destination to the POI border, which made that version's remembered "unshifted" destination absorb the push, so it was replaced.
- Config `[Separation]`: `Default` (false), `Gap` (2). `ShipSettings.separation` (bool?) per ship; popup row "Keep distance from other ships".
- Carrier fighters are neither pushed nor pushed away from.
- FleetStrip popup check counts the new dropdown.

Verified (harness 51 pass, 0 fail, 4 skip): `push` (Hurricane placed 0.5 units east of the flagship gets a push of (11.9, 0), straight east); `live` (after 5 physics steps with a target, the game steered Hurricane at its destination + push and its stored destination was untouched). A throwaway 20 s A/B brawl (8 size-3 hulls spawned, separation off vs on, code removed afterwards) said nothing useful: Hurricane is a railgun ship with a 30-unit approach distance and moved under 2 units in 20 s either way, sitting next to the flagship at the station. How it plays in a large fight with faster ships is unconfirmed.

## 2026-10-30: drone modes (`DroneModes`, wishlist "better drone behavior settings", plan #11)

Added:
- `src/IgiQoL/Features/DroneModes/DroneModes.cs`. Per-ship `ShipSettings.droneMode` (Game / Follow / Guard / ShipTarget) and `droneRadius` (2-40, default 10), saved as `droneMode`/`droneRadius` in the ship record. Nothing touches the game's own `DroneBayModule.behaviorMode`, which vanilla resets to FollowShip on load if it sees an unknown name.
- Prefix on `Drone.ApplyBehaviorMode` (every frame per deployed drone): Follow replaces the fixed 10-unit leash with the ship's radius; Guard drops an auto target whose hull edge is past 1.25 × radius from the ship; Ship's target keeps the drone's auto target equal to the commander's `priorityTarget`. Your own target clicks (the drone's manual target, which vanilla copies from the ship) are left alone.
- Prefix on `AbstractTargetingModule.FindClosestFilteredTarget` for drone modules: Guard picks the nearest enemy inside the radius, Ship's target picks only the ship's target.
- Guard and Ship's target act on combat and painter drones (`DroneModes.FightsEnemies`); repair, mining and salvage drones only use the follow radius.
- HUD: prefix on `DroneControls.ToggleBehavior` cycles Free roam → Follow ship → Follow → Guard → Ship's target → Free roam for the flagship; postfix on `DroneControls.UpdateDisplay` shows "Follow 10u", "Guard 10u" or "Ship's target" on the button.
- Per-ship popup: "Drones" dropdown plus distance field. FleetStrip popup check counts it.

Verified (harness 48 pass, 0 fail, 4 skip): settings round-trip; `hud-toggle` (four clicks gave Follow 12u → Guard 12u → Ship's target → Free Roam); `follow` (a drone 12 units out gets a destination at the ship with radius 4 and none with radius 20); `guard` (radius just under the nearest enemy's hull edge picks nothing, just over picks an enemy inside it); `ship-target` (pick and per-frame correction both moved to the ship's target). The dev save's flagship only carries painter and shield-repair drones, so Guard and Ship's target were checked on the painter drone; no combat drone was available.

Not done: guard a different fleet ship, assist mining/salvage on the ship's target, recall below N% drone hull. Drones get no repairs in the bay (`DroneBayModule` only has a rebuild timer for destroyed drones), and a docked drone is redeployed at once while the bay wants drones out (`HandlePostUnloadBehavior`), so a damage recall would need its own hold-in-bay logic.

## 2026-10-30: per-weapon target rules (`TargetPriority`, plan #4)

Added, on the same ranking engine as the ship rule below:
- `ShipSettings.weaponRules`: rule per hardpoint index (`SpaceShipHardpoint.index`), with the weapon identifier it was set for; saved as a `weaponRules` array in the ship record. The popup marks a row "(refitted)" when the hardpoint now holds a different weapon; the rule still applies.
- `TargetPriorityPatches.UpdateWeaponPicks` (runs in the same `CombatModule.UpdateAvailableTargets` postfix): each weapon with a rule picks among enemies it can damage (`CanTarget`) whose hull edge is within its range, sticky with the ship's re-check interval, finish threshold and level cap. No enemy in reach means the weapon follows the ship.
- Postfix on the `AbstractTurret.currentTarget` getter returns the weapon's pick. Aim, lead, firing and missile launch all read that getter. Fast path: returns at once while no weapon has a pick; the pick table is a reference-keyed dictionary.
- Weapon rules only act while the ship itself is fighting (`priorityTarget` set, not Hold Fire). Your clicks on the flagship and attack orders to fleet ships switch them off (`ManualWins`); Escort/Defensive assist targets don't.
- Per-ship popup: one dropdown per combat hardpoint ("Follow the ship" or any rule). `TargetPriority.CombatWeaponCount` feeds the FleetStrip popup check.

Verified: harness 43 pass, 0 fail, 4 skip. `weapon-pick`: ship held the nearest enemy (Slashfang), hardpoint 0 with "Lowest hull %" returned the weakened enemy in its range (Shrapnel) and hardpoint 1 still returned Slashfang. `weapon-aim`: within 3 s the turret's own `Update` parented its tracking point to the weakened enemy, so the game's aim code reads the patched getter (no inlining problem).

Open: a ship with 10+ combat hardpoints makes the per-ship popup tall (26 px per row); no scroll yet.

## 2026-10-30: fleet target priority (`TargetPriority`, wishlist "better turret targeting", plan #5)

Added:
- `src/IgiQoL/Features/TargetPriority/TargetPriority.cs`: feature, ranking engine (`TargetRanking`) and patches (`TargetPriorityPatches`).
  - Prefix on `AbstractTargetingModule.FindClosestFilteredTarget`: a player ship's first pick ranks `filteredTargets` by its rule instead of taking the nearest.
  - Postfix on `CombatModule.UpdateAvailableTargets`: on the re-check timer, switches the auto target to the rule's pick if it is clearly better (margins: 20% / 2 units closer, 10 hull points, 25% more damage, or a better tier for discrete rules); drops a fleet ship's auto target past its flagship leash. The scan list is the scanner's fresh `List<TargetableUnit>`; `filteredTargets` is empty while a target is held, so it can't be used for re-picks.
  - Prefix on `AbstractUnit.AssistPlayerAgainstAttacker`: with a rule other than Nearest, a ship that already has an auto target no longer jumps to whoever just hit a nearby ally. Ships without a target still answer.
  - Rules: Nearest (vanilla), lowest/highest hull %, most damage to my fleet (sum of the game's `targetedBy` damage trackers on your ships), attacking my flagship (`flagship.IsTargetedBy`), smallest/largest hull class (`shipRoleType.GetTypeSize()`, drones and carrier fighters 0, non-ships 9), lowest/highest level. Level cap is soft (over-cap enemies only when nothing else is in range); leash is hard and applies to fleet ships only.
- Config `[TargetPriority]`: `Rule`, `RepickSeconds` (0-30, 0 = vanilla stickiness), `FinishBelowPercent` (0-100). Defaults are vanilla.
- `ShipSettings`: `targetRule`, `repickSeconds`, `finishBelow`, `maxLevel`, `leash`, saved in the ship's `igiQol` record.
- Per-ship popup (`UI/ShipPopup.cs`): Target choice, Re-check the target, Finish weak targets, Avoid enemies above level, Only attack within (units of the flagship).

Changed:
- `Core/FeatureRegistry.cs`: registers `TargetPriority` (startup now 20 active with Dev on).
- `Features/FleetStrip/FleetStrip.cs`: popup self-check expects 3 more dropdowns when TargetPriority is active.
- `README.md` feature table; `docs/qol-modpack-plan.md` overview statuses refreshed to match what has shipped.

Verified (dev save at a conquest station, fleet Eclipse + Hurricane): harness 41 pass, 0 fail, 4 skip. The self-check spawns three size-2 combat hulls at the player's level, weakens the one farthest away to 20% hull and checks: the ranking pick, the first pick through the game's `UpdateAutoTarget` path, the re-pick postfix, and a live run where the Aggressive fleet ship Hurricane started on a healthy enemy (Shrapnel) and the game's own scanner tick moved it to the weakened one (Ripsaw) within 8 s. The game pauses every player ship's scanner while a route is set (`ScannerModule.Update`), so the check clears the route for the test and restores it.

Not done / open:
- Disengage below Y% hull (mod-plans §3 conditions) was left out.
- Torpedo bays get the new target through `AddManualTarget(…, overrideTarget: false)`, the same call vanilla uses on a fresh pick; not checked in play.
