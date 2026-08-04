// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace Planetsmith.Gen
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

		/// <summary>Per-tile continentality (0 = maritime, 1 = deep interior). Filled by ContinentalityPass.</summary>
		public readonly float[] Continentality;

		/// <summary>Per-tile distance in tiles to the nearest ocean (0 for ocean itself). Filled by ContinentalityPass.</summary>
		public readonly int[] CoastDistance;

		/// <summary>
		/// Per-tile share of ocean-sourced moisture still carried by the wind (1 at sea,
		/// falling with each land tile crossed). A high value means the air reaching this
		/// tile came off the water recently, so it doubles as a smooth, direction-aware
		/// measure of onshore versus offshore wind. Filled by MoistureAdvectionPass.
		/// </summary>
		public readonly float[] OceanicMoisture;

		/// <summary>
		/// Per-tile share of the landscape's runoff passing through, on a logarithmic
		/// scale: near 0 on ridges, near 1 where a major river would run. Filled by
		/// DrainagePass.
		/// </summary>
		public readonly float[] FlowAccumulation;

		/// <summary>
		/// Per-tile share of the moisture the arriving air has already left on high
		/// ground upwind: 0 where nothing has been taken from it, 1 where it has been
		/// wrung out entirely. Filled by OrographyPass.
		/// </summary>
		public readonly float[] RainShadow;

		/// <summary>
		/// Per-tile monsoon strength, 0 where the seasonal rains never reach and 1 where
		/// they are at their fiercest. Filled by MonsoonPass.
		/// </summary>
		public readonly float[] MonsoonStrength;

		/// <summary>
		/// Per-tile effective moisture: rainfall divided by how much this climate could
		/// evaporate. Below 0.2 is arid, 0.65 and above humid. Filled by AridityPass.
		/// </summary>
		public readonly float[] AridityIndex;

		/// <summary>
		/// Signed strength of the coastal ocean-current effect: positive where onshore
		/// wind brings mild damp maritime air, negative over cold subtropical upwelling,
		/// zero where no current anomaly applies. Filled by OceanCurrentPass.
		/// </summary>
		public readonly float[] CoastalAnomaly;

		/// <summary>Per-tile coldest-of-year temperature (deg C). Filled by SeasonalityPass.</summary>
		public readonly float[] WinterMinTemp;

		/// <summary>Per-tile warmest-of-year temperature (deg C). Filled by SeasonalityPass.</summary>
		public readonly float[] SummerMaxTemp;

		/// <summary>Low-frequency coherent noise that breaks up the latitude bands. Seeded from the world.</summary>
		public readonly ModuleBase TemperatureNoise;
		public readonly ModuleBase RainfallNoise;

		/// <summary>Player adjustments to the climate model for this planet.</summary>
		public readonly ClimateTuning Tuning;

		/// <summary>Planet's axial tilt in degrees.</summary>
		public readonly float AxialTilt;

		/// <summary>Axial tilt relative to Earth's 23.4 degrees. 1 = Earth-like, 0 = no seasons.</summary>
		public readonly float TiltFactor;

		/// <summary>
		/// True when another mod owns what tilt does to temperature, in which case our own
		/// version of it stands down and its figures are used instead. The tilt still
		/// shapes our seasons and biomes; it is simply somebody else's number now.
		/// </summary>
		public readonly bool TiltHandledExternally;

		/// <summary>
		/// Mean rainfall vanilla had given the land before Planetsmith replaced it. Kept
		/// because the game's own river step sizes rivers by the rainfall running through
		/// them, so what we hand it decides whether a planet gets rivers or trickles.
		/// </summary>
		public float VanillaRainfallMean;

		private UpwindGraph upwind;

		/// <summary>
		/// Where the wind arrives from at every land tile. Worked out on first use and
		/// shared from there on, since more than one pass carries something downwind and
		/// walking the neighbours is the expensive part of doing so.
		/// </summary>
		public UpwindGraph Upwind => upwind ?? (upwind = UpwindGraph.Build(Layer));

		public GenContext(PlanetLayer layer)
		{
			Layer = layer;
			TileCount = layer.Tiles.Count;
			Seed = Find.World.info.Seed;

			Continentality = new float[TileCount];
			CoastDistance = new int[TileCount];
			CoastalAnomaly = new float[TileCount];
			OceanicMoisture = new float[TileCount];
			AridityIndex = new float[TileCount];
			MonsoonStrength = new float[TileCount];
			FlowAccumulation = new float[TileCount];
			RainShadow = new float[TileCount];
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
			PlanetsmithWorldSettings world = PlanetsmithWorldParams.Active;
			Tuning = world?.tuning ?? new ClimateTuning();
			TiltHandledExternally = Compat.ModCompat.AxialTiltHandledExternally();
			AxialTilt = Mathf.Clamp(
				TiltHandledExternally
					? Compat.ModCompat.ExternalAxialTiltDegrees(world?.axialTilt ?? 23.4f)
					: world?.axialTilt ?? 23.4f,
				0f,
				90f);
			TiltFactor = AxialTilt / 23.4f;

			int tempIdx = Mathf.Clamp((int)Find.World.info.overallTemperature, 0, 6);
			EquatorMeanTemp = 30f + (tempIdx - 3) * 7f;
			// A steeper tilt swings the poles through long summers as well as long
			// nights, so over a whole year they collect more sunlight and the
			// equator-to-pole gradient flattens. No tilt leaves them permanently dark.
			// This is a single term standing in for the whole business, so where a mod
			// works the sunlight out properly we drop it and take theirs per latitude
			// instead, rather than letting both bend the same gradient.
			PoleMeanTemp = -37f + (tempIdx - 3) * 10f
				+ (TiltHandledExternally ? 0f : (AxialTilt - 23.4f) * 0.6f);

			int rainIdx = Mathf.Clamp((int)Find.World.info.overallRainfall, 0, 6);
			RainfallMultiplier = (rainIdx + 1) / 4f;

			// Continent-scale coherent noise, independent seeds so temperature and
			// rainfall patches don't line up. Low frequency = large, gentle patches.
			TemperatureNoise = new Perlin(0.022, 2.0, 0.5, 3, Seed, QualityMode.Medium);
			RainfallNoise = new Perlin(0.028, 2.0, 0.5, 3, Seed ^ 0x51EED, QualityMode.Medium);
		}
	}
}
