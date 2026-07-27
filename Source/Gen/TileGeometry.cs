// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Worldsmith.Gen
{
	/// <summary>
	/// Shared per-tile geometry helpers for generation passes. Single-threaded
	/// generation only: the neighbour buffer is reused between calls.
	/// </summary>
	public static class TileGeometry
	{
		private static readonly List<PlanetTile> neighborBuffer = new List<PlanetTile>();

		/// <summary>
		/// Returns the neighbour lying furthest upwind of the tile (the one whose
		/// direction is most opposed to the wind flow), or -1 if there is none.
		/// </summary>
		public static int UpwindNeighbor(PlanetLayer layer, int tileId, Vector3 center, Vector3 wind)
		{
			layer.GetTileNeighbors(tileId, neighborBuffer);
			int tileCount = layer.Tiles.Count;
			float bestDot = 0f;
			int upwind = -1;
			for (int i = 0; i < neighborBuffer.Count; i++)
			{
				int nid = neighborBuffer[i].tileId;
				if (nid < 0 || nid >= tileCount)
				{
					continue;
				}
				Vector3 dir = (layer.GetTileCenter(nid) - center).normalized;
				float dot = Vector3.Dot(dir, wind);
				if (dot < bestDot)
				{
					bestDot = dot;
					upwind = nid;
				}
			}
			return upwind;
		}
	}
}
