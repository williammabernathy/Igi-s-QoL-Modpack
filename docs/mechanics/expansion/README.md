# Beyond the Front

This stage covers what opens once you accept the "Beyond the Front" mission: the expansion sectors, claiming and buying systems, your empire, and your own Empire Fleets. It needs player level 60 and one finished 30% sector-control mission. Values are read from the game code of version 0.8.3.4 (the decompiled source, not in-game testing). Anything not confirmed in code is marked `(not verified)`.

Terms and abbreviations are explained in the [Beyond the Front glossary](glossary.md), the [Conquest glossary](../conquest/glossary.md) and the [basics glossary](../basics/glossary.md).

## Spoiler: What level 60 and "Beyond the Front" gate

Level 60 is a real gate, but it works through the level cap. The game does not check your level when you take conquest actions.

| Gate | What you need | What it opens |
|---|---|---|
| Level 10 | Player level 10 and a system you hold | Empire Fleet budgets exist from level 10, but without a held system you can't field one. |
| Level cap 60 | You have not accepted "Beyond the Front" | You cannot pass level 60 until the expansion is unlocked. |
| "Beyond the Front" offered | Level 60, one finished 30% sector-control mission for your faction (the [30% sector-control missions](../conquest/sector-control.md#the-30-sector-control-mission)), and the expansion not yet unlocked | The mission only appears once you are at the cap and have finished your faction's 30% sector-control mission. |
| Expansion unlock | Accept the mission from the questgiver | Generates the expansion sectors, raises the level cap to 100, raises the loot box skill point cap from 10 to 30 and the reputation cap from Distinguished to Exalted, and widens the Darkspace Compact and Meridia's Chosen fleet caps. |
| Claim unlock | Complete the mission | Claiming systems, buying player-sector systems and the Empire level tab. Empire Fleets, Defense stations and patrol fleets also need a held system. |

The mission has three travel steps (the Penumbra gate, the player sector gate, the home hub) and pays 1 skill point and +300 reputation with Darkspace Compact. It then starts a follow-up mission that asks you to buy the first system.

So "level 60 or below" is simply everyone who has not accepted the mission, and "above level 60" is everyone who has. Between accepting and completing the mission you can already level past 60 and the expansion exists, but claiming is still locked. Your faction's 30% sector-control mission checks the "Area Conquered" figure (that faction's systems divided by all Conquest sector systems), so before you can leave level 60 the war itself has to go your faction's way. That mission pays 400 Vanguard Marks, 10,000,000 credits and a skill point; how to finish it is in [the 30% sector-control mission](../conquest/sector-control.md#the-30-sector-control-mission).

The expansion sectors are added to the count of all war-zone systems only once the expansion is unlocked, which is after that mission ([the 30% sector-control mission](../conquest/sector-control.md#what-the-objective-counts)).

Other places level 60 shows up, none of which gate conquest actions:

- Empire upkeep divides its cost multiplier by the multiplier at level 60.
- Legendary loot can drop only at level 60 or higher. Its weight is (your level - 60) / 2000, so it is 0 at exactly 60.
- The expansion's minimum system level is 60.
- The empire fleet budget steps every 5 levels (2500 points and 12 ships at level 60, see [slots and size](empire-fleets.md#slots-and-size)); 60 is not special in that table.

In the code: [what level 60 and "Beyond the Front" gate](#code-what-level-60-and-beyond-the-front-gate).

## Pages

- [Glossary](glossary.md): terms and abbreviations used in the Beyond the Front pages.
- [Your empire](your-empire.md): the expansion sectors, buying and claiming systems, the capital, Empire Level and upkeep, your stations, reputation from capture and claim, and the economy once the expansion war is open.
- [Empire fleets](empire-fleets.md): fleet slots and size, orders, stances, the outmatched warning, map labels, your fleets in the Defense number, and what changes above level 60.

The previous stage is [Conquest](../conquest/README.md); the mission that opens this one is described in [the 30% sector-control mission](../conquest/sector-control.md#the-30-sector-control-mission).

## In the game code

This part holds the code names behind the gate table above. It comes from the decompiled game code of version 0.8.3.4; to check a claim, look up the class and method named in the `Source:` line.

### Code: What level 60 and "Beyond the Front" gate

Level 60 is a real code gate, but it works through the level cap, not through a check on conquest actions.

| Gate | Condition in code | What it controls |
|---|---|---|
| Level 10 | `FleetLevelSteps.UnlockLevel` | Empire Fleet budgets exist from level 10, but you also need a held system (see the Claim unlock row). |
| Level cap 60 | `Conquest.maxPlayerLevel` is 60 while `expansionUnlocked` is false, 100 after | You cannot pass level 60 until the expansion is unlocked. |
| "Beyond the Front" offered | `ExpansionMissions.IsGoToExpansionAvailable`: `player.level >= 60`, and one of `ConquestStellar2`, `ConquestKolyatov2` or `ConquestLuminate2` completed (the [30% sector-control missions](../conquest/sector-control.md#the-30-sector-control-mission)), and the expansion not yet unlocked | The mission only appears once you are at the cap and have finished your faction's 30% sector-control mission. |
| Expansion unlock | Creating the mission (when you accept it from the questgiver) calls `Conquest.UnlockExpansion()` | Generates the expansion sectors, raises the level cap to 100, raises the loot box skill point cap from 10 to 30 and the reputation cap from Distinguished to Exalted, and widens the Darkspace Compact and Meridia's Chosen fleet caps (`Faction.ExpansionWarOpen`). |
| Claim unlock | Completing the mission pays `SystemClaimUnlock`, which sets `systemClaimUnlocked` | Claiming systems, buying player-sector systems and the Empire level tab (`CommandTabUnlocks.IsEmpireLevelUnlocked` is `systemClaimUnlocked`). Empire Fleets, Defense stations and patrol fleets also need a held system. |

After the mission completes it starts `StoryMissionBuyFirstSystem`, which asks you to buy the first system.

Your faction's 30% sector-control mission checks `currentConqueredPercentage` (the "Area Conquered" figure: that faction's systems divided by all Conquest sector systems).

Other level 60 references in code, none of which gate conquest actions: `EmpireUpkeep.ReferenceLevel` (the level whose cost multiplier upkeep is divided by), Legendary loot (`LootRarity` adds it only at level 60 or higher, weight `(level - 60) / 2000`, so it is 0 at exactly 60), and the expansion's minimum system level (`ExpansionLevels.SeamLevel` 60). The empire fleet budget steps every 5 levels (2500 points and 12 ships at level 60); 60 is not special in that table.

Source: `ExpansionMissions.IsGoToExpansionAvailable`, `ExpansionMissions.BuildBeyondTheFront`, `ExpansionDialogues.GetTooEarlyLine`, `Conquest.maxPlayerLevel`, `Conquest.UnlockExpansion`, `Conquest.maxLootBoxSkillPoints`, `Conquest.maxReputation`, `SystemClaimUnlock`, `GamePlayer.UnlockSystemClaim`, `ConquestMissions.CreateFactionConquestProgressMission` (`SystemsConquered` at 0.3), `Conquest.UpdateConquestArea`, `LootRarity.GetRarityChances`, `EmpireUpkeep`, `ExpansionLevels`, `ConquestFleetPlanner.MaxFleetsFor`, `CommandTabUnlocks`
