// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;

namespace Worldsmith.Gen.Passes
{
	/// <summary>
	/// Assigns each tile an annual mean temperature and rainfall from a compact
	/// latitude + elevation model. This is deliberately a first approximation:
	/// dedicated wind, orographic, and monsoon passes will refine it later.
	/// </summary>
	public sealed class ClimatePass : IGenPass
	{
		public string Name => "Climate";

		// Environmental lapse: temperature falls with elevation above a threshold,
		// at roughly 6 deg C per kilometre, capped so peaks don't run away.
		private const float LapseStartAltitude = 250f;
		private const float LapseRatePerMetre = 0.006f;
		private const float MaxLapseReduction = 45f;

		// Latitude of, and spread around, each rainfall band (degrees).
		private const float BandSpread = 17f;
		private const float SubtropicalDryLatitude = 25f;
		private const float MidLatitudeWetLatitude = 50f;

		// Strength of the coherent noise that dapples the otherwise-smooth bands.
		private const float TemperatureNoiseAmplitude = 4f; // deg C
		private const float RainfallNoiseAmplitude = 0.35f; // fraction of local rainfall

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			for (int i = 0; i < tiles.Count; i++)
			{
				Tile tile = tiles[i];
				float lat = layer.LongLatOf(i).y;
				Vector3 center = layer.GetTileCenter(i);

				float tempOffset = ctx.TemperatureNoise.GetValue(center) * TemperatureNoiseAmplitude;
				float rainFactor = 1f + Mathf.Clamp(ctx.RainfallNoise.GetValue(center), -1f, 1f) * RainfallNoiseAmplitude;

				tile.temperature = Temperature(ctx, lat, tile.elevation) + tempOffset;
				tile.rainfall = Mathf.Max(0f, Rainfall(ctx, lat, tile.elevation) * rainFactor);
			}
		}

		private static float Temperature(GenContext ctx, float lat, float elevation)
		{
			float latRad = Mathf.Abs(lat) * Mathf.Deg2Rad;
			// 1 at the equator, 0 at the poles, with a slightly widened tropical belt.
			float warmth = Mathf.Pow(Mathf.Clamp01(Mathf.Cos(latRad)), 0.75f);
			float baseTemp = Mathf.Lerp(ctx.PoleMeanTemp, ctx.EquatorMeanTemp, warmth);

			float lapse = 0f;
			if (elevation > LapseStartAltitude)
			{
				lapse = Mathf.Min(MaxLapseReduction, (elevation - LapseStartAltitude) * LapseRatePerMetre);
			}
			return baseTemp - lapse;
		}

		private static float Rainfall(GenContext ctx, float lat, float elevation)
		{
			float a = Mathf.Abs(lat);
			// Three climatological bands: the equatorial convergence zone (wet), the
			// subtropical high (dry), and the mid-latitude storm track (wet).
			float equatorial = 2600f * Gaussian(a, 0f, BandSpread);
			float subtropicalDry = 900f * Gaussian(a, SubtropicalDryLatitude, BandSpread);
			float midLatitude = 1000f * Gaussian(a, MidLatitudeWetLatitude, BandSpread);
			float rain = 400f + equatorial - subtropicalDry + midLatitude;

			rain *= ctx.RainfallMultiplier;

			// Crude high-elevation drying, a stand-in until orographic rain shadows land.
			if (elevation > 1000f)
			{
				rain *= Mathf.Lerp(1f, 0.5f, Mathf.Clamp01((elevation - 1000f) / 3000f));
			}
			return rain;
		}

		private static float Gaussian(float x, float mean, float sigma)
		{
			float d = (x - mean) / sigma;
			return Mathf.Exp(-d * d);
		}
	}
}
