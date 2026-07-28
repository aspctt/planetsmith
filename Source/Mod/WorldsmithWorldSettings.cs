// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using Verse;

namespace Worldsmith
{
	/// <summary>
	/// The generation parameters belonging to one particular planet, as opposed to the
	/// mod-wide preferences that seed them. These travel with the world: they are chosen
	/// on the world creation page, used while generating, and saved into the game.
	/// </summary>
	public class WorldsmithWorldSettings : IExposable
	{
		public bool enableSeaLevelControl;
		public float targetLandFraction;
		public float axialTilt;

		/// <summary>
		/// Per-biome adjustments, keyed by defName. Only biomes the player has actually
		/// touched are stored; everything absent behaves as its worker intends.
		/// </summary>
		private Dictionary<string, BiomeSettings> biomes = new Dictionary<string, BiomeSettings>();

		/// <summary>Settings for a biome, creating a default entry the first time it is asked for.</summary>
		public BiomeSettings ForBiome(string defName)
		{
			if (!biomes.TryGetValue(defName, out BiomeSettings settings))
			{
				settings = new BiomeSettings();
				biomes[defName] = settings;
			}
			return settings;
		}

		/// <summary>Settings for a biome, or null when it has never been adjusted.</summary>
		public BiomeSettings ForBiomeOrNull(string defName)
		{
			return biomes.TryGetValue(defName, out BiomeSettings settings) ? settings : null;
		}

		public void ResetAllBiomes()
		{
			biomes.Clear();
		}

		public bool AnyBiomeAdjusted
		{
			get
			{
				foreach (BiomeSettings settings in biomes.Values)
				{
					if (!settings.IsDefault)
					{
						return true;
					}
				}
				return false;
			}
		}

		public WorldsmithWorldSettings()
		{
			CopyFrom(WorldsmithMod.Settings);
		}

		public void CopyFrom(WorldsmithSettings defaults)
		{
			if (defaults == null)
			{
				enableSeaLevelControl = false;
				targetLandFraction = 0.4f;
				axialTilt = 23.4f;
				return;
			}
			enableSeaLevelControl = defaults.enableSeaLevelControl;
			targetLandFraction = defaults.targetLandFraction;
			axialTilt = defaults.axialTilt;
		}

		public WorldsmithWorldSettings Clone()
		{
			var clone = new WorldsmithWorldSettings
			{
				enableSeaLevelControl = enableSeaLevelControl,
				targetLandFraction = targetLandFraction,
				axialTilt = axialTilt,
			};
			foreach (var pair in biomes)
			{
				clone.biomes[pair.Key] = pair.Value.Clone();
			}
			return clone;
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref enableSeaLevelControl, "enableSeaLevelControl", defaultValue: false);
			Scribe_Values.Look(ref targetLandFraction, "targetLandFraction", 0.4f);
			Scribe_Values.Look(ref axialTilt, "axialTilt", 23.4f);
			Scribe_Collections.Look(ref biomes, "biomes", LookMode.Value, LookMode.Deep);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && biomes == null)
			{
				biomes = new Dictionary<string, BiomeSettings>();
			}
		}
	}

	/// <summary>
	/// Holds the parameters the player is currently editing on the world creation page.
	/// Generation reads these, and the world component copies them so they persist with
	/// the finished planet.
	/// </summary>
	public static class WorldsmithWorldParams
	{
		private static WorldsmithWorldSettings pending;

		/// <summary>Parameters for the world about to be created; seeded from the mod defaults.</summary>
		public static WorldsmithWorldSettings Pending
		{
			get
			{
				if (pending == null)
				{
					pending = new WorldsmithWorldSettings();
				}
				return pending;
			}
			set => pending = value;
		}

		/// <summary>Re-seed the pending parameters from the mod-wide defaults.</summary>
		public static void ResetToDefaults()
		{
			pending = new WorldsmithWorldSettings();
		}

		/// <summary>
		/// The parameters generation should obey: the current world's own settings when
		/// it has them, otherwise whatever is pending for the world being made.
		/// </summary>
		public static WorldsmithWorldSettings Active
		{
			get
			{
				WorldsmithWorldSettings stored = Find.World?.GetComponent<WorldsmithWorldComponent>()?.Settings;
				return stored ?? Pending;
			}
		}
	}
}
