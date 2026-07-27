// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace Worldsmith.Gen
{
	/// <summary>
	/// Shared state threaded through the generation passes for a single layer.
	/// Baselines are derived once, up front, from the world's overall temperature
	/// and rainfall settings so every pass sees the player's chosen climate.
	/// </summary>
	public sealed class GenContext
	{
		public readonly PlanetLayer Layer;
		public readonly int TileCount;
		public readonly int Seed;

		/// <summary>Mean annual temperature (deg C) at the equator, from the world temp setting.</summary>
		public readonly float EquatorMeanTemp;

		/// <summary>Mean annual temperature (deg C) at the poles, from the world temp setting.</summary>
		public readonly float PoleMeanTemp;

		/// <summary>Global rainfall scale from the world rainfall setting (1.0 = Normal).</summary>
		public readonly float RainfallMultiplier;

		/// <summary>Per-tile continentality (0 = maritime, 1 = deep interior). Filled by MoistureAdvectionPass.</summary>
		public readonly float[] Continentality;

		/// <summary>Per-tile coldest-of-year temperature (deg C). Filled by SeasonalityPass.</summary>
		public readonly float[] WinterMinTemp;

		/// <summary>Per-tile warmest-of-year temperature (deg C). Filled by SeasonalityPass.</summary>
		public readonly float[] SummerMaxTemp;

		/// <summary>Low-frequency coherent noise that breaks up the latitude bands. Seeded from the world.</summary>
		public readonly ModuleBase TemperatureNoise;
		public readonly ModuleBase RainfallNoise;

		/// <summary>Planet's axial tilt in degrees.</summary>
		public readonly float AxialTilt;

		/// <summary>Axial tilt relative to Earth's 23.4 degrees. 1 = Earth-like, 0 = no seasons.</summary>
		public readonly float TiltFactor;

		public GenContext(PlanetLayer layer)
		{
			Layer = layer;
			TileCount = layer.Tiles.Count;
			Seed = Find.World.info.Seed;

			Continentality = new float[TileCount];
			WinterMinTemp = new float[TileCount];
			SummerMaxTemp = new float[TileCount];
			// Default to "no seasonal swing" so a tile the seasonality pass never
			// touches cannot accidentally trip a frost gate.
			for (int i = 0; i < TileCount; i++)
			{
				WinterMinTemp[i] = 999f;
				SummerMaxTemp[i] = 999f;
			}

			// Both settings are 7-step enums (index 3 == Normal). Anchor the model to
			// an Earth-like planet at Normal and shift the whole globe per step.
			AxialTilt = Mathf.Clamp(WorldsmithMod.Settings?.axialTilt ?? 23.4f, 0f, 90f);
			TiltFactor = AxialTilt / 23.4f;

			int tempIdx = Mathf.Clamp((int)Find.World.info.overallTemperature, 0, 6);
			EquatorMeanTemp = 30f + (tempIdx - 3) * 7f;
			// A steeper tilt swings the poles through long summers as well as long
			// nights, so over a whole year they collect more sunlight and the
			// equator-to-pole gradient flattens. No tilt leaves them permanently dark.
			PoleMeanTemp = -45f + (tempIdx - 3) * 10f + (AxialTilt - 23.4f) * 0.6f;

			int rainIdx = Mathf.Clamp((int)Find.World.info.overallRainfall, 0, 6);
			RainfallMultiplier = (rainIdx + 1) / 4f;

			// Continent-scale coherent noise, independent seeds so temperature and
			// rainfall patches don't line up. Low frequency = large, gentle patches.
			TemperatureNoise = new Perlin(0.022, 2.0, 0.5, 3, Seed, QualityMode.Medium);
			RainfallNoise = new Perlin(0.028, 2.0, 0.5, 3, Seed ^ 0x51EED, QualityMode.Medium);
		}
	}
}
