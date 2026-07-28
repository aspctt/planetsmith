// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Worldsmith.Gen.Passes
{
	/// <summary>
	/// Fills the hollows that rivers run into but never out of. Drainage has already
	/// worked out how much water crosses every tile; where a great deal of it arrives at
	/// ground ringed entirely by higher land, it has nowhere left to go and stands there.
	/// That is how the Caspian and the Great Salt Lake came to be, and it is the one
	/// place a large body of water belongs nowhere near a coast.
	///
	/// The hollow is flooded up to the lip of the surrounding land rather than a fixed
	/// depth, so a shallow wide basin becomes a broad lake and a tight one a small tarn.
	/// Because a tile counts as water simply by lying below sea level, dropping these
	/// tiles is enough for the lake and ocean biomes to claim them on their own.
	///
	/// Runs after drainage, which tells it where the water goes, and before the biome
	/// pass, which needs the finished coastline.
	/// </summary>
	public sealed class BasinLakePass : IGenPass
	{
		public string Name => "BasinLakes";

		// Share of the landscape's runoff a hollow must gather before it holds a lake.
		private const float MinInflow = 0.55f;
		// Most tiles one basin may flood, so a vast flat interior cannot become a sea.
		private const int MaxLakeTiles = 60;
		// How far above the deepest point the water may climb. This, rather than the tile
		// cap, is what keeps a shallow dimple small and lets a genuinely deep hollow hold
		// a broad lake.
		private const float MaxBasinRelief = 120f;
		// Metres of water left above the deepest point once the basin has filled.
		private const float LakeDepth = 30f;

		private static readonly List<PlanetTile> neighbors = new List<PlanetTile>();

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			int count = tiles.Count;

			var flooded = new bool[count];
			var basin = new List<int>();
			var frontier = new List<int>();
			int lakes = 0;
			int lakeTiles = 0;

			for (int i = 0; i < count; i++)
			{
				if (flooded[i] || tiles[i].elevation <= 0f)
				{
					continue;
				}
				if (ctx.FlowAccumulation[i] < MinInflow || !IsSink(layer, tiles, i, count))
				{
					continue;
				}

				int filled = FloodBasin(layer, tiles, count, i, flooded, basin, frontier);
				if (filled > 0)
				{
					lakes++;
					lakeTiles += filled;
				}
			}

			if (lakes > 0)
			{
				Log.Message($"[Worldsmith] Filled {lakes} inland basins with water, covering {lakeTiles} tiles.");
			}
		}

		/// <summary>True when no neighbour lies lower, so water arriving here cannot leave.</summary>
		private static bool IsSink(PlanetLayer layer, List<Tile> tiles, int tileId, int count)
		{
			layer.GetTileNeighbors(tileId, neighbors);
			float here = tiles[tileId].elevation;
			for (int k = 0; k < neighbors.Count; k++)
			{
				int nid = neighbors[k].tileId;
				if (nid >= 0 && nid < count && tiles[nid].elevation < here)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Raises the water from the lowest point outwards, always taking the lowest tile
		/// still on the shore, until it would spill over the rim or the basin grows past
		/// what one hollow can plausibly hold.
		/// </summary>
		private static int FloodBasin(PlanetLayer layer, List<Tile> tiles, int count, int sink, bool[] flooded, List<int> basin, List<int> frontier)
		{
			basin.Clear();
			frontier.Clear();

			var inBasin = new HashSet<int> { sink };
			basin.Add(sink);
			AddNeighbors(layer, tiles, count, sink, inBasin, frontier);

			float floor = tiles[sink].elevation;
			float waterLevel = floor;

			while (basin.Count < MaxLakeTiles && frontier.Count > 0)
			{
				// The rim gives way at its lowest point, so that is where the water goes next.
				int bestIndex = 0;
				for (int k = 1; k < frontier.Count; k++)
				{
					if (tiles[frontier[k]].elevation < tiles[frontier[bestIndex]].elevation)
					{
						bestIndex = k;
					}
				}
				int next = frontier[bestIndex];
				frontier.RemoveAt(bestIndex);

				if (tiles[next].elevation <= 0f)
				{
					// The hollow drains to the sea after all; it is no basin.
					return 0;
				}
				if (tiles[next].elevation > floor + MaxBasinRelief)
				{
					// The rim rises out of reach: the water has found its own level.
					break;
				}

				waterLevel = tiles[next].elevation;
				basin.Add(next);
				inBasin.Add(next);
				AddNeighbors(layer, tiles, count, next, inBasin, frontier);
			}

			if (basin.Count < 2)
			{
				return 0; // a single dimple is a puddle, not a lake
			}

			for (int k = 0; k < basin.Count; k++)
			{
				int tile = basin[k];
				flooded[tile] = true;
				tiles[tile].elevation = Mathf.Min(tiles[tile].elevation - LakeDepth, waterLevel - LakeDepth);
				tiles[tile].hilliness = Hilliness.Flat;
			}

			return basin.Count;
		}

		private static void AddNeighbors(PlanetLayer layer, List<Tile> tiles, int count, int tileId, HashSet<int> inBasin, List<int> frontier)
		{
			layer.GetTileNeighbors(tileId, neighbors);
			for (int k = 0; k < neighbors.Count; k++)
			{
				int nid = neighbors[k].tileId;
				if (nid >= 0 && nid < count && !inBasin.Contains(nid) && !frontier.Contains(nid))
				{
					frontier.Add(nid);
				}
			}
		}
	}
}
