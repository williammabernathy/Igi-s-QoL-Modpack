# Glossary: Late Game

This page covers the terms from the story lines that run behind the war (spoilers). Earlier terms are in the [basics glossary](../basics/glossary.md), the [Conquest glossary](../conquest/glossary.md) and the [Beyond the Front glossary](../expansion/glossary.md).

Wording is read from the game's translation strings and the decompiled interface code of version 0.8.3.4, not from play. The interface code was compared with the 0.8.3.5 source. The translation strings were not re-read for 0.8.3.5. The table columns are explained in the basics glossary. The code names are in the last section.

Pages: [The control story line](control-story.md), [The closed sector story line](closed-sector-story.md).

## Places and Stations

| Term used here | What the game shows | Meaning |
|---|---|---|
| war zone (Darkspace) | none | The Darkspace region is not part of the war zone. |
| Automated Outpost | "Automated Outpost" (map location type) | An Umbral Reach station. Not the same as an economic outpost. |
| puppeteers' station | none; its faction is named "Your Employer" at first and "Umbral Reach" after the name change | The 6-part station in quadrant 2. It is not an economic outpost or an outpost system. |
| Meridia (sector) | "There is nothing to besiege here" when you order an attack once the quadrant is open to you | The sector that allows no stations. Malcyte hold it without hubs, so fleets cannot attack it. While the quadrant is closed, the "closed" refusal below comes first. |
| Meridia (quadrant) | "This system is closed to you for now." when you claim a system or order a fleet or raid there | While closed, it is open only to Malcyte, Meridia's Chosen and, outside the Meridia sector, the Darkspace Compact. You are refused. The game also treats jump gates into it as closed. |

Terms in this group that are easily confused:

- "Outpost" is four different things in the game: the system card's "Conquest outpost", the Faction overview's "Economic Outposts" (also what raids target and what you build in your own systems), an "Industrial Outpost" location, and an "Automated Outpost" location. These pages say "outpost system" for the first and "economic outpost" for the second. The other two keep their full names.

In the code: [Places and Stations](#code-places-and-stations).

## Conquest Map

| Term used here | What the game shows | Meaning |
|---|---|---|
| Umbral Reach control | "Umbral Reach control" row | How far the Umbral Reach has spread into a system, as a percentage. |
| conquest update (Umbral spread) | "Conquest Update" card | The hourly conquest update also spreads the Umbral Reach. It makes one 50% roll per update and adds at most one new system. |
| Umbral Hacking Tool | "Umbral Hacking Tool" (item) | Starts the Umbral Reach spread in a system by setting its control to 0.05. The stronger variant, the "Umbral Hacking Multitool" (called "uber" in the code), sets it to 0.7. You must use either at the system's hub, be friendly with the owner, and the system must have no control yet. Unless you use the Multitool, a neighbouring hub must have control above 0.19 (the game's message says "at least 20%"). |
| Umbral transponder | "Decoy Transponder" (item) | Prevents reputation loss and gains from combat until you next dock. It skips the reputation cost of entering a facility, boarding a ship, every kill (station parts and ships), and destroying a faction station or yard. The full list is on [the control story line](control-story.md#spoiler-what-the-transponder-changes). |

In the code: [Conquest Map](#code-conquest-map).

## Factions

| Term used here | What the game shows | Meaning |
|---|---|---|
| Malcyte | "Malcyte" (faction name) | The dormant raider faction with no yards. While dormant, it earns no taxes, fields no fleets and has a fleet cap of 0. It is one of the factions that are harder to board. See [the closed sector story line](closed-sector-story.md). |

In the code: [Factions](#code-factions).

## Easily Confused Terms

- Outpost: outpost system, economic outpost, Industrial Outpost, Automated Outpost.

## In the Game Code

This part maps the terms above to their names in the decompiled source of version 0.8.3.4, compared with the 0.8.3.5 source. The translation strings are in `resources.assets`.

### Code: Places and Stations

| Term used here | Code name |
|---|---|
| war zone (Darkspace) | `ConquestScope.IsWarZone` |
| Automated Outpost | `EmbassyStation.typeName` for the puppeteers faction (text key `MapPOIUmbralStation`) |
| puppeteers' station | `CombatStationFactory.CreateUmbralOutpost` (the code builder name is "Umbral outpost") |
| Meridia (sector) | `ConquestScope.AllowsStations` |
| Meridia (quadrant) | `ConquestScope.IsClosedMeridiaGate`, `ConquestScope.IsOpenTo` |

### Code: Conquest Map

| Term used here | Code name |
|---|---|
| Umbral Reach control | `umbralControlLevel` |
| conquest update (Umbral spread) | `Conquest.DoConquestTick` |
| Umbral Hacking Tool | `UmbralHackingTool`, `UmbralHackingBot` (the Umbral Hacking Multitool is item `UmbralUberTool`, which uses the same `UmbralHackingTool` class with `isUberVariant` set) |
| Umbral transponder | `hasUmbralTransponder`, `UmbralTransponderItem` (the item's translation key is `UmbralTransponder`) |

### Code: Factions

| Term used here | Code name |
|---|---|
| Malcyte | `amalgam` (dormant until `Faction.MalcyteAwake`) |
