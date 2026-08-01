// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Gen
{
	/// <summary>
	/// For every land tile, the neighbours the wind arrives from and how much of the
	/// arriving air each one accounts for. Gathering from the whole upwind arc rather
	/// than a single neighbour lets anything carried on the wind travel as a broad
	/// front instead of a thin streamline, while still stretching downwind.
	///
	/// Built once per world and shared, because working out the arcs means walking
	/// every tile's neighbours and is the expensive half of any pass that uses it.
	/// Ocean tiles are left empty on purpose: every pass that carries something on the
	/// wind treats the sea as a source rather than something to gather from.
	/// </summary>
	public sealed class UpwindGraph
	{
		/// <summary>Upwind neighbour ids per tile, or null where there are none.</summary>
		public readonly int[][] Sources;

		/// <summary>Share of the arriving air per source, summing to 1.</summary>
		public readonly float[][] Weights;

		private UpwindGraph(int[][] sources, float[][] weights)
		{
			Sources = sources;
			Weights = weights;
		}

		public static UpwindGraph Build(PlanetLayer layer)
		{
			var tiles = layer.Tiles;
			int count = tiles.Count;
			var sources = new int[count][];
			var weights = new float[count][];

			var neighbors = new List<PlanetTile>();
			var ids = new List<int>(6);
			var shares = new List<float>(6);

			for (int i = 0; i < count; i++)
			{
				if (tiles[i].elevation <= 0f)
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
				shares.Clear();
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
						shares.Add(upwindness);
						total += upwindness;
					}
				}

				if (total <= 0f)
				{
					continue;
				}

				float[] w = new float[shares.Count];
				float inv = 1f / total;
				for (int k = 0; k < shares.Count; k++)
				{
					w[k] = shares[k] * inv;
				}
				sources[i] = ids.ToArray();
				weights[i] = w;
			}

			return new UpwindGraph(sources, weights);
		}
	}
}
