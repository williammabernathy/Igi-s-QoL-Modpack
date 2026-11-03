# Combat stations

This page covers the physical stations you can shoot at: the parts they are built from, how tough each part is, what drops when it dies, what happens to the station and its owner when the last part goes, and how the same layouts feed the off-screen siege numbers. Values are read from the game code of version 0.8.3.4 (the decompiled source, not in-game testing). Part stats come from the prefab data in `resources.assets`, read without launching the game.

Terms and abbreviations are explained in the [glossary](glossary.md).

Station-wide topics that live elsewhere: how the conquest map uses these numbers ([conquest.md](conquest.md)), how factions pay for and rebuild stations ([faction-economy.md](faction-economy.md)), what happens to a shot between the turret and the hull ([combat-damage.md](combat-damage.md)), the general level and rarity curves ([scaling.md](scaling.md)), and boarding a station's facility ([boarding.md](boarding.md)).

## Which station is which

The game uses "station" and "outpost" for several different things, and the rules below differ by kind. This page uses these names:

| Name | What it is | Code name |
|---|---|---|
| hub | The main station of a war-zone system, shown as "Space Station" on the map. It carries the owner's flag. Destroying it abandons the system. | `ConquestStation` |
| headquarters | The one system a faction cannot lose to a fleet. Its hub has the faction commander and shop. | `ConquestSystem.headquarters` |
| Defense station | A station type that holds a garrison (NPC factions) or patrol fleets (yours) and fires on enemies. Not the same as the Defense number on the system card. | `FactionStation` or `PlayerStation` of type Defense |
| economic outpost | A station that produces something: Mining, Salvage, Refinery, Forge, Production, Trading, Recruitment or Construction Yard. Raids target these. It is not a Defense station. | `FactionStation` that is not a Defense station, or `PlayerStation` |
| Industrial Outpost | A separate neutral map location type, shown by the industry board. It has nothing to do with economic outposts. See [Industrial Outpost stations](#industrial-outpost-stations). | `IndustryStation` |

A war-zone system is one where factions fight over ownership: the Conquest sector (Ara Martis, the original war zone) and, once unlocked, the expansion sectors. The Frontier, Staging and Darkspace regions are not part of it. NPC (non-player character) factions are the factions the game controls. A "location" below is anything you can fly to inside a system: a station, gate, asteroid field or Fleet Battle zone (the code calls it a point of interest, POI).

## Every station part

A station is a set of `CombatStationPart` objects joined by physics joints. The tooltip title is the part type name, followed by the level (`Lv. N`), the owner, and a single `Hull` row. Only the Station Core has its own name string. Every other part falls back to `{type} ({N}-Way)`, where N is the number of connectors the part has.

`Base HP` (hit points; the game shows "Hull HP", "Armor HP" and "Shield HP") below is the unscaled starting term: hull is `hullHPScale * 150`, shield is `shieldHPScale * 300`, armor is `armorHPScale * 300`. Shield and armor base HP only count when the part has a Shield Generator or Armor module slot, so a prefab's shield and armor scale is not always live (see the notes under the table). Modules add their own HP on top before the multipliers in the next section.

| In-game name | Prefab (after the faction prefix) | Base HP | Module slots | Weapon slots |
|---|---|---|---|---|
| Station Core | `Core_4C` | hull 900 | Hull, Scanner (Small) | none |
| Energy Field | `Energy1_4C` | hull 1350 | Hull, Scanner (Small) | none |
| Refinery | `Refinery1_2C` | hull 1350 | Hull, Scanner (Small) | none |
| Refinery | `EscortPartRefinery1_2C` | hull 1500 | Hull, Scanner (Small) | none |
| Cargo Dock | `Conq_CargoDock_4C` | hull 900 | Hull, Scanner (Small) | none |
| Docking Pad | `DockingSmall_2C`, `Conq_DockingSmall_2C`, `Conq_DockingMedium_2C` | hull 900 | Hull, Scanner (Small) | none |
| Docking Tunnel | `DockingTunnel_1C` | hull 900 | Hull, Scanner (Small) | none |
| Connector | `Connector_1C`, `Connector_1CSmall` | hull 315 | Hull, Scanner (Small) | none |
| Connector | `Connector_2C`, `_2CSmall`, `_3C`, `_4C` | hull 735 | Hull, Scanner (Small) | none |
| Defense Platform | `DefenseTurretS1_1C` | hull 450, shield 150 | Reactor, Hull, Scanner, Shield Generator (Small) | 1 Small |
| Defense Platform | `DefenseTurretS2_1C` | hull 600, shield 300 | same, Small | 2 Small |
| Defense Platform | `DefenseTurretS2_2C`, `_2CSlim`, `_3C` | hull 750, shield 420 | same, Small | 2 Small |
| Defense Platform | `DefenseTurretM2_1C` | hull 900, shield 900 | same, Medium | 2 Medium |
| Defense Platform | `DefenseTurretL1_1C` | hull 900, shield 900 | same, Large | 1 Large |
| Defense Platform | `DefenseDronebaySmall_2C` | hull 600, armor 600 | Reactor, Hull, Scanner, Armor, Drone Bay (Small) | none (drones) |

Notes on the table:

- The Core prefab has `armorHPScale` 3, but it has no Armor slot, and the base armor term is only added when an armor module is equipped. The Core therefore has no armor of its own.
- The `DefenseDronebaySmall_2C` prefab is typed as a Defense Platform in game, so it shows that name even though it has no turret slots. Its armor is the only armor base HP on any part.
- Some prefabs (Core, Energy Field, `Connector_1C`) have child objects named "Hardpoint" in the prefab. They are not listed in the part's `hardpointSlots`, so no turret is ever equipped there.
- The number before the `C` in a prefab name is not reliable as a connector count. The serialized connector list is rebuilt from the child objects at load, and for example `Energy1_4C` has four.
- In 0.8.3.4 the dedicated prefixes are `Generic_` (Kolyatov Collective and every faction without its own prefix), `Stellar_` (Stellar Industries), `Luminate_` (Luminate Combine) and `Corsair_` (Corsair Syndicate). Each has the same 23 prefabs. I compared the part type, hull, shield and armor scale, module slots, weapon slot sizes, tonnage and experience reward across all four prefixes, and only the sprites and connector references differ. The prefix changes how a station looks and nothing else.

Source: `CombatStationFactory.GetFactionPrefix`, `AbstractUnit.baseHullHP`, `AbstractUnit.baseShieldHP`, `AbstractUnit.baseArmorHP`, `AbstractUnit.GetStatRaw`, `CombatStationPart.BuildTooltipCard`, `CombatStationPart.GetDisplayName`, prefab data for `*_Core_4C` and the other `StationParts`.

### What each part does in a live fight

Only Defense Platforms shoot. A part gets turret behavior (`CombatActions`) only when its prefab lists weapon slots, so Core, Energy Field, Refinery, Cargo Dock, Docking Pads, Docking Tunnels and Connectors are targets and nothing else. Everything below is pure hull for you to chew through.

| Part | In a fight |
|---|---|
| Station Core | Always drops credits. Counts as a required part for station destruction like any non-connector. In Station Defense missions the Core is set up as the "defense subject" the mission protects. |
| Energy Field | Hull and loot only (see below). |
| Refinery | Hull and loot (ore, canisters, blueprints). |
| Cargo Dock | Hull and loot (credits and refined parts). |
| Docking Pad, Docking Tunnel | Hull and loot. They hold the docking points ships use, which matters only for boardable facilities. |
| Connector | Hull and a little loot. Never required for station destruction. |
| Defense Platform (turret) | Shoots, has the only shields on any part, and can drop one of its turrets. |
| Defense Platform (drone bay) | Carries a Drone Bay module and has no turret slots. It drops nothing from the platform loot table. Drone launching and targeting are not read here. |

### The Energy Field

The Energy Field does nothing special. It is a 1350 base hull part with no weapons, no aura and no effect on the other parts. Its code-side role is the loot table (warp fuel, rarely Energy Crystals) and the structure score for the off-screen siege (20 hull, 0 firepower). Every part prefab carries a child object called `RangeBonus` with a `BoostStat` of +0.75 on `WeaponRange`. Nothing registers it as a stat source for a station part: a part's stats come only from equipped modules and turrets (and saved stat boosts), because only equipment and ship upgrades register themselves as stat sources. It does not extend turret range for any part.

Source: `CombatStationPart.CreateAutoActions`, `CombatStationPartData.CreateLootTable`, `StationDefense.ScorePart`, `AbstractUnit.RegisterStatSource`, `AbstractEquipment` (the only registrant), `BoostStat`.

## How tough a part is

### The stat formula

Each of hull, armor and shield HP is built the same way:

```
HP = (base term + sum of equipped module HP) * M
M  = rankHP * HpBalance(level) * NpcBase(level) * escalationHP * stationContext
```

- `base term`: the numbers in the table above.
- `rankHP`: 0.7. Station parts are never given a rank in code, so they keep the default rank (Rookie), whose HP multiplier is 0.7.
- `HpBalance(level)`: `(1 + 0.28 * clamp01((L-10)/30)) * (1 + 0.6 * max(0,(L-40)/60)^1.5)`.
- `NpcBase(level)`: `4 ^ clamp01((L-30)/70)`, using the default peak of 4. It also multiplies the part's combat power (see war effort below).
- `escalationHP`: the Escalation tier multiplier, 1 / 1.15 / 1.35 / 1.6 / 2.8 / 3.8 / 5 / 6.2 / 8 for tiers 0 to 8, times the per-save override (1 to 4). Damage uses its own table, 1 / 1.08 / 1.18 / 1.3 / 1.5 / 2 / 4 / 6 / 10.
- `stationContext`: x1, x10, x20 or x30 (next table).

The NPC (non-player character) ship HP curve (`NpcShipHpCurve`) is only applied to ships, not to station parts. The general level curves are on [scaling.md](scaling.md).

`L` is the part's own level. At spawn the part's equipment is built at the location's level (items roll within one level of it), and the part level is then `round((average item level + highest item level) / 2)` over the equipped modules and turrets, so it sits at or very near the location's level.

Combined multiplier at escalation tier 0 (`rankHP * HpBalance * NpcBase`), before the context multiplier:

| Level | HpBalance | NpcBase | Combined | In a hub or Defense station (x20) | In a headquarters hub (x30) |
|---|---|---|---|---|---|
| 10 | 1.00 | 1.00 | 0.70 | 14.0 | 21.0 |
| 30 | 1.19 | 1.00 | 0.83 | 16.6 | 24.9 |
| 45 | 1.30 | 1.35 | 1.22 | 24.5 | 36.7 |
| 60 | 1.43 | 1.81 | 1.81 | 36.2 | 54.3 |
| 80 | 1.70 | 2.69 | 3.20 | 64.0 | 96.0 |
| 100 | 2.05 | 4.00 | 5.73 | 114.7 | 172.0 |

Worked example: a level 60 Station Core in a hub has a base hull term of 900, so about 900 x 36.2 = 32,600 hull before the Hull module's own HP is added (and before any Escalation multiplier). The part's tooltip `Hull` row shows the real current and maximum.

The HP that equipped modules add is not listed because it comes from equipment rolls that I did not read. Treat the tooltip number as the truth and the base terms above as the floor.

### Context multiplier

The context multiplier is chosen from where you are, not from who owns the part. It is looked up from the location you are at (or flying to) when stats are calculated.

| Where the part is | Multiplier |
|---|---|
| A hub (`ConquestStation`) in a headquarters system | x30 |
| A hub in any other war-zone system | x20 |
| A faction Defense station (`FactionStation` of type Defense) in a war-zone system, including a headquarters system | x20 |
| A boardable facility (dungeon station) | x10 |
| Everything else: other faction stations (economic outposts such as Construction Yards and Refineries), neutral guild stations, skirmish and pirate stations, mission stations, Industrial Outpost stations | x1 |

So a faction Construction Yard in a war-zone system has the same tiny HP a mission station has, while the Defense station next to it has twenty times as much.

Source: `CombatStationPart.GetStationHpMultiplier`, `CombatStationPart.GetConquestDefenseSystem`, `CombatStationPart.AddStatModifiers`, `AbstractUnit.RecalculateStats`, `AbstractUnit.ApplyNpcRankModifiers`, `AbstractUnit.ApplyHpBalanceBonus`, `AbstractUnit.ApplyEscalationNpcModifiers`, `GameMath.HpBalanceMultiplier`, `EscalationHelper.GetNpcBaseMultiplier`, `UnitRankHelper.GetHpMultiplier`, `AbstractUnitData.RecalculateLevel`, `BasePoiManager.AddToWorld`.

### War effort on station weapons

In the same contexts as the x20 and x30 multipliers (a hub, or a Defense station in a war-zone system), the part's combat power, drone power and torpedo power are multiplied by that system's war effort multiplier, `max(0, 1 + warEffortBonus - warEffortPenalty)`, but only while the system has a war effort running. War effort is the firepower bonus that Conquest missions you complete give a system's stations; it lasts 3 hours from the last one. It applies to stat indices 21, 25 and 26 (`CombatPower`, `DronePower`, `TorpedoPower`). Escalation and `NpcBase` multiply the same three stats on top of that, which is why a high-level station hits much harder than a low-level one. How war effort is earned and decays is on [conquest.md](conquest.md).

Source: `CombatStationPart.ApplyWarEffort`, `ConquestSystem.warEffortMultiplier`, `AbstractUnit.ApplyEscalationNpcModifiers`.

### Module gear and weapons

Modules on a part are built at the part's level, with the rarity fixed at HighGrade. This overrides the normal rarity roll, so every station part has HighGrade modules whatever the level. Two rules in `StationGearRollRules` apply to everything the part equips (modules and turrets):

- Optional bonus stats can never roll Hull, Armor or Shield HP on that gear.
- The size scaling for HP stats is capped at Medium. A Large module's HP stat scales like a Medium one (x1.5) instead of x2.25. Other stats on the same Large module still use the full size scale.

For NPC parts the module is a uniform random pick among every builder of that slot and size, with no level gate.

Turrets are chosen by the weapon slot size:

- Each slot gets a uniform random combat turret builder of that slot's size, any level.
- A part with two or more weapon slots has a 25% chance that one random slot is a Laser Painter (a support turret) instead of a gun, when a Laser Painter exists for that size.
- Turret rarity is rolled from `u = random(0, 0.4 + L/100)`: below 0.2 Standard, below 0.5 Enhanced, below 0.8 HighGrade, below 0.95 Exotic, else Legendary.

| Part level | Standard | Enhanced | HighGrade | Exotic | Legendary |
|---|---|---|---|---|---|
| 10 | 40% | 60% | 0 | 0 | 0 |
| 30 | 29% | 43% | 29% | 0 | 0 |
| 45 | 24% | 35% | 35% | 6% | 0 |
| 60 | 20% | 30% | 30% | 15% | 5% |
| 80 | 17% | 25% | 25% | 12% | 21% |
| 100 | 14% | 21% | 21% | 11% | 32% |

The equipment seed is the location id, the station seed and the part's position in the build order, so a given station always rolls the same gear. Damage-dealt and fire-rate scaling for the turrets is on [combat-damage.md](combat-damage.md).

Source: `CombatStationPartData.fixedModuleRarity`, `CombatStationPartData.StationGearRollRules`, `AbstractUnitData.LoadDefaultEquipment`, `AbstractUnitData.EquipDefaultModuleSlots`, `AbstractUnitData.EquipDefaultHardpoints`, `AbstractUnitData.GetSupportTurretFilter`, `AbstractUnitData.DetermineSupportSlotIndex`, `AbstractUnitData.GetDefaultEquipmentRarity`, `EquipmentBuilder.GetRandom`, `EquipmentBuilder.GetStatSize`, `CombatStationData.GetEquipmentSeed`.

## What drops when a part dies

Loot is rolled when the part is destroyed, using the global random generator, so it differs on every kill. Level `L` is the part's level. Integer random ranges are taken as upper-bound exclusive. In auto-play (idle) mode each non-fixed item has a 50% chance to be skipped.

| Part | Drop |
|---|---|
| Connector | 70%: 1 refined part (Titanium Plate, Bulkhead, Hull Beam or Structural Component), at level above 25 a second one with 50% chance. Each amount is `1 + random(round(L/12), round(L/8))`, halved (minimum 1) for Connectors. |
| Docking Pad, Docking Tunnel | Same refined-part roll as Connectors, without the halving. |
| Cargo Dock | The refined-part roll, plus 70%: credits worth `GetCreditsValue(random(4.5, 5.25), L)`. |
| Energy Field | 50%: HighGrade warp fuel, `1 + random(round(L/20), round(L/16))`. Above level 25, 4%: Energy Crystals, `1 + random(round(L/45), round(L/20))`. |
| Refinery | 50%: ore of this system, a surface ore `1 + random(round(L/6), round(L/4))` and a core ore for half that amount (minimum 1). Inside that 50%, another 50% for two further entries (see the note below). |
| Refinery and Core | Also 30%: a blueprint of level L (98.5% of the time Exotic and higher blueprints are excluded). |
| Station Core | Always credits worth `GetCreditsValue(random(13.5, 15.25), L)`. |
| Defense Platform with turrets | 40%: one of its equipped turrets, rebuilt at its exact level and rarity (55% of the time restricted to the lowest-rarity turrets it carries). Separately 20%: a stack of one of its ammo types worth about `GetCreditsValue(1 or 2, L)`, capped at what it holds. |
| Defense Platform with a drone bay | Nothing from the table. |
| Fixed loot on the part | Always dropped (not skipped in auto-play). Station missions and some skirmish and pirate stations add a pair of gear drops and a skill-point item here (see layouts). |

Games running the sandbox storyteller add to the Core: 80% for 1 to 2 Locked Container Keys, and in a conquest system also a Loot Box of the part's level.

Note on Refineries: the decompiled code looks up the canister item for the surface and core ore and then yields the core ore item (x4 and x2 the rolled amount) in both extra entries, not the canisters. This may be a decompiler artifact. Treat the extra two entries as "bulk core ore, possibly canisters" until checked in play.

Killing a part also pays kill experience, `ceil(50 * gapFactor * 2^(effectiveLevel/9))`, where the base is 5 x the part's reward of 10 and the gap factor is the usual level-gap penalty or bonus (see [scaling.md](scaling.md)). The Rookie rank (the lowest ship rank) adds no extra multiplier. Industrial Outpost stations pay no loot, experience or reputation change.

Source: `CombatStationPartData.CreateLootTable`, `AbstractUnitData.CreateLootTable`, `AbstractUnitData.FilterLoot`, `LootRarity.SelectDroppableEquipment`, `LootManager.DropLoot`, `LootManager.DropRegularLoot`, `LootManager.AddKillExperience`, `GameMath.GetExperienceRewardValue`.

## What counts as destroyed

- A part is destroyed when its hull HP reaches 0. Shields and armor only soak damage first; they do not remove the part (the exact order is on [combat-damage.md](combat-damage.md)).
- A destroyed part is removed from the station's saved data for good. Partial damage is saved and carries over to your next visit.
- There is no cascade. Killing a part does not kill, detach or weaken any neighbor. Parts are joined by joints that sit on the parent part; a destroyed part's joints go with it, so what hung off it is released and can drift. That is a physics effect read from the joint wiring, not a destruction rule.
- When a part is destroyed by damage (not removed some other way) it releases a shockwave. Every ship within 8 units is pushed, with the force `(8 - clamp(distance, 1, 8)) * 0.6 * 1500`, which is 6300 at 1 unit or closer and 0 at 8 units. The push arrives 0.62 seconds later. It does not damage ships. Non-ship targets that anyone can damage take 10.
- The station counts as destroyed when every part that is not a Connector is gone. Connectors can survive and the station is still destroyed.
- If you hit a part at any point before it dies, the station is flagged as having player contribution. See the next section for what that changes.

Source: `CombatStationPart.OnDestroy`, `CombatStationPart.CheckCombatStationFullyDestroyed`, `CombatStationPart.RecordPlayerContribution`, `CombatStationData.CleanupParts`, `CombatStationData.ConnectPartPhysics`, `PhysicsInteraction.ApplyShockwaveToNearbyShips`.

### When the last part dies

All of this runs once for the whole station:

1. The mission trigger `CombatStationDestroyed` fires, which completes "Destroy the enemy station" objectives.
2. `ConquestOwnership.OnStationDestroyed` runs for the current station. If it is the hub of an owned war-zone system, the system is abandoned and handed on; what that does is on [conquest.md](conquest.md). A hub flagged `rebuildOnLeave` is skipped.
3. If the station is a faction station that is not already wrecked: if you contributed, it is wrecked on the spot (next section). Then, if an active live siege of that Defense station exists, the siege ends with the attacker taking the station. Otherwise, and if it is not wrecked yet, the faction station is destroyed.
4. In a pocket system, the achievement for a pirate or warzone pocket is awarded.

A Defense station that is not destroyed by your hand but is taken during a live siege (`DefenseStationDestroyed`, shown in the Empire Log as "Defense station destroyed" or "Defense station lost") goes through `FactionStationDestruction.Destroy` the same way.

### Wrecking a faction station

Wrecking applies to `FactionStation` (Defense stations and economic outposts such as Construction Yards, refineries and production stations), not to hubs or guild stations.

- If you contributed, `FactionStationDestruction.OnDestroyedByPlayer` runs when the last part dies. Player builds queued at that station are refunded, 10% of each item stack in the station's faction storage (rounded down per stack) is dropped as loot at the destroyed part's position, the storage is emptied, the garrison is cleared, the station is marked wrecked, and your reputation with the owner drops by 5000 (1000 if you are already Hostile or worse with the owner, meaning reputation -5000 or lower). The reputation loss is skipped if you have the Umbral transponder. The event log reads "Station destroyed! -N reputation".
- Wrecked stations stop running jobs. When you leave, the station location is removed. If any recoverable materials (10%) or dropped items remain, a "Wreckage of {station}" salvage site is created that lasts 3600 seconds.
- Without player contribution (for example a conquest siege kills it), the same removal happens through `FactionStationDestruction.Destroy`: builds are refunded, an Empire Log entry is posted, and the station is removed or marked wrecked depending on whether you are there. There is no loot and no reputation cost.
- Destroying a Construction Yard does not cancel the ships its faction has already queued. That is covered on [faction-economy.md](faction-economy.md).

Source: `CombatStationPart.OnFactionStationDestroyed`, `CombatStationPart.WreckFactionStation`, `FactionStationDestruction.Destroy`, `FactionStationDestruction.OnDestroyedByPlayer`, `FactionStationDestruction.GetDestroyReputationLoss`, `FactionStationDestruction.SwapForWreck`, `StationDestruction.GetRecoverableMaterials`, `BattleManager.OnDefenseStationDestroyedLive`, `ConquestOwnership.OnStationDestroyed`.

## Reputation and force attacks

### Killing parts

Every part you kill hits the owner's reputation, unless the part is flagged `noReputationLoss` (several mission and Umbral stations are) or you have the Umbral transponder. This applies to every kind of station: hubs, Defense stations and economic outposts alike. Reputation bands (Wary, Neutral and so on) are listed in the [glossary](glossary.md#reputation).

- Each part's reward base is 10, so the base penalty is `ceil(10 * 0.5) = 5`.
- If your reputation with the owner is above -500 (Neutral or better), the penalty is multiplied by 20, so each part costs 100 and logs "Betrayal! -100 reputation". At -500 or below (Wary or worse), each part costs 5. Below -500 (Wary except exactly -500, and everything worse) the faction also counts as your enemy, as it does when you are at war with it (`FactionData.IsEnemy`), so its parts can be targeted with a plain click.
- If the faction that owns the system you are in is an enemy of the part's owner, and your reputation with that system's faction is above -500 and below 1500 (that is, Neutral), that faction gains `ceil(5 * 0.2) = 1` reputation per kill.

A full Large conquest layout (`Large1`) has 45 parts, connectors included, so wrecking a friendly station can cost several thousand reputation in part kills before the 5000 for the station itself.

### Ctrl + left-click on a friendly station

Left-clicking a hostile part sets it as your manual target. Ctrl + left-click on a part whose faction is not hostile to you (the tooltip hint "force attack") works only when all of these are true:

- The part is not already hostile to you.
- You are in a sector in a quadrant above 1.
- The location you are in is not a `SpaceStation`.

`FactionStation` (Defense stations and economic outposts), `ConquestStation` (hubs), `EmbassyStation`, `PlayerStation`, `IndustryStation` (Industrial Outposts) and every guild or neutral station are `SpaceStation`s, so you cannot force-attack any of them. The rule lets you force-fire at non-hostile parts at combat-type locations (mission, skirmish and pirate stations, and boardable facilities). Embassy station parts are also always friendly, so they are not targeted by normal means either.

When you do attack a non-hostile part, the part's `playerHostile` flag is set. Any damage you deal to a unit whose faction is not your enemy also marks every unit of that faction at that location hostile to you. The reputation cost is the kill penalty above. A part that is already hostile to you (a faction at war with you, or a flagged unit) can be targeted with a plain click.

Source: `LootManager.HandleKillReputation`, `LootManager.DropLoot`, `AbstractUnit.CanBeForceFired`, `AbstractUnit.TakeDamage`, `CombatStationPart.OnMouseUpAsButton`, `BasePoiManager.MarkFactionPlayerHostile`, `AbstractUnitData.IsEnemy`, `MapPointOfInterest.FinishSeedBuiltStation`.

## Station layouts

Which layout a station gets depends on how it was created.

`SpaceStation.SetStationSeed` gives every `SpaceStation` a seed `{size}{digit}` where the digit is 0 to 6. The size comes from the number of facilities the station has: fewer than 4 is Small, 4 or 5 is Medium, 6 or more is Large. Faction stations (`FactionStation`) and player stations are created with no facilities, so they are always Small.

| Seed | Builder | Result |
|---|---|---|
| `Small0`..`Small3` | `CreateSmallConquestStation1` | 19 parts |
| `Small4`..`Small6` | `CreateSmallConquestStation2` | 18 parts |
| `Medium0`..`Medium3` | `CreateMediumConquestStation1` | 23 parts |
| `Medium4`..`Medium6` | `CreateMediumConquestStation2` | 27 parts |
| `Large0`..`Large6` | `CreateLargeConquestStation1` | 45 parts |
| (puppeteers station, quadrant 2) | `CreateUmbralOutpost` | 6 parts |
| `SoloMediumStation`, `MediumSkirmish` | `CreateMediumStation` | 14 parts |
| `PirateLarge` | `CreateLargeStation` | 18 parts |
| `EasyStation1` | `CreateEasyStation1` | 9 parts |
| `EasyStation2` | `CreateEasyStation2` | 10 parts |
| `EscortLocation` | `CreateEscortLocation` | 6 parts |
| (window dressing, Industrial Outpost stations) | `CreateGunPlatform` | 3 or 4 parts |

The faction prefix picks the artwork: Luminate Combine uses `Luminate_`, Stellar Industries `Stellar_`, Corsair Syndicate `Corsair_`, and every other faction `Generic_`.

"Umbral outpost" in the tables on this page is the code builder name (`CreateUmbralOutpost`) for the puppeteers' station in quadrant 2. It is not an economic outpost or an outpost system.

### Part lists

Turret entries marked "tier" follow the turret tier rule below. Connector counts are in the last column.

| Builder | Core | Energy | Refinery | Cargo Dock | Docking Pad (Small / Medium) | Tunnel | Platforms | Connectors |
|---|---|---|---|---|---|---|---|---|
| Small1 | 1 | 0 | 1 | 2 | 3 / 2 | 2 | 3 tier | 5 |
| Small2 | 1 | 1 | 0 | 1 | 2 / 1 | 1 | 2 tier + S1 | 8 |
| Medium1 | 1 | 1 | 0 | 2 | 4 / 2 | 2 | 6 tier | 5 |
| Medium2 | 1 | 1 | 0 | 2 | 4 / 2 | 2 | 3 tier + 2 S2_3C + drone bay | 9 |
| Large1 | 1 | 2 | 0 | 3 | 6 / 2 | 2 | 4 tier + 2 S2_2C + drone bay | 22 |
| Umbral outpost | 1 | 0 | 1 | 0 | 1 / 0 | 1 | 2 tier (same roll) | 0 |
| Medium station | 1 | 1 | 0 | 0 | 1 / 0 | 0 | S1_1C, S2_1C, S2_3C | 8 |
| Large station | 1 | 1 | 0 | 0 | 2 / 0 | 0 | S1_1C, 2 S2_2C, 2 S2_2CSlim, S2_1C | 8 |
| Easy 1 | 1 | 1 | 0 | 0 | 0 | 0 | 2 S1_1C | 5 |
| Easy 2 | 1 | 1 | 0 | 0 | 1 / 0 | 0 | S1_1C + drone bay | 5 |
| Escort location | 0 | 0 | 1 (Escort Refinery) | 0 | 0 | 0 | S2_3C + drone bay | 3 |
| Gun platform variant 1 | none | none | none | none | none | none | S2_2C | 2 |
| Gun platform variant 2 | none | none | none | none | none | none | 2 S1_1C + drone bay | 0 |
| Gun platform variant 3 | none | none | none | none | none | none | S2_3C + S1_1C | 2 |

The gun platform variant is chosen at random (a roll above 0.666, between 0.333 and 0.666, and below 0.333 give variants 1, 2 and 3) unless the caller forces one. "Docking Pad" counts `Conq_DockingSmall_2C` and `Conq_DockingMedium_2C` in the conquest builders, and `DockingSmall_2C` in the others.

### Turret tier rules

In the Small, Medium, Large and Umbral builders each "tier" turret is `DefenseTurret{S2|M2|L1}_1C`:

- Below location level 45 it is always S2: two Small slots, hull 600 and shield 300 base.
- At level 45 or above it is M2 or L1 with a 50/50 roll. M2 is two Medium slots; L1 is one Large slot. Both have base hull 900 and base shield 900.
- The roll is seeded per station, except the Umbral outpost, which uses the global generator. In Medium1 the first two tier turrets share one roll, and in the Umbral outpost both turrets share one roll. Everywhere else each turret rolls on its own.

The fixed S1, S2_1C, S2_2C and S2_3C turrets in the other builders never change with level.

### Extra loot on mission and skirmish stations

- `MediumSkirmish` (faction skirmish locations): one random non-weapon, non-booster piece of equipment and one random turret, both at the location's level, with rarity rolled from the level and floored at HighGrade. The two items go on random non-connector parts. The Core also gets a bonus skill point template.
- `PirateLarge` (pirate hideouts, the pirate station in the conquest world, Hard to Insane Station Offense and Defense missions): same two items plus the skill point template, but the rarity is HighGrade, with Exotic at 5% above level 20 and a further 10% on the remainder above level 30 (14.5% at level 31 or higher).
- Station Offense and Defense missions pick the builder from the difficulty: Easy gets Easy 1 or 2, Normal the Medium station, Hard and above the Large station. In a Defense mission the station is player-friendly and its Core is the protected subject.

Source: `CombatStationFactory.CreateStationVariant`, `CombatStationFactory.DispatchStationVariant`, `CombatStationFactory.ResolveSizeVariant`, `CombatStationFactory.CreateSmallConquestStation1`, `CreateSmallConquestStation2`, `CreateMediumConquestStation1`, `CreateMediumConquestStation2`, `CreateLargeConquestStation1`, `CreateUmbralOutpost`, `CreateMediumStation`, `CreateLargeStation`, `CreateEasyStation1`, `CreateEasyStation2`, `CreateEscortLocation`, `CreateGunPlatform`, `CreateMediumSkirmishStation`, `CreatePirateHideoutStation`, `SpaceStation.SetStationSeed`, `SystemMapData.AddSpaceStation`, `MapPointOfInterest.BuildStationFromSeed`, `StationBattle.GenerateMission`, `PoiWindowDressingHelper.AddGunPlatform`.

## How long damage lasts

Damage and kills are saved, but stations are rebuilt from their seed in three situations, which restores every part at full HP:

- When more than 3600 seconds of game time have passed since you last visited a given space station, that station is rebuilt the next time the game saves.
- In every war-zone sector, the hourly conquest update (`DoConquestTick`, every 3600 seconds; the "Conquest Update" countdown card on the galaxy map) rebuilds every station except the one you are at and abandoned hulks.
- When a hub changes hands, the hub is rebuilt (after you leave, if you are there).

For a war-zone system this means that killing parts is temporary unless the hub itself is lost, which `OnStationDestroyed` handles on the conquest side.

Source: `GamePlayer.CleanupStaleSpaceStation`, `GamePlayer.ToJson`, `Conquest.DoConquestTick`, `Conquest.RepairConquestStations`, `ConquestOwnership.RebuildHub`, `ConquestOwnership.CompleteHubRebuild`, `MapPointOfInterest.ClearStationBuilding`.

## The station's off-screen siege numbers

The conquest map never reads the live HP of a station. It scores the station's part list instead, and a live fight does not write back into that score.

| Part | Structure (hull) score | Firepower score |
|---|---|---|
| Station Core | 200 | 0 |
| Connector | 5 | 0 |
| Every other non-platform part | 20 | 0 |
| Defense Platform | `60 + 15 * S` | `40 * S` |

`S` is the sum over the part's prefab weapon slots of `max(1, size)`: Small 1, Medium 2, Large 3. So S1 is 1, S2 is 2, M2 is 4, L1 is 3, and the drone bay (no weapon slots) is 0. Destroyed parts (HP 0 after damage) are skipped. Damaged parts count in full. An ungenerated station (never visited) uses a fixed estimate: Small 560 hull and 240 firepower, Medium 940 and 480, Large 1390 and 560.

Totals per builder, with every tier turret S2 (below level 45), and the range when the tier turrets are all M2 or all L1:

| Builder | Hull | Firepower |
|---|---|---|
| Small1 | 695 (740 to 785) | 240 (360 to 480) |
| Small2 | 615 (645 to 675) | 200 (280 to 360) |
| Medium1 | 985 (1075 to 1165) | 480 (720 to 960) |
| Medium2 | 975 (1020 to 1065) | 400 (520 to 640) |
| Large1 | 1210 (1270 to 1330) | 480 (640 to 800) |
| Umbral outpost | 440 (470 to 500) | 160 (240 to 320) |

These are summed over the system's hub and every un-wrecked same-faction Defense station, scaled by the sector's station-defense bonus, and halved while the system is settling (the hours after a system changes owner; the game has no name for this). A station siege uses this hull number as the "station structure" pool. A raid on an economic outpost (any non-Defense station) uses 15% of the hull and 50% of the firepower. How the Defense number, siege resolution and raids use these figures is on [conquest.md](conquest.md).

Because the score ignores partial damage and the live fight does not feed back, shooting a Defense Platform's hull down without killing it changes nothing off-screen. Killing parts does lower the score until the station is rebuilt.

Source: `StationDefense.ScorePart`, `StationDefense.ScoreParts`, `StationDefense.EstimateFromSize`, `StationDefense.GetStationProfile`, `StationDefense.GetProfile`, `StationDefense.GetDefendingStations`, `StationRaid.GetRaidProfile`, `BattleInstance.StartStationStructure`, `BattleManager.StartDefenseStationStage`.

## Defense stations and garrisons

A Defense station is a `FactionStation` of type Defense (NPC factions) or a `PlayerStation` of that type (yours). The NPC kind is built with the Small layouts (Small1 or Small2), and has the x20 HP multiplier while you are in a war-zone system.

| Station tier | Garrison ships wanted | Point budget | Points per ship |
|---|---|---|---|
| 1 | 3 | 270 | 90 |
| 2 | 4 | 405 | 101 |
| 3 | 5 | 540 | 108 |
| 4 | 6 | 720 | 120 |
| 5 | 8 | 900 | 112 |

Each garrison ship is a warship chosen to fit its per-ship budget at the NPC fleet level. "Points" are the fleet value the game shows as "strength" on fleet cards. Ways a garrison is refilled:

- Warships built at faction yards and delivered to the station (priority is on [faction-economy.md](faction-economy.md)). In practice this is the only refill path.
- `Conquest.DrainCombatStrengthIntoGarrisons` runs each conquest update and would add one ship per 10 combat strength through `FactionGarrison.Repair` (900 s timer). It never fires, because nothing in the code raises a system's `combatStrength` above 0 (see [conquest.md](conquest.md)).
- Newly seeded Defense stations are provisioned to their wanted count.

Refit: when the garrison's level is below the current NPC fleet level and the system is not besieged, a 300 second refit starts and raises every ship below that level to it. It does not add ships. Refits stop while the station is in raid recovery, and a wrecked station runs no jobs at all.

When a Defense station siege is running and you are at that station, the garrison ships are spawned into the live fight as real ships at their stored level, hull rating and seed, along with the defending fleets in the system. When the siege targets the hub instead, the garrisons of every Defense station in the system and the player patrols spawn at the hub. Ships killed in the live fight are removed from the garrison. When the station is taken (live or at the siege cap) it goes through `FactionStationDestruction.Destroy`: if you are there the garrison is cleared and the station is marked wrecked, otherwise it is swapped for a wreck right away. A system holds at most 2 faction stations of any type.

Source: `FactionGarrison.GetWantedCount`, `FactionGarrison.PickShip`, `FactionGarrison.Repair`, `FactionGarrison.ProgressRefit`, `FactionGarrison.CompleteRefit`, `FactionGarrison.SpawnInto`, `Conquest.DrainCombatStrengthIntoGarrisons`, `ConquestFleetPlanner.ChooseGarrisonToSupply`, `WarshipDeliveries`, `BattleManager.SpawnSiegeDefenders`, `BattleManager.EnsureVenueUnits`, `BattleManager.TakeDefenseStation`, `FactionStation.MaxPerSystem`.

## Player stations

A player-owned station is a `PlayerStation` built with no facilities, so it uses a Small layout with the `Generic_` prefix. Nothing in the combat station code treats it differently from other Small stations.

- `StationClaim` is about mining and salvage claims that economic outposts stake in their system, with 300 to 180 second re-stake cooldowns by tier. It has no link to combat stations.
- `PlayerStationCapture` hands your stations over when the system you hold falls: build jobs are cancelled, serving ships are released, routes end, and a faction station is created for the captor holding the stock. 75% of the stock goes to your refund station and the rest stays with the captor. If you are at the station, the handover waits until you leave. The same file converts economic outposts you own into player stations (tier drops by 1, 10% of their storage moves over).
- `StationDecommission` removes a station on your request and leaves a "Remains of {station}" salvage site for 3600 seconds.
- A Defense station you own counts toward the siege profile of a system whose conquest owner is you.

Source: `StationClaim.GetCooldownSeconds`, `PlayerStationCapture.HandOver`, `PlayerStationCapture.HandOverStation`, `PlayerStationCapture.TakeOver`, `PlayerStationCapture.GetTakeoverTier`, `StationDecommission`, `StationDefense.IsDefenseStation`.

## Industrial Outpost stations

An Industrial Outpost (the map location type shown by the industry board, `IndustryStation`) is a separate, neutral location. It is not an economic outpost: economic outposts are the faction and player production stations that raids target. The Industrial Outpost is built from `CreateEasyStation1` plus a variant 1 gun platform. Its parts have no HP multiplier and give no loot, kill experience or reputation change. They are kinematic, so they cannot be pushed. The mission fails when the Core is destroyed.

The part tooltip adds an "Industrial Outpost" section:

| Row | Meaning |
|---|---|
| Repair packs | The repair pool as a percentage of one pack. It starts at 100% and each Repair Pack from storage adds 100%. A pack is `6000 * DamageMultiplier(startLevel)` HP of repair. |
| Ammo stockpile | Starts at 100%. Each Ammo Pack adds 100%. Every enemy wave drains 10 points plus 5 points per filled turret slot. At 0% all turrets stop firing. Shown red below 25%. |
| Turret slots | Filled slots out of 4. The Repair packs row is shown red below 10%. |

Every 0.2 seconds the Industrial Outpost repairs armor, then hull, of each part by up to 10% of that part's maximum per tick, paid from the repair pool. Empty turret slots (offsets (20,-5), (20,5), (14,-10), (14,10) from the point) each pull one Turret Pack from storage and spawn a gun platform, one per tick. Waves of marauders (or fanatics) spawn when no enemies are left or 120 seconds pass, with strength `0.7 + 0.1 * wave`, and the first wave is delayed 45 seconds.

Source: `CombatStationPart.BuildIndustrySection`, `IndustrialOutpost.UpdateActive`, `IndustrialOutpost.repairMax`, `IndustryBoard.GenerateIndustryMissions`, `LootManager.DropLoot`.

## Boardable facilities

A facility (dungeon) station is built by `DungeonStationFactory` from Cores, Refineries, Energy Fields, Cargo Docks, Docking Pads, a Docking Tunnel and Connectors, with no weapons. Its parts have the x10 HP multiplier, but projectiles, rail cannons, torpedoes and manual drone targeting all ignore them (`IsProtectedFromWeaponFire`), and your combat modules do not pick them as targets. I did not find a weapon path that damages them. Their numbers are on [boarding.md](boarding.md); the part-side rules are:

- Facility integrity is the share of live non-connector parts, `live parts / total parts`, not HP.
- A facility is enterable only if no docking part is gone for good and integrity is above 10%.
- When the last Docking Pad or Docking Tunnel dies, the facility's `dockingDestroyed` flag is set, and it can no longer be entered. Losing the docking point of an active operation aborts the operation.
- When a saved facility with integrity below 100% loads, each part other than the first is removed with probability `1 - integrity` (seeded per facility), and the rest get seeded visual damage.
- Damage from the boarding fight is visual only. It breaks the part's sprite and does not change HP.

Source: `CombatStationPart.HandleDockingPartDestroyed`, `CombatStationPart.UpdateFacilityIntegrityFromCombat`, `CombatStationPart.CheckDungeonStationFullyDestroyed`, `DungeonData.isEnterable`, `DungeonLocationData.CollectOrDamagePart`, `DungeonStationFactory.Generate`, `TargetableUnit.IsProtectedFromWeaponFire`, `AbstractUnit.ApplyFacilityDamage`.

## What to do with this

Pure HP: Connectors, Docking Pads, Docking Tunnels, Cargo Docks, Energy Fields, Refineries and the Core. They never shoot. Their only reasons to die are loot and the final destruction check.

Kill order for a fight you have to win:

1. Defense Platforms, because they are the only damage on the station. Within them, the S1 and S2 platforms have the thinnest shields and go down fastest. M2 and L1 platforms carry 900 base shield, three times an S2_1C's, and the shield regenerates, so burst them rather than sniping.
2. Platforms with the strongest gear come first when you can tell. From level 80 a third or more of the turrets are Exotic or Legendary.
3. Everything else in whatever order is convenient. You never have to kill Connectors.

If the goal is loot:

| You want | Shoot |
|---|---|
| Credits | The Core (always), then Cargo Docks (70%). |
| Weapons | Defense Platforms (40% for one of their turrets). |
| Blueprints | Cores and Refineries (30% each). |
| Ore and canisters | Refineries. |
| Warp fuel | Energy Fields. |
| Crafting parts | Connectors, Docking parts and Cargo Docks (70%, refined parts). |

If the goal is to keep your reputation, do not take out the last non-connector part of a faction station you hit. The 5000 point loss and the wreck happen when that part dies with your damage on the station. The per-part penalty (100 points a part while you are Neutral or better with the owner) applies to every part you kill either way. If you are already Hostile or worse with the owner, the final penalty is 1000 and each part costs 5.

What does not work as it looks:

- Killing Connectors does not count toward destroying the station and removes only 5 hull from its off-screen score each. It can release the parts hanging off that Connector to drift, which is a physics effect and not damage.
- Shooting a Defense Platform's hull down without killing it changes nothing off-screen.
- Destroying a station in a war-zone system is undone within about an hour of game time after you leave, unless the hub itself is lost.
- A faction Construction Yard or Refinery (an economic outpost) in a war-zone system has none of the x20 HP of the Defense station beside it.
- The Energy Field is not an energy source for the station's weapons or a range booster. It is a 1350 hull sponge.

## Not verified

- HP added by equipped modules (Hull, Shield Generator, Armor) and the exact turret statistics, because they come from equipment rolls I did not read.
- Whether the extra two Refinery drops are canisters or core ore (decompiled output yields core ore).
- Whether station parts are included in the unit list used by `MarkFactionPlayerHostile`.
- How drone-bay platforms pick targets and how many drones they launch.
- What the sector quadrant numbers 0 and 1 are.
- Whether the rule "integer random ranges exclude the upper bound" holds for `SeededRandom.RandomRange`.
- How a faction station's tier rises (it sets the garrison size above).
- `CombatStationFactory.CreateFromLayout` has no caller in the decompiled code.
