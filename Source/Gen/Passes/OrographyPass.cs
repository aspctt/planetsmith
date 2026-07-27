// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;

namespace Worldsmith.Gen.Passes
{
	/// <summary>
	/// Terrain-driven rainfall adjustment. Where the prevailing wind climbs into
	/// higher ground the air is forced upward and rains out (windward enhancement);
	/// on the far side, descending air leaves a rain shadow. Approximated per tile
	/// from the elevation difference to its upwind neighbour, which is enough to put
	/// deserts behind mountain ranges. Runs after the base climate, before biomes.
	/// </summary>
	public sealed class OrographyPass : IGenPass
	{
		public string Name => "Orography";

		// Metres of rise (or drop) to the upwind neighbour for the full effect.
		private const float SlopeScale = 800f;
		private const float WindwardBoost = 0.8f;
		private const float LeewardShadow = 0.4f;

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			int count = tiles.Count;
			for (int i = 0; i < count; i++)
			{
				Tile tile = tiles[i];
				if (tile.elevation <= 0f)
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

				int upwind = TileGeometry.UpwindNeighbor(layer, i, center, wind);
				float upwindElevation = upwind >= 0 ? tiles[upwind].elevation : tile.elevation;
				float rise = tile.elevation - upwindElevation;

				float factor = rise >= 0f
					? 1f + WindwardBoost * Mathf.Clamp01(rise / SlopeScale)
					: 1f - LeewardShadow * Mathf.Clamp01(-rise / SlopeScale);

				tile.rainfall = Mathf.Max(0f, tile.rainfall * factor);
			}
		}
	}
}
