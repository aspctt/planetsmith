// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Planetsmith.Gen.Passes
{
	/// <summary>
	/// Raises or lowers sea level until the planet has the requested fraction of land.
	/// Rather than inventing new terrain, it finds the elevation that already sits at
	/// the target percentile and shifts every tile so that height becomes the new
	/// shoreline: flooding low ground when the target is small, exposing continental
	/// shelf when it is large. Runs first, because every later pass keys off elevation
	/// and the land/ocean split.
	///
	/// Off by default so vanilla (and mods that shape the coastline themselves) keep
	/// control; enabling it makes Planetsmith the authority on the land fraction.
	/// </summary>
	public sealed class SeaLevelPass : IGenPass
	{
		public string Name => "SeaLevel";

		// Depth given to tiles that the new sea level submerges but that sat above the
		// old shoreline, so freshly flooded ground reads as shallow sea rather than 0m.
		private const float MinFloodDepth = 20f;

		public void Run(GenContext ctx)
		{
			PlanetsmithWorldSettings settings = PlanetsmithWorldParams.Active;
			if (settings == null || !settings.enableSeaLevelControl)
			{
				return;
			}

			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			int count = tiles.Count;
			if (count == 0)
			{
				return;
			}

			float targetLand = Mathf.Clamp(settings.targetLandFraction, 0.05f, 0.95f);

			float[] sorted = new float[count];
			for (int i = 0; i < count; i++)
			{
				sorted[i] = tiles[i].elevation;
			}
			Array.Sort(sorted);

			// The tiles above the (1 - targetLand) percentile become the new land.
			int index = Mathf.Clamp(Mathf.RoundToInt((1f - targetLand) * count), 0, count - 1);
			float newShoreline = sorted[index];
			if (Mathf.Approximately(newShoreline, 0f))
			{
				return; // already at the requested land fraction
			}

			// Moving the shoreline by sliding every height up or down the same amount
			// would drag the sea floor thousands of metres below anything the game
			// otherwise produces, and leave the peaks correspondingly wrong. Instead each
			// side is stretched back over the range it originally occupied: the land that
			// survives spans the old heights, the ground now drowned spans the old
			// depths, and the waterline sits at nothing in between.
			float deepest = Mathf.Min(sorted[0], -MinFloodDepth);
			float tallest = Mathf.Max(sorted[count - 1], 1f);
			float aboveShore = Mathf.Max(1f, tallest - newShoreline);
			float belowShore = Mathf.Max(1f, newShoreline - deepest);

			for (int i = 0; i < count; i++)
			{
				Tile tile = tiles[i];
				float elevation = tile.elevation;
				if (elevation > newShoreline)
				{
					tile.elevation = Mathf.Max(1f, (elevation - newShoreline) / aboveShore * tallest);
				}
				else
				{
					// Underwater, and flattened: hilliness carried over from dry land
					// would otherwise show through as relief on the sea bed.
					float depth = deepest * (1f - (elevation - deepest) / belowShore);
					tile.elevation = Mathf.Min(depth, -MinFloodDepth);
					tile.hilliness = Hilliness.Flat;
				}
			}

			Log.Message($"[Planetsmith] Sea level set for {targetLand:P0} land (waterline was at {newShoreline:F0}m).");
		}
	}
}
