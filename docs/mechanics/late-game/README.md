# Late game (spoilers)

This stage covers the story lines that run behind the war: the Umbral Reach and the puppeteers, Malcyte, and the closed Meridia sector. Everything on these pages is a spoiler for the story, so stop here if you want to find it in play. Values are read from the game code of version 0.8.3.4 (the decompiled source, not in-game testing). Anything not confirmed in code is marked "(not verified)".

Terms are in the [late-game glossary](glossary.md), the [basics glossary](../basics/glossary.md) and the [Conquest glossary](../conquest/glossary.md).

## Spoiler: Gates

- **The Umbral line.** The first mission of each faction's Conquest chain (Stellar, Kolyatov or Luminate, depending on your faction) pays 2,000,000 credits, 60 Vanguard Marks, XP, the second mission and the Umbral line. So the Umbral line is granted with the reward of your faction's first Conquest mission.
- **The Darkspace line.** The second mission (the 30% sector-control mission) starts the next story mission of the Darkspace line. I did not find that mission defined in the code.
- **Malcyte and Meridia.** In version 0.8.3.4 only the debug console wakes Malcyte or opens Meridia. I found no other caller. How either one happens in a normal game is "(not verified)".

In the code: [gates](#code-gates).

## Pages

| Page | What it covers |
|---|---|
| [Common questions](faq.md) | Short answers to questions players ask about the story lines, each linked to the section with the full numbers. |
| [The control story line](control-story.md) | The Umbral Reach control row and its spread, the Umbral Hacking Tool, the puppeteers' station, and every rule the Umbral transponder changes. |
| [The closed sector story line](closed-sector-story.md) | The closed Meridia sector and quadrant, Malcyte as a dormant, yardless raider, its fleet cap, boarding it, and its alliance with Meridia's Chosen. |
| [Glossary](glossary.md) | Terms for this stage. |

The pages that come before this stage: [Basics](../basics/README.md), [Conquest](../conquest/README.md) and [Beyond the Front](../expansion/README.md).

## In the game code

This part holds the code names behind the sections above. It comes from the decompiled source of version 0.8.3.4. To check a claim, search the decompiled source for the method or field named here.

### Code: Gates

The first mission of each faction's Conquest chain is `ConquestStellar1`, `ConquestKolyatov1` or `ConquestLuminate1`. Its reward is 2,000,000 credits, 60 Vanguard Marks, XP, the second mission and the Umbral line (`ConquestUmbral1`).

The second mission (the 30% sector-control mission: `ConquestStellar2` and its siblings) starts the next story mission, `ConquestDarkspace1`, which I did not find defined in the code.

`Conquest.WakeMalcyte` and `Conquest.OpenMeridia` are only called from the debug console in 0.8.3.4. The commands are `wmc` (wake Malcyte) and `omr` (open Meridia), in `Behaviour.UI.DebugScreen/ConsoleScreen.cs`. How either one happens in a normal game is `(not verified)`.

Source: `Conquest.WakeMalcyte`, `Conquest.OpenMeridia`, `Behaviour.UI.DebugScreen/ConsoleScreen.cs`.
