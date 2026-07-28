// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Planetsmith.Gen.Passes
{
	/// <summary>
	/// Traces where the rain goes once it lands. Water runs downhill, so each patch of
	/// ground sheds its rainfall onto the lowest neighbour it touches, and that neighbour
	/// passes on everything it received along with its own. Work down the slopes from the
	/// peaks and the totals build: ridges carry almost nothing, valley floors gather
	/// whole watersheds, and the places rivers would run stand out on their own.
	///
	/// Tiles ringed entirely by higher ground keep what reaches them, which is the right
	/// answer rather than an oversight: that is how a basin with no outlet to the sea
	/// works, and it is where salt flats and inland lakes belong.
	///
	/// The totals span such a range that they are stored on a logarithmic scale, so a
	/// mere damp hollow can still be told apart from a major river's floodplain.
	/// </summary>
	public sealed class DrainagePass : IGenPass
	{
		public string Name => "Drainage";

		// Accumulated flow (in mm of gathered rainfall) treated as a full watercourse.
		private const float FullFlow = 400000f;

		private static readonly List<PlanetTile> neighbors = new List<PlanetTile>();

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			int count = tiles.Count;

			int[] downhill = new int[count];
			float[] flow = new float[count];
			var landTiles = new List<int>(count);

			for (int i = 0; i < count; i++)
			{
				downhill[i] = -1;
				if (tiles[i].elevation <= 0f)
				{
					continue;
				}
				landTiles.Add(i);
				// Each tile starts with the water that fell on it.
				flow[i] = Mathf.Max(0f, tiles[i].rainfall);
				downhill[i] = LowestNeighbor(layer, tiles, i, count);
			}

			// Settle the high ground first so every tile has already collected from
			// upstream by the time it hands its own total onwards.
			int[] order = landTiles.ToArray();
			Array.Sort(order, (a, b) => tiles[b].elevation.CompareTo(tiles[a].elevation));

			for (int k = 0; k < order.Length; k++)
			{
				int tile = order[k];
				int next = downhill[tile];
				if (next >= 0 && tiles[next].elevation > 0f)
				{
					flow[next] += flow[tile];
				}
			}

			float logFull = Mathf.Log(FullFlow + 1f);
			for (int i = 0; i < count; i++)
			{
				if (tiles[i].elevation <= 0f)
				{
					continue;
				}
				ctx.FlowAccumulation[i] = Mathf.Clamp01(Mathf.Log(flow[i] + 1f) / logFull);
			}
		}

		/// <summary>
		/// The neighbour water would run to, or -1 when every neighbour stands higher.
		/// Ocean counts, so coastal tiles drain to the sea rather than pooling.
		/// </summary>
		private static int LowestNeighbor(PlanetLayer layer, List<Tile> tiles, int tileId, int count)
		{
			layer.GetTileNeighbors(tileId, neighbors);
			float lowest = tiles[tileId].elevation;
			int result = -1;
			for (int k = 0; k < neighbors.Count; k++)
			{
				int nid = neighbors[k].tileId;
				if (nid < 0 || nid >= count)
				{
					continue;
				}
				float elevation = tiles[nid].elevation;
				if (elevation < lowest)
				{
					lowest = elevation;
					result = nid;
				}
			}
			return result;
		}
	}
}
