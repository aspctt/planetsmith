// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Worldsmith.Gen.Passes
{
	/// <summary>
	/// Drainage-aware swampiness. Wetlands form where water arrives and cannot leave:
	/// wet climates, flat ground, and local basins that sit lower than their
	/// surroundings. Replaces vanilla's noise-driven swampiness, and because the
	/// vanilla swamp and bog workers gate on <c>tile.swampiness</c>, this steers those
	/// biomes toward genuinely waterlogged ground with no per-biome tuning.
	/// </summary>
	public sealed class SwampinessPass : IGenPass
	{
		public string Name => "Swampiness";

		// Rainfall (mm) below which no wetland forms, and where rainfall stops helping.
		private const float RainfallLow = 500f;
		private const float RainfallFull = 1600f;
		// Metres a tile must sit below its neighbours to count as a full basin.
		private const float BasinDepthScale = 150f;
		// Floor on the collection term so flat wet plains still hold some water.
		private const float BasinFloor = 0.35f;
		// How much of the collection term the upstream catchment can account for, as
		// opposed to the tile simply sitting in a dip. Floodplains along a big river are
		// waterlogged even where the ground barely dips at all.
		private const float FlowShare = 0.6f;

		private static readonly List<PlanetTile> neighbors = new List<PlanetTile>();

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
					tile.swampiness = 0f;
					continue;
				}

				float rainFactor = Mathf.Clamp01((tile.rainfall - RainfallLow) / (RainfallFull - RainfallLow));
				float flatFactor = FlatnessFactor(tile.hilliness);
				if (rainFactor <= 0f || flatFactor <= 0f)
				{
					tile.swampiness = 0f;
					continue;
				}

				// Water collects here either because the ground dips, or because a whole
				// catchment drains through; take whichever is the stronger claim.
				float basin = BasinFactor(layer, tiles, i, tile.elevation, count);
				float catchment = ctx.FlowAccumulation[i];
				float collects = Mathf.Max(basin, catchment * FlowShare);

				float swampiness = rainFactor * flatFactor * (BasinFloor + (1f - BasinFloor) * collects);
				tile.swampiness = Mathf.Clamp01(swampiness);
			}
		}

		private static float FlatnessFactor(Hilliness hilliness)
		{
			switch (hilliness)
			{
				case Hilliness.Flat:
					return 1f;
				case Hilliness.SmallHills:
					return 0.55f;
				case Hilliness.LargeHills:
					return 0.2f;
				default:
					return 0f; // mountainous, impassable, undefined
			}
		}

		private static float BasinFactor(PlanetLayer layer, List<Tile> tiles, int tileId, float elevation, int count)
		{
			layer.GetTileNeighbors(tileId, neighbors);
			float sum = 0f;
			int n = 0;
			for (int k = 0; k < neighbors.Count; k++)
			{
				int nid = neighbors[k].tileId;
				if (nid < 0 || nid >= count)
				{
					continue;
				}
				float neighborElevation = tiles[nid].elevation;
				if (neighborElevation <= 0f)
				{
					continue; // a coast drains to the sea, not into this tile
				}
				sum += neighborElevation;
				n++;
			}
			if (n == 0)
			{
				return 0f;
			}
			float meanNeighbor = sum / n;
			// Positive when this tile is a local low point that water flows toward.
			return Mathf.Clamp01((meanNeighbor - elevation) / BasinDepthScale);
		}
	}
}
