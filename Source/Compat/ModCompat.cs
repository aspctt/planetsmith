// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System;
using System.Reflection;
using Verse;

namespace Planetsmith.Compat
{
	/// <summary>
	/// Soft (reflection-only) detection of other mods Planetsmith needs to cooperate
	/// with. No hard assembly references: everything is probed by type name so the
	/// mod loads and runs whether or not these are present.
	///
	/// Notes on the mods present in this environment:
	///  - AlienWorlds: adds planet types with their own climate intent and patches
	///    BiomeWorker.GetScore. When one of its planet types is active we defer
	///    entirely so we don't fight its design; when we do run, its GetScore patch
	///    still applies because our biome pass calls the workers.
	///  - EarthLikePlanet: rebalances elevation and sea level during vanilla terrain
	///    generation. We read the resulting elevation in a postfix, so its work flows
	///    through unchanged. No action needed.
	///  - Worldbuilder: its world presets can carry saved terrain. When one of those is
	///    loaded the planet is meant to come back as it was stored, so we stand aside;
	///    presets that only keep factions or names leave generation to us as usual.
	///  - ReGrowthCore and other biome mods: their biomes are scored through their own
	///    workers by our biome pass automatically.
	/// </summary>
	public static class ModCompat
	{
		private const string AlienWorldsDefaultPlanet = "Default";

		private static bool initialized;
		private static PropertyInfo alienWorldsCurrent;
		private static PropertyInfo worldbuilderCurrentPreset;
		private static FieldInfo worldbuilderSaveTerrain;

		public static bool AlienWorldsLoaded { get; private set; }
		public static bool EarthLikePlanetLoaded { get; private set; }
		public static bool WorldbuilderLoaded { get; private set; }

		public static void EnsureInit()
		{
			if (initialized)
			{
				return;
			}
			initialized = true;

			Type planetTypeManager = GenTypes.GetTypeInAnyAssembly("AlienWorlds.PlanetTypeManager");
			if (planetTypeManager != null)
			{
				AlienWorldsLoaded = true;
				alienWorldsCurrent = planetTypeManager.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
			}

			EarthLikePlanetLoaded = GenTypes.GetTypeInAnyAssembly("EarthLikePlanet.EarthLikePlanetMod") != null;

			Type presetManager = GenTypes.GetTypeInAnyAssembly("Worldbuilder.WorldPresetManager");
			if (presetManager != null)
			{
				WorldbuilderLoaded = true;
				worldbuilderCurrentPreset = presetManager.GetProperty("CurrentlyLoadedPreset", BindingFlags.Public | BindingFlags.Static);
				worldbuilderSaveTerrain = GenTypes.GetTypeInAnyAssembly("Worldbuilder.WorldPreset")
					?.GetField("saveTerrain", BindingFlags.Public | BindingFlags.Instance);
			}
		}

		/// <summary>
		/// True when a Worldbuilder preset that carries its own saved terrain is being
		/// loaded, in which case Planetsmith leaves the planet alone.
		/// </summary>
		public static bool WorldbuilderTerrainPresetActive()
		{
			EnsureInit();
			if (!WorldbuilderLoaded || worldbuilderCurrentPreset == null || worldbuilderSaveTerrain == null)
			{
				return false;
			}
			try
			{
				object preset = worldbuilderCurrentPreset.GetValue(null);
				return preset != null && worldbuilderSaveTerrain.GetValue(preset) is bool saved && saved;
			}
			catch (Exception e)
			{
				Log.WarningOnce($"[Planetsmith] Worldbuilder compatibility probe failed: {e.Message}", 0x5701A2);
				return false;
			}
		}

		/// <summary>
		/// True when AlienWorlds is present and a non-default planet type is active,
		/// in which case Planetsmith defers its climate overhaul to AlienWorlds.
		/// </summary>
		public static bool AlienWorldsCustomPlanetActive()
		{
			EnsureInit();
			if (!AlienWorldsLoaded || alienWorldsCurrent == null)
			{
				return false;
			}
			try
			{
				string current = alienWorldsCurrent.GetValue(null) as string;
				return !string.IsNullOrEmpty(current) && current != AlienWorldsDefaultPlanet;
			}
			catch (Exception e)
			{
				Log.WarningOnce($"[Planetsmith] AlienWorlds compatibility probe failed: {e.Message}", 0x5701A1);
				return false;
			}
		}
	}
}
