// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;

namespace Worldsmith.Gen.Passes
{
	/// <summary>
	/// Effective moisture, which is what plants actually experience. Rainfall alone is
	/// misleading: 400mm falling on baking desert and 400mm falling on cold tundra
	/// support utterly different life, because warmth decides how much of that water
	/// evaporates straight back out. This computes the ratio of rainfall to how much the
	/// climate could evaporate given the chance, so a cold, dry place reads as damp and a
	/// hot place with respectable rainfall can still read as arid.
	///
	/// Evaporative demand follows Holdridge's estimate from biotemperature (mean
	/// temperature counted only between freezing and 30C, since plants neither transpire
	/// below freezing nor benefit further past that). The resulting index uses the usual
	/// bands: below 0.2 is arid, 0.2 to 0.5 semi-arid, 0.65 and up humid.
	///
	/// Must run after everything that alters temperature or rainfall.
	/// </summary>
	public sealed class AridityPass : IGenPass
	{
		public string Name => "Aridity";

		// Millimetres of potential evaporation per year, per degree of biotemperature.
		private const float EvaporationPerDegree = 58.93f;
		// Floor on evaporative demand so frozen ground does not divide by nothing.
		private const float MinEvaporation = 60f;
		// Ceiling on the index; past this a tile is simply very wet.
		private const float MaxAridityIndex = 3f;

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
					ctx.AridityIndex[i] = MaxAridityIndex; // open water is never moisture limited
					continue;
				}

				float biotemperature = Mathf.Clamp(tile.temperature, 0f, 30f);
				float evaporation = Mathf.Max(MinEvaporation, EvaporationPerDegree * biotemperature);
				ctx.AridityIndex[i] = Mathf.Clamp(tile.rainfall / evaporation, 0f, MaxAridityIndex);
			}
		}
	}
}
