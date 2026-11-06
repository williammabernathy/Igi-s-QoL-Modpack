# Late Game (Spoilers)

This stage covers the story lines that run behind the war: the Umbral Reach and the puppeteers, Malcyte, and the closed Meridia sector. Everything on these pages is a spoiler for the story, so stop here if you want to find it in play. Values are read from the game code of version 0.8.3.4 (the decompiled source, not in-game testing). The code was then compared with the 0.8.3.5 source, and none of the rules on these pages changed.

Terms are in the [late-game glossary](glossary.md), the [basics glossary](../basics/glossary.md) and the [Conquest glossary](../conquest/glossary.md).

## Spoiler: Gates

- The Umbral line. The first mission of each faction's Conquest chain (Stellar, Kolyatov or Luminate, depending on your faction) pays 2,000,000 credits, 60 Vanguard Marks and XP. It also starts the second mission and the Umbral line. So the Umbral line is granted with the reward of your faction's first Conquest mission.
- The Darkspace line. The second mission (the 30% sector-control mission) pays 10,000,000 credits, 400 Vanguard Marks, 1 skill point and XP. It also starts the first mission of the Darkspace line. That mission is a single travel step to a point of interest in a Conquest staging system. It pays XP and 200 reputation with the Darkspace Compact, and it starts the next Darkspace mission.
- Malcyte and Meridia. In versions 0.8.3.4 and 0.8.3.5 only the debug console wakes Malcyte or opens Meridia, and only once the expansion is unlocked. Nothing in the story missions, dialogue, save loading or game assets does either, so in a normal game Malcyte stays dormant and Meridia stays closed.

In the code: [Gates](#code-gates).

## Pages

| Page | What it covers |
|---|---|
| [Common Questions](faq.md) | Short answers to questions players ask about the story lines, each linked to the section with the full numbers. |
| [The Control Story Line](control-story.md) | How the Umbral Reach control row grows and spreads, the Umbral Hacking Tool, the puppeteers' station, and every rule the Umbral transponder changes. |
| [The Closed Sector Story Line](closed-sector-story.md) | The closed Meridia sector and quadrant, Malcyte as a dormant raider with no yards, its fleet cap, boarding it, and its alliance with Meridia's Chosen. |
| [Glossary](glossary.md) | Terms for this stage. |

The pages that come before this stage: [Basics](../basics/README.md), [Conquest](../conquest/README.md) and [Beyond the Front](../expansion/README.md).

## In the Game Code

This part holds the code names behind the sections above. It comes from the decompiled source of version 0.8.3.4, compared with the 0.8.3.5 source. To check a claim, search the decompiled source for the method or field named here.

### Code: Gates

The first mission of each faction's Conquest chain is `ConquestStellar1`, `ConquestKolyatov1` or `ConquestLuminate1`. Its reward is 2,000,000 credits, 60 Vanguard Marks, XP, the second mission and the Umbral line (`ConquestUmbral1`). The reward list is built in `ConquestMissions.AddHQIntroRewards`. `GamePatch.TryAddUmbralMission` adds the same reward to an already running first mission, or starts `ConquestUmbral1` if the first mission is already archived.

The second mission (the 30% sector-control mission: `ConquestStellar2` and its siblings) pays 10,000,000 credits, 400 Vanguard Marks, 1 skill point, XP and `ConquestDarkspace1` (`ConquestMissions.AddConquestProgressRewards`; `GamePatch.TryAddDarkspaceMission` does the same catch-up for saves in progress). `ConquestDarkspace1` is defined in `UmbralMissions.ConquestDarkspace1`: one `TravelToPOI` step to a combat point of interest in a random conquest staging system, with rewards of XP, a `Reputation` reward of 200 and `ConquestDarkspace2`. The reputation reward names no faction, so `Reputation.OnComplete` applies it to the mission's source faction, `darkspacers`.

The `Conquest.WakeMalcyte` and `Conquest.OpenMeridia` methods are only called from the debug console in 0.8.3.4 and 0.8.3.5, and the console refuses both until `expansionUnlocked` is true. The commands are `wmc` (wake Malcyte) and `omr` (open Meridia), in `Behaviour.UI.DebugScreen/ConsoleScreen.cs`. The `Conquest.malcyteAwake` and `Conquest.meridiaOpen` flags have private setters. They are assigned only in those two methods and when `Conquest.LoadFromJson` reads a save, and `Conquest.DataToJson` writes each one only when it is true. The only code that reads a method by name at run time, `Characters`, looks up dialogue `Character` factories, and the story missions never reference either flag. A search of the `MonoBehaviour` data in `resources.assets` for the method and flag names found nothing. The questgiver line `ExpansionQuestgiver8` ("something in Meridia is about to break out") is plain dialogue in `ExpansionDialogues` and sets nothing. So in a normal game Malcyte stays dormant and Meridia stays closed.

Source: `ConquestMissions.AddHQIntroRewards`, `ConquestMissions.AddConquestProgressRewards`, `GamePatch.TryAddUmbralMission`, `GamePatch.TryAddDarkspaceMission`, `UmbralMissions.ConquestDarkspace1`, `Conquest.WakeMalcyte`, `Conquest.OpenMeridia`, `Behaviour.UI.DebugScreen/ConsoleScreen.cs`.
