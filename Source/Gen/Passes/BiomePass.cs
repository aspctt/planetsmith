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
	/// vanilla uses), driven by Worldsmith's temperature and rainfall, and then layers
	/// Worldsmith's own adjustments on top of that base score. The first adjustment is
	/// a frost gate: a frost-intolerant biome (learned from its worker via
	/// <see cref="BiomeProfiler"/>) is penalised where the winter minimum falls below
	/// its cold tolerance, so a warm annual mean with a lethal winter no longer reads
	/// as tropical. Water biomes fall out naturally because their workers key off
	/// elevation.
	/// </summary>
	public sealed class BiomePass : IGenPass
	{
		public string Name => "Biome";

		// A biome that cannot score below this temperature is treated as frost-intolerant.
		private const float FrostSensitiveThreshold = 5f;
		// Score lost per degree the winter minimum sits below a biome's cold tolerance.
		private const float FrostPenaltyPerDegree = 2.5f;

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			List<BiomeDef> biomes = DefDatabase<BiomeDef>.AllDefsListForReading;
			var tiles = layer.Tiles;
			for (int i = 0; i < tiles.Count; i++)
			{
				Tile tile = tiles[i];
				BiomeDef best = SelectBiome(biomes, tile, layer, ctx.WinterMinTemp[i]);
				if (best != null)
				{
					tile.PrimaryBiome = best;
				}
			}
		}

		private static BiomeDef SelectBiome(List<BiomeDef> biomes, Tile tile, PlanetLayer layer, float winterMin)
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
					score = ApplyFrostGate(biome, score, winterMin);
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

		private static float ApplyFrostGate(BiomeDef biome, float score, float winterMin)
		{
			if (score <= 0f)
			{
				return score;
			}
			float coldTolerance = BiomeProfiler.ColdTolerance(biome);
			if (coldTolerance <= FrostSensitiveThreshold)
			{
				return score; // biome tolerates cold; the winter minimum is irrelevant
			}
			if (winterMin >= coldTolerance)
			{
				return score; // winters stay above what the biome needs
			}
			return score - FrostPenaltyPerDegree * (coldTolerance - winterMin);
		}
	}
}
