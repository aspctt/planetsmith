// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Gen.Passes
{
	/// <summary>
	/// Carries moisture inland from the oceans along the prevailing wind. Ocean tiles
	/// are saturated sources; over land the carried moisture decays with each tile of
	/// fetch, so continental interiors and tiles far downwind of the sea dry out. The
	/// resulting moisture field scales rainfall, adding continentality on top of the
	/// latitude bands (ClimatePass) and orographic effects (OrographyPass).
	///
	/// Each land tile draws moisture from the whole arc the wind arrives through (see
	/// <see cref="UpwindGraph"/>). Implemented as a wavefront relaxation: oceans stay
	/// saturated, and one tile of fetch resolves per step. Purely geometric and
	/// deterministic.
	/// </summary>
	public sealed class MoistureAdvectionPass : IGenPass
	{
		// Fraction of moisture retained per land tile of fetch.
		private const float LandRetention = 0.94f;
		// Furthest inland fetch resolved, in tiles.
		private const int PropagationSteps = 64;
		// Rainfall multiplier where no ocean moisture reaches (fully dry interior).
		private const float DriestRainfallFactor = 0.45f;

		public string Name => "MoistureAdvection";

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			int count = tiles.Count;

			UpwindGraph graph = ctx.Upwind;
			bool[] isLand = new bool[count];
			float[] moisture = new float[count];
			float[] next = new float[count];
			for (int i = 0; i < count; i++)
			{
				isLand[i] = tiles[i].elevation > 0f;
				moisture[i] = 1f;
			}

			float retention = Mathf.Clamp(1f - (1f - LandRetention) / Mathf.Max(0.01f, ctx.Tuning.moistureReach), 0.5f, 0.995f);
			for (int step = 0; step < PropagationSteps; step++)
			{
				for (int i = 0; i < count; i++)
				{
					if (!isLand[i])
					{
						next[i] = 1f; // ocean: saturated source
						continue;
					}
					int[] sources = graph.Sources[i];
					if (sources == null)
					{
						next[i] = moisture[i]; // no upwind arc: leave unchanged
						continue;
					}
					float[] weights = graph.Weights[i];
					float acc = 0f;
					for (int k = 0; k < sources.Length; k++)
					{
						acc += weights[k] * moisture[sources[k]];
					}
					next[i] = acc * retention;
				}

				float[] swap = moisture;
				moisture = next;
				next = swap;
			}

			for (int i = 0; i < count; i++)
			{
				// Share the settled field: it doubles as an onshore/offshore wind measure
				// for the coastal passes, and it is far smoother than tracing a single
				// upwind path across a hex grid.
				ctx.OceanicMoisture[i] = Mathf.Clamp01(moisture[i]);
				if (!isLand[i])
				{
					continue;
				}
				float factor = Mathf.Lerp(DriestRainfallFactor, 1f, Mathf.Clamp01(moisture[i]));
				tiles[i].rainfall = Mathf.Max(0f, tiles[i].rainfall * factor);
			}
		}
	}
}
