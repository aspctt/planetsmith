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
	///  - RealisticAxialTilt: models what a planet's tilt does to daylight, the sun's
	///    path, plants and the seasons, in far more depth than we ever set out to. Where
	///    the two meet is only the tilt itself, so we hand that over rather than run a
	///    second, cruder model of the same thing beside it. See <see cref="AxialTiltHandledExternally"/>.
	/// </summary>
	public static class ModCompat
	{
		private const string AlienWorldsDefaultPlanet = "Default";

		private static bool initialized;
		private static PropertyInfo alienWorldsCurrent;
		private static PropertyInfo worldbuilderCurrentPreset;
		private static FieldInfo worldbuilderSaveTerrain;
		private static PropertyInfo axialTiltDegrees;
		private static PropertyInfo axialGeometryReady;
		private static Func<float, float> annualTemperatureCorrection;

		public static bool AlienWorldsLoaded { get; private set; }
		public static bool EarthLikePlanetLoaded { get; private set; }
		public static bool WorldbuilderLoaded { get; private set; }
		public static bool RealisticAxialTiltLoaded { get; private set; }

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

			BindAxialTilt();
		}

		/// <summary>
		/// Binds the parts of Realistic Axial Tilt we need: the tilt this world was made
		/// with, and how far that tilt moves a latitude's yearly average off an Earth-like
		/// one. The tilt is published on its API; the correction is not yet, so we take the
		/// published name if it appears and otherwise reach for where it currently lives.
		/// Missing either leaves the mod's other work untouched and ours running as normal.
		/// </summary>
		private static void BindAxialTilt()
		{
			Type api = GenTypes.GetTypeInAnyAssembly("RealisticAxialTilt.Api.RealisticAxialTiltApi");
			if (api == null)
			{
				return;
			}
			RealisticAxialTiltLoaded = true;

			axialTiltDegrees = api.GetProperty("AxialTiltDegrees", BindingFlags.Public | BindingFlags.Static);
			axialGeometryReady = api.GetProperty("GeometryReady", BindingFlags.Public | BindingFlags.Static);

			MethodInfo correction =
				api.GetMethod("AnnualTemperatureCorrectionDegrees", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(float) }, null)
				?? GenTypes.GetTypeInAnyAssembly("RealisticAxialTilt.AxialAnnualTemperature")
					?.GetMethod("AnnualTemperatureCorrection", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);

			// A delegate rather than repeated Invoke: this is asked once per tile, and a
			// reflective call per tile would cost more than the whole climate pass.
			annualTemperatureCorrection = correction == null
				? null
				: Delegate.CreateDelegate(typeof(Func<float, float>), correction, false) as Func<float, float>;

			if (annualTemperatureCorrection == null)
			{
				Log.Warning("[Planetsmith] Realistic Axial Tilt is installed but its annual temperature model could not be read, so Planetsmith will keep using its own. Worth reporting: the two will disagree about what tilt does to a planet.");
			}
		}

		/// <summary>
		/// True when Realistic Axial Tilt owns this planet's tilt, so our own slider stands
		/// down in favour of its one. Kept apart from <see cref="AxialTiltHandledExternally"/>
		/// because the settings screens are drawn long before any world exists to be ready.
		/// </summary>
		public static bool AxialTiltOwnedExternally()
		{
			EnsureInit();
			if (!RealisticAxialTiltLoaded || axialTiltDegrees == null || annualTemperatureCorrection == null)
			{
				return false;
			}

			// Worldbuilder replaces the world creation page outright rather than adding to
			// it, which takes that mod's tilt slider down with everything else injected
			// into the original page. Handing it the tilt in that state would leave the
			// planet stuck at whatever the slider was never able to change, so we keep
			// ours. Only the tilt is affected; the rest of both mods carries on.
			return !WorldbuilderLoaded;
		}

		/// <summary>
		/// True when Realistic Axial Tilt is present and ready to say what this world's
		/// tilt does to its temperatures. Planetsmith then stops applying its own version
		/// of that, since the two are the same effect and would otherwise both land.
		/// </summary>
		public static bool AxialTiltHandledExternally()
		{
			if (!AxialTiltOwnedExternally())
			{
				return false;
			}
			try
			{
				// The geometry reads as an upright planet until it is seeded, which would
				// quietly cost every world its seasons. Better to use our own model than a
				// neighbour's uninitialised one.
				return axialGeometryReady == null || (axialGeometryReady.GetValue(null) is bool ready && ready);
			}
			catch (Exception e)
			{
				Log.WarningOnce($"[Planetsmith] Realistic Axial Tilt compatibility probe failed: {e.Message}", 0x5701A3);
				return false;
			}
		}

		/// <summary>This world's tilt in degrees as Realistic Axial Tilt understands it.</summary>
		public static float ExternalAxialTiltDegrees(float fallback)
		{
			try
			{
				return axialTiltDegrees?.GetValue(null) is float tilt ? tilt : fallback;
			}
			catch
			{
				return fallback;
			}
		}

		/// <summary>
		/// Degrees this latitude's yearly average sits away from where an Earth-like tilt
		/// would put it. Zero when nothing is bound, so callers need no second path.
		/// </summary>
		public static float AnnualTemperatureCorrection(float latitudeDeg)
		{
			return annualTemperatureCorrection?.Invoke(latitudeDeg) ?? 0f;
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
