// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Worldsmith.Gen.Passes
{
	/// <summary>
	/// Re-selects every tile's biome against the freshly computed climate. It runs
	/// each biome's own worker in a competitive best-score selection (the shape
	/// vanilla uses), but driven by Worldsmith's temperature and rainfall so the
	/// map's biomes follow the new climate. Water biomes (ocean/sea ice/lake) fall
	/// out of this naturally because their workers key off elevation.
	/// </summary>
	public sealed class BiomePass : IGenPass
	{
		public string Name => "Biome";

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			List<BiomeDef> biomes = DefDatabase<BiomeDef>.AllDefsListForReading;
			var tiles = layer.Tiles;
			for (int i = 0; i < tiles.Count; i++)
			{
				Tile tile = tiles[i];
				BiomeDef best = SelectBiome(biomes, tile, layer);
				if (best != null)
				{
					tile.PrimaryBiome = best;
				}
			}
		}

		private static BiomeDef SelectBiome(List<BiomeDef> biomes, Tile tile, PlanetLayer layer)
		{
			BiomeDef best = null;
			float bestScore = 0f;
			for (int i = 0; i < biomes.Count; i++)
			{
				BiomeDef biome = biomes[i];
				if (!biome.implemented || !biome.generatesNaturally)
				{
					continue;
				}
				BiomeWorker worker = biome.Worker;
				if (worker == null || !worker.CanPlaceOnLayer(biome, layer))
				{
					continue;
				}
				try
				{
					float score = worker.GetScore(biome, tile, tile.tile);
					if (best == null || score > bestScore)
					{
						best = biome;
						bestScore = score;
					}
				}
				catch (Exception e)
				{
					Log.ErrorOnce($"[Worldsmith] Biome worker '{biome.defName}' failed during selection: {e.Message}", biome.shortHash ^ 0x570B10);
				}
			}
			return best;
		}
	}
}
