// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Worldsmith.Gen.Passes
{
	/// <summary>
	/// Carries moisture inland from the oceans along the prevailing wind. Ocean tiles
	/// are saturated sources; over land the carried moisture decays with each tile of
	/// fetch, so continental interiors and tiles far downwind of the sea dry out. The
	/// resulting moisture field scales rainfall, adding continentality on top of the
	/// latitude bands (ClimatePass) and orographic effects (OrographyPass).
	///
	/// Each land tile draws moisture from all of its upwind neighbours, weighted by how
	/// directly upwind each one lies. Gathering from the whole upwind arc (rather than a
	/// single neighbour) spreads moisture as a broad front instead of thin streamlines,
	/// while still letting dry zones stretch downwind. Implemented as a wavefront
	/// relaxation: oceans stay saturated, and one tile of fetch resolves per step.
	/// Purely geometric and deterministic.
	/// </summary>
	public sealed class MoistureAdvectionPass : IGenPass
	{
		// Fraction of moisture retained per land tile of fetch.
		private const float LandRetention = 0.92f;
		// Furthest inland fetch resolved, in tiles.
		private const int PropagationSteps = 64;
		// Rainfall multiplier where no ocean moisture reaches (fully dry interior).
		private const float DriestRainfallFactor = 0.1f;

		public string Name => "MoistureAdvection";

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			int count = tiles.Count;

			bool[] isLand = new bool[count];
			int[][] upwindTiles = new int[count][];
			float[][] upwindWeights = new float[count][];
			float[] moisture = new float[count];
			float[] next = new float[count];

			BuildUpwindGraph(layer, tiles, count, isLand, upwindTiles, upwindWeights, moisture);

			for (int step = 0; step < PropagationSteps; step++)
			{
				for (int i = 0; i < count; i++)
				{
					if (!isLand[i])
					{
						next[i] = 1f; // ocean: saturated source
						continue;
					}
					int[] sources = upwindTiles[i];
					if (sources == null)
					{
						next[i] = moisture[i]; // no upwind arc: leave unchanged
						continue;
					}
					float[] weights = upwindWeights[i];
					float acc = 0f;
					for (int k = 0; k < sources.Length; k++)
					{
						acc += weights[k] * moisture[sources[k]];
					}
					next[i] = acc * LandRetention;
				}

				float[] swap = moisture;
				moisture = next;
				next = swap;
			}

			for (int i = 0; i < count; i++)
			{
				if (!isLand[i])
				{
					continue;
				}
				float factor = Mathf.Lerp(DriestRainfallFactor, 1f, Mathf.Clamp01(moisture[i]));
				tiles[i].rainfall = Mathf.Max(0f, tiles[i].rainfall * factor);
			}
		}

		private static void BuildUpwindGraph(PlanetLayer layer, List<Tile> tiles, int count, bool[] isLand, int[][] upwindTiles, float[][] upwindWeights, float[] moisture)
		{
			var neighbors = new List<PlanetTile>();
			var ids = new List<int>(6);
			var weights = new List<float>(6);

			for (int i = 0; i < count; i++)
			{
				bool land = tiles[i].elevation > 0f;
				isLand[i] = land;
				moisture[i] = 1f;
				if (!land)
				{
					continue;
				}

				float lat = layer.LongLatOf(i).y;
				Vector3 center = layer.GetTileCenter(i);
				Vector3 wind = WindModel.PrevailingWind(center, lat);
				if (wind == Vector3.zero)
				{
					continue;
				}

				layer.GetTileNeighbors(i, neighbors);
				ids.Clear();
				weights.Clear();
				float total = 0f;
				for (int k = 0; k < neighbors.Count; k++)
				{
					int nid = neighbors[k].tileId;
					if (nid < 0 || nid >= count)
					{
						continue;
					}
					Vector3 dir = (layer.GetTileCenter(nid) - center).normalized;
					// Positive when the neighbour lies upwind of this tile.
					float upwindness = -Vector3.Dot(dir, wind);
					if (upwindness > 0f)
					{
						ids.Add(nid);
						weights.Add(upwindness);
						total += upwindness;
					}
				}

				if (total <= 0f)
				{
					continue;
				}

				int[] arr = ids.ToArray();
				float[] w = new float[weights.Count];
				float inv = 1f / total;
				for (int k = 0; k < weights.Count; k++)
				{
					w[k] = weights[k] * inv;
				}
				upwindTiles[i] = arr;
				upwindWeights[i] = w;
			}
		}
	}
}
