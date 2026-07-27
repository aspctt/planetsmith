// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Worldsmith.Gen.Passes
{
	/// <summary>
	/// Continentality as distance from the sea: a multi-source flood fill outward from
	/// every ocean tile gives each land tile its graph distance to the nearest coast,
	/// which maps to 0 (maritime) at the shore up to 1 (fully continental) deep inland.
	/// This is isotropic on purpose. Maritime influence comes from being next to water,
	/// not from the wind, so every coast fades smoothly regardless of wind direction.
	/// (The one-directional downwind model stays where it belongs, in rainfall.) The
	/// result feeds the seasonality swing.
	/// </summary>
	public sealed class ContinentalityPass : IGenPass
	{
		public string Name => "Continentality";

		// Tiles inland at which a location counts as fully continental.
		private const float DistanceScale = 20f;

		private static readonly List<PlanetTile> neighbors = new List<PlanetTile>();

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			int count = tiles.Count;

			int[] distance = new int[count];
			for (int i = 0; i < count; i++)
			{
				distance[i] = -1;
			}

			var frontier = new Queue<int>();
			for (int i = 0; i < count; i++)
			{
				if (tiles[i].elevation <= 0f)
				{
					distance[i] = 0;
					frontier.Enqueue(i);
				}
			}

			while (frontier.Count > 0)
			{
				int current = frontier.Dequeue();
				layer.GetTileNeighbors(current, neighbors);
				for (int k = 0; k < neighbors.Count; k++)
				{
					int nid = neighbors[k].tileId;
					if (nid < 0 || nid >= count || distance[nid] != -1)
					{
						continue;
					}
					distance[nid] = distance[current] + 1;
					frontier.Enqueue(nid);
				}
			}

			for (int i = 0; i < count; i++)
			{
				if (tiles[i].elevation <= 0f)
				{
					ctx.Continentality[i] = 0f;
					continue;
				}
				// distance < 0 would mean a landmass with no sea path at all; treat as fully continental.
				int d = distance[i] < 0 ? (int)DistanceScale : distance[i];
				ctx.Continentality[i] = Mathf.Clamp01(d / DistanceScale);
			}
		}
	}
}
