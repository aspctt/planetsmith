// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;

namespace Worldsmith.Gen.Passes
{
	/// <summary>
	/// Derives each tile's seasonal temperature extremes (coldest and warmest of the
	/// year) around its annual mean. The swing grows with latitude (poles have long
	/// dark winters) and with continentality (the sea moderates coasts, so interiors
	/// bake in summer and freeze in winter). These extremes feed the biome scoring
	/// gates; vanilla biome workers only see the annual mean, so this is where
	/// Worldsmith adds the "a warm average with a lethal winter isn't tropical" rule.
	/// </summary>
	public sealed class SeasonalityPass : IGenPass
	{
		public string Name => "Seasonality";

		// Half of the summer-to-winter swing (deg C) at the equator and at the poles.
		private const float EquatorHalfSwing = 3f;
		private const float PoleHalfSwing = 22f;
		// Extra swing in deep interiors relative to the coast.
		private const float ContinentalityBoost = 0.6f;
		// Residual swing on a world with no tilt, from its elliptical orbit alone.
		private const float MinTiltFactor = 0.12f;

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			int count = tiles.Count;
			// Seasons exist because the planet is tilted, so the whole swing scales with
			// it: an upright world has near-constant temperatures year round.
			float tiltScale = Mathf.Max(MinTiltFactor, ctx.TiltFactor) * ctx.Tuning.seasonIntensity;
			for (int i = 0; i < count; i++)
			{
				float latFrac = Mathf.Clamp01(Mathf.Abs(layer.LongLatOf(i).y) / 90f);
				float baseSwing = Mathf.Lerp(EquatorHalfSwing, PoleHalfSwing, latFrac);
				float swing = baseSwing * tiltScale * (1f + ContinentalityBoost * ctx.Continentality[i]);

				float mean = tiles[i].temperature;
				ctx.WinterMinTemp[i] = mean - swing;
				ctx.SummerMaxTemp[i] = mean + swing;
			}
		}
	}
}
