// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Planetsmith.Gen.Passes
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
		// The dry belt is narrower than the wet belts either side of it. Air sinks back to
		// the ground over a tighter run of latitudes than the two it rose over, so giving
		// all three the same width spread the drought far past where it belongs: it was
		// still taking half the rain at 35 degrees and a third at 40, latitudes that should
		// be Mediterranean and temperate. That left the most habitable band of the planet
		// as desert, with the deserts themselves no drier for it.
		private const float SubtropicalSpread = 10f;

		// Rain that reaches everywhere, before the belts add their share.
		private const float BaseRainfall = 450f;
		// How much of the rain the subtropical high holds back at its worst.
		private const float SubtropicalSuppression = 0.72f;

		/// <summary>
		/// How much of the equator-to-pole warmth survives at a given fraction of the way
		/// to the pole. Sunlight thins out faster than a plain cosine suggests once past
		/// the tropics, and this follows the profile RimWorld itself uses, which is what
		/// the biome workers were balanced against. A gentler falloff leaves the middle
		/// latitudes several degrees too warm and pushes hot, dry biomes polewards.
		/// </summary>
		private static readonly SimpleCurve LatitudeWarmthCurve = new SimpleCurve
		{
			new CurvePoint(0f, 1f),
			new CurvePoint(0.1f, 0.985f),
			new CurvePoint(0.5f, 0.657f),
			new CurvePoint(1f, 0f),
		};

		// Strength of the coherent noise that dapples the otherwise-smooth bands.
		private const float TemperatureNoiseAmplitude = 4f; // deg C
		private const float RainfallNoiseAmplitude = 0.35f; // fraction of local rainfall

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			double vanillaTotal = 0d;
			int vanillaLand = 0;
			for (int i = 0; i < tiles.Count; i++)
			{
				Tile tile = tiles[i];

				// Read before we overwrite it. This is the only moment vanilla's own
				// rainfall still exists, and it is the yardstick for whether the rivers the
				// game builds afterwards have as much water to work with as they used to.
				if (tile.elevation > 0f)
				{
					vanillaTotal += tile.rainfall;
					vanillaLand++;
				}

				float lat = layer.LongLatOf(i).y;
				Vector3 center = layer.GetTileCenter(i);

				float tempOffset = ctx.TemperatureNoise.GetValue(center) * TemperatureNoiseAmplitude;
				float rainFactor = 1f + Mathf.Clamp(ctx.RainfallNoise.GetValue(center), -1f, 1f) * RainfallNoiseAmplitude;

				tile.temperature = Temperature(ctx, lat, tile.elevation) + tempOffset;
				tile.rainfall = Mathf.Max(0f, Rainfall(ctx, lat, tile.elevation) * rainFactor);
			}

			ctx.VanillaRainfallMean = vanillaLand > 0 ? (float)(vanillaTotal / vanillaLand) : 0f;
		}

		private static float Temperature(GenContext ctx, float lat, float elevation)
		{
			// 1 at the equator, 0 at the poles.
			float warmth = LatitudeWarmthCurve.Evaluate(Mathf.Clamp01(Mathf.Abs(lat) / 90f));
			float baseTemp = Mathf.Lerp(ctx.PoleMeanTemp, ctx.EquatorMeanTemp, warmth);

			// Where another mod works out what the tilt does to the sunlight a latitude
			// receives, take its answer here rather than ours. Folding it in this early
			// matters: everything after this reads the temperature we write, so the
			// seasons, the frost gates and the biomes all end up describing the same
			// planet the player will actually be living on.
			if (ctx.TiltHandledExternally)
			{
				baseTemp += Compat.ModCompat.AnnualTemperatureCorrection(lat);
			}

			float lapse = 0f;
			if (elevation > LapseStartAltitude)
			{
				lapse = Mathf.Min(MaxLapseReduction, (elevation - LapseStartAltitude) * LapseRatePerMetre * ctx.Tuning.elevationCooling);
			}
			return baseTemp - lapse + ctx.Tuning.temperatureOffset;
		}

		private static float Rainfall(GenContext ctx, float lat, float elevation)
		{
			float a = Mathf.Abs(lat);
			// The two wet belts every planet has: rain rising off the equator, and the
			// storm track of the middle latitudes.
			float equatorial = 2600f * Gaussian(a, 0f, BandSpread);
			float midLatitude = 1000f * Gaussian(a, MidLatitudeWetLatitude, BandSpread);
			float rain = BaseRainfall + equatorial + midLatitude;

			// Between them sits the subtropical high, where descending air suppresses
			// rain. It has to hold rainfall down rather than subtract from it: taking a
			// fixed amount away drove the whole belt to nothing at all, and nothing is a
			// floor that mountains, coasts and every tuning dial multiply straight back
			// into nothing. A desert should be dry, not empty.
			rain *= 1f - SubtropicalSuppression * Gaussian(a, SubtropicalDryLatitude, SubtropicalSpread);

			rain *= ctx.RainfallMultiplier * ctx.Tuning.rainfallScale;

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
