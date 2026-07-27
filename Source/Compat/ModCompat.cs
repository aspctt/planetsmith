// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System;
using System.Reflection;
using Verse;

namespace Worldsmith.Compat
{
	/// <summary>
	/// Soft (reflection-only) detection of other mods Worldsmith needs to cooperate
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
	///  - ReGrowthCore and other biome mods: their biomes are scored through their own
	///    workers by our biome pass automatically.
	/// </summary>
	public static class ModCompat
	{
		private const string AlienWorldsDefaultPlanet = "Default";

		private static bool initialized;
		private static PropertyInfo alienWorldsCurrent;

		public static bool AlienWorldsLoaded { get; private set; }
		public static bool EarthLikePlanetLoaded { get; private set; }

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
		}

		/// <summary>
		/// True when AlienWorlds is present and a non-default planet type is active,
		/// in which case Worldsmith defers its climate overhaul to AlienWorlds.
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
				Log.WarningOnce($"[Worldsmith] AlienWorlds compatibility probe failed: {e.Message}", 0x5701A1);
				return false;
			}
		}
	}
}
