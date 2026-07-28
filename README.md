# Worldsmith
- - -

## Description

Worldsmith is a world-generation overhaul for RimWorld. It replaces vanilla's binary, threshold-based biome placement with a physically-motivated climate simulation and a competitive biome-scoring system, then hands the finished world back to the vanilla pipeline so rivers, lakes, roads, factions, and landmarks all continue to generate normally.

At its core is a chain of climate passes, each building on the last, so that every tile's conditions follow from what came before rather than from a lookup table. The result is planets whose deserts sit in rain shadows, whose forests follow the moisture, and whose ice caps and tropics fall where the physics put them.

Worldsmith is an original, independent, clean-room implementation. It is **not** derived from, affiliated with, or endorsed by any other mod, and it shares no code with any other mod (see [NOTICE](./NOTICE)). Its *feature set* was inspired by the now-abandoned mod Realistic Planets 2; its *code* is entirely new.

## What it simulates

* **Temperature** falls from equator to pole along a curve matched to RimWorld's own, then drops with altitude.
* **Rainfall** arrives in the three great latitude belts: the wet equator, the dry subtropics, and the mid-latitude storm track.
* **Rain shadows** form where prevailing winds climb into high ground, soaking the windward slope and starving the far side.
* **Moisture** is carried inland from the sea by the wind and gives out as it goes, so continental interiors run dry.
* **Ocean currents** temper the coasts: mild and damp where the wind comes off the water, cold and parched where it blows offshore in the subtropics, which is how deserts come to sit on a shoreline.
* **Seasons** swing further from the sea and further from the equator, and their depth is set by the planet's axial tilt.
* **Monsoons** break over tropical coasts with a large landmass behind them, where the land bakes hotter than the sea each summer and drags the ocean air inland.
* **Effective moisture** weighs rainfall against how much of it the heat would evaporate, so a cold dry place reads as damp and a hot wet one can still be arid.
* **Drainage** follows the water downhill from the peaks, gathering whole watersheds into the valleys.
* **Lakes** fill the hollows that rivers run into but never out of.
* **Wetlands** form where water collects, in dips and along floodplains.
* **Biomes** are then chosen competitively, with frost-shy biomes held back from killing winters and dry-country biomes kept off ground that is not truly parched.

## What you can control

* **Per planet, from the world creation page**: sea level and land fraction, axial tilt, and a set of dials over the simulation itself, from global temperature and rainfall through rain shadows, inland moisture, monsoon strength, season intensity and coastal influence.
* **Per biome**: turn one off entirely, change how much ground it takes, or nudge its score. Biomes added by other mods are listed automatically.
* **Presets**: save any of the above under a name and reuse it for every planet afterwards.
* **Map modes**: shade the planet by temperature, rainfall, wetlands, water flow, effective moisture, monsoon, winter temperature, distance from sea, or coastal currents.

## Compatibility

Worldsmith scores biomes through their own workers, so biomes from other mods are included without any patching. It also stands aside where another mod has already decided the matter: it defers to Alien Worlds when one of its planet types is active, and to Worldbuilder presets that carry their own saved terrain. Where a mod covers the same ground as one of Worldsmith's own controls, the world creation dialog says so.

## Installation

To install, place the `Worldsmith` folder inside your RimWorld `Mods` folder (e.g. `.../steamapps/common/RimWorld/Mods/Worldsmith`), then enable it in the in-game mod list below its dependencies.

**REMOVE ANY OLD VERSIONS BEFORE INSTALLING.**

## Dependencies

* [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) (`brrainz.harmony`): must load before Worldsmith.

## Licensing

This work is licensed as follows:

* [GNU General Public License, version 3 or (at your option) any later version](https://www.gnu.org/licenses/gpl-3.0.en.html) (GPLv3-or-later). See [LICENSE](./LICENSE).
	+ You are free to:
		- Use : run the program for any purpose.
		- Study & modify : access the source code and change it to suit your needs.
		- Share : redistribute copies, with or without changes.
	+ Under the following terms:
		- Copyleft : any distributed derivative work must also be licensed under the GPLv3, and its complete corresponding source code must be made available.
		- License and copyright notice : you must retain this license, the copyright notice, and a statement of any changes made.
		- No additional restrictions : you may not impose further legal terms or technological measures that restrict the freedoms this license grants.

Please note the copyrights and trademarks in [NOTICE](./NOTICE).

## Credits

### Core Team

* aspctt: design, programming, project lead

### Dependencies and tools

* Andreas Pardeike (brrainz): Harmony runtime patching library: [github](https://github.com/pardeike/Harmony)

### Acknowledgements

* koth-87: author of Realistic Planets 2, whose feature set inspired Worldsmith's design. Worldsmith is a clean-room reimplementation and contains none of its code.
* Ludeon Studios: for RimWorld and its modding support.
