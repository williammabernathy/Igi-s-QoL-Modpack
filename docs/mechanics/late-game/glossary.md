# Glossary: Late game

This page covers the terms from the story lines that run behind the war (spoilers). Earlier terms are in the [basics glossary](../basics/glossary.md), the [Conquest glossary](../conquest/glossary.md) and the [Beyond the Front glossary](../expansion/glossary.md).

Wording is read from the game's translation strings and the decompiled interface code of version 0.8.3.4, not from play. The table columns are explained in the basics glossary. Code names are in the last section.

Pages: [Umbral Reach](umbral-reach.md), [Malcyte and Meridia](malcyte-and-meridia.md).

## Places and stations

| Term used here | What the game shows | Meaning |
|---|---|---|
| war zone (Darkspace) | none | The Darkspace region is not part of the war zone. |
| Automated Outpost | "Automated Outpost" (map location type) | An Umbral Reach station. Not the same as an economic outpost. |
| puppeteers' station | none | The 6-part station in quadrant 2. Not an economic outpost or an outpost system. |
| Meridia (sector) | "There is nothing to besiege here" when you order an attack | The sector that allows no stations. Malcyte hold it without hubs. |
| Meridia (quadrant) | none | While closed, open only to Malcyte, Meridia's Chosen and, outside the Meridia sector, Darkspace Compact. Jump gates into it are closed. |

Collisions in this group:

- "Outpost" is four different things in the game: the system card's "Conquest outpost", the Faction overview's "Economic Outposts" (also what raids target and what you build in your own systems), an "Industrial Outpost" location, and an "Automated Outpost" location. These pages say "outpost system" for the first and "economic outpost" for the second. The other two keep their full names.

In the code: [places and stations](#code-places-and-stations).

## Conquest map

| Term used here | What the game shows | Meaning |
|---|---|---|
| Umbral Reach control | "Umbral Reach control" row | How far the Umbral Reach has spread into a system, as a percentage. |
| conquest update (Umbral spread) | "Conquest Update" card | The hourly conquest update also spreads the Umbral Reach. |
| Umbral Hacking Tool | none | Starts Umbral Reach infection in a system by setting its control to 0.05 (0.7 for the "uber" variant). |
| Umbral transponder | none | Skips several reputation costs for entering, boarding, killing parts and destroying stations. The list is on [Umbral Reach](umbral-reach.md#what-the-umbral-transponder-changes). |

In the code: [conquest map](#code-conquest-map).

## Factions

| Term used here | What the game shows | Meaning |
|---|---|---|
| Malcyte | none | The dormant, yardless raider faction. A hard-boarding faction. See [Malcyte and Meridia](malcyte-and-meridia.md). |

In the code: [factions](#code-factions).

## Collisions in one list

- Outpost: outpost system, economic outpost, Industrial Outpost, Automated Outpost.

## In the game code

This part maps the terms above to their names in the decompiled source of version 0.8.3.4. The translation strings are in `resources.assets`.

### Code: Places and stations

| Term used here | Code name |
|---|---|
| war zone (Darkspace) | `ConquestScope.IsWarZone` |
| Automated Outpost | `MapPOIUmbralStation` |
| puppeteers' station | `CombatStationFactory.CreateUmbralOutpost` (the code builder name is "Umbral outpost") |
| Meridia (sector) | `ConquestScope.AllowsStations` |
| Meridia (quadrant) | `ConquestScope.IsClosedMeridiaGate`, `ConquestScope.IsOpenTo` |

### Code: Conquest map

| Term used here | Code name |
|---|---|
| Umbral Reach control | `umbralControlLevel` |
| conquest update (Umbral spread) | `Conquest.DoConquestTick` |
| Umbral Hacking Tool | `UmbralHackingTool`, `UmbralHackingBot` |
| Umbral transponder | `hasUmbralTransponder` |

### Code: Factions

| Term used here | Code name |
|---|---|
| Malcyte | `amalgam` (dormant until `Faction.MalcyteAwake`) |
