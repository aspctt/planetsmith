# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Simplified Chinese translation, by ylc395

### Fixed

- Map modes worked out during generation are now kept in the save and come back after loading; games saved with 1.2.2 or older still show them greyed out

## [1.2.2] - 2026-10-17

### Added

- RimWorld 1.7 support, running the same build as 1.6
- Translation support: every menu, tooltip and message now reads its text from a language file
- A biome menu switch that keeps raised offsets to ground the biome would accept, so raised Scarlands stay with their ruins
- The log lists the most common biomes and how much ground is waterlogged in each band of latitude

### Changed

- Equatorial interiors stay wet far from the sea, so rainforest takes more of the equator from temperate forest
- Swamps and bogs form wherever the climate leaves water standing, not only under heavy rain, so cool lowlands get their wetlands and the tropics fewer
- Odyssey's grasslands hold back from the humid tropics, leaving the wettest equatorial ground to rainforest

### Fixed

- The map mode button showing on maps in orbit, where the planet is only a backdrop

## [1.2.1] - 2026-08-04

### Changed

- Narrower subtropical dry belt: on the same world the 30 to 40 degree band went from 80% arid to 57%, while the true deserts either side of the tropics are untouched
- Realistic Axial Tilt sets the tilt whenever it is installed, including alongside Worldbuilder
- The climate Planetsmith builds is reported to the log, band by band

### Fixed

- Worldbuilder's biome frequency and offset sliders doing nothing with Planetsmith installed
- A biome turned down in Planetsmith's menu surviving on ground no biome really suits
- A crash with biome mods that ship placeholder biomes without a worker, which left the world with the game's own biomes
- Backing out of world generation reporting errors

## [1.2.0] - 2026-08-04

### Added

- Realistic Axial Tilt support: it owns the planet's tilt and Planetsmith builds the climate around it, except alongside Worldbuilder, where Planetsmith's own tilt slider stays

### Changed

- Textures ship in an asset bundle, for faster loading and lower memory use

### Fixed

- Steeply tilted worlds getting polar ice caps when Realistic Axial Tilt set the tilt

## [1.1.0] - 2026-08-02

### Added

- Rain shadow map mode, showing where the air has been dried out

### Changed

- Rain shadows carry twenty or thirty tiles downwind instead of stopping at the foot of a range, and end at open sea
- Two ranges in a row dry the air further than one
- Slopes are judged against the rises a planet actually has, so a range's two flanks get different weather

## [1.0.1] - 2026-08-01

### Changed

- Stronger rain on the windward side of mountain ranges

### Fixed

- Unbroken desert bands, missing rivers and tuning dials that seemed to do nothing, all caused by the subtropical high driving rainfall to zero
- Tiles drowned by a higher sea level keeping their land biomes, which left forests on the sea floor and cost the world its rivers
- Moving the sea level dragging the sea floor thousands of metres deeper than anything the game makes
- The biome list crashing on the world creation page with some mod lists

## [1.0.0] - 2026-07-29

First release.

[Unreleased]: https://github.com/aspctt/planetsmith/compare/1.2.2...HEAD
[1.2.2]: https://github.com/aspctt/planetsmith/compare/1.2.1...1.2.2
[1.2.1]: https://github.com/aspctt/planetsmith/compare/1.2.0...1.2.1
[1.2.0]: https://github.com/aspctt/planetsmith/compare/1.1.0...1.2.0
[1.1.0]: https://github.com/aspctt/planetsmith/compare/1.0.1...1.1.0
[1.0.1]: https://github.com/aspctt/planetsmith/compare/1.0.0...1.0.1
[1.0.0]: https://github.com/aspctt/planetsmith/releases/tag/1.0.0
