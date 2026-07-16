# Worldsmith
- - -

## Description

Worldsmith is a world-generation overhaul for RimWorld. It replaces vanilla's binary, threshold-based biome placement with a physically-motivated climate simulation and a competitive biome-scoring system, then hands the finished world back to the vanilla pipeline so rivers, lakes, roads, factions, and landmarks all continue to generate normally.

At its core is a dependency-ordered climate model. Geography, temperature, seasonality, wind, moisture transport, orographic (mountain) effects, monsoons, rainfall, and aridity are computed in sequence so that each tile's conditions follow from the ones before it. Every biome then scores each tile on temperature, moisture, and elevation, and the best-fitting biome wins. The result is planets whose deserts sit in rain shadows, whose forests follow the moisture, and whose ice caps and tropics fall where the physics put them rather than where a lookup table does.

Worldsmith also exposes per-world generation controls (sea level, axial tilt, global temperature and rainfall, land/ocean coverage) with a preset system for saving and reusing world configurations.

Worldsmith is an original, independent, clean-room implementation. It is **not** derived from, affiliated with, or endorsed by any other mod, and it shares no code with any other mod (see [NOTICE](./NOTICE)). Its *feature set* was inspired by the now-abandoned mod Realistic Planets 2; its *code* is entirely new.

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
