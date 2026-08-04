// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Planetsmith.Compat;

namespace Planetsmith.Gen.Passes
{
	/// <summary>
	/// Re-selects every tile's biome against the freshly computed climate. It runs
	/// each biome's own worker in a competitive best-score selection (the shape
	/// vanilla uses), driven by Planetsmith's temperature and rainfall, and then layers
	/// Planetsmith's own adjustments on top of that base score. The first adjustment is
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

		// A biome that only accepts rainfall up to this is treated as belonging to dry
		// country. Set above the semi-arid scrub band (which tolerates up to 2000mm), or
		// scrub escapes the gate and takes over every warm region, since its score rises
		// with temperature faster than forest's does.
		private const float DryBiomeRainfallLimit = 1600f;
		// Effective moisture above which land is no longer meaningfully water limited.
		private const float HumidAridityIndex = 0.65f;
		// Score lost per unit of effective moisture beyond that, for dry-country biomes.
		private const float AridityPenaltyPerUnit = 25f;

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			List<BiomeDef> biomes = DefDatabase<BiomeDef>.AllDefsListForReading;
			PlanetsmithWorldSettings world = PlanetsmithWorldParams.Active;
			bool playerHasChosen = world != null && world.AnyBiomeAdjusted;
			ExternalTuning[] external = BuildExternalTuning(biomes);
			var tiles = layer.Tiles;
			for (int i = 0; i < tiles.Count; i++)
			{
				Tile tile = tiles[i];
				BiomeDef best = SelectBiome(biomes, external, tile, layer, ctx.WinterMinTemp[i], ctx.AridityIndex[i], world, out float bestScore);
				if (best == null)
				{
					continue;
				}

				// Anything under water must end up with a water biome, and that has to be
				// settled before the rule below. Open sea scores zero rather than
				// positive, so leaving it to that rule would let a tile the rising sea
				// has just drowned keep whatever it was when it was still dry land. A
				// forest sitting on the sea floor is bad enough on its own, but it also
				// hides the coastline from the game, which finds river mouths by looking
				// for ocean, and so quietly costs the world its rivers.
				if (tile.WaterCovered)
				{
					if (BiomeProfiler.IsWaterBiome(best))
					{
						tile.PrimaryBiome = best;
					}
					continue;
				}

				// On dry land, a positive score means some biome genuinely suits the ground
				// and takes it. Otherwise every candidate was merely tolerating the tile,
				// and vanilla's own choice is left standing as the better of two poor fits.
				//
				// Unless the player has said what they want. Their settings only ever reach
				// our own selection, so handing the tile back to vanilla hands it to a
				// choice made before they touched anything, and made against a climate we
				// have since overwritten. A biome turned all the way down would sit there
				// unmoved on exactly the ground its owner was trying to clear it from,
				// which reads as the setting being broken, and fairly so.
				if (bestScore > 0f || playerHasChosen)
				{
					tile.PrimaryBiome = best;
				}
			}
		}

		/// <summary>
		/// Another mod's per-biome adjustment, already looked up so the tile loop can just
		/// do the arithmetic. Selection asks every biome about every tile, which on a large
		/// planet is millions of questions, and a dictionary lookup inside that is a cost
		/// worth paying once per biome instead.
		/// </summary>
		private readonly struct ExternalTuning
		{
			public readonly float Offset;
			public readonly float Multiplier;
			public readonly bool Applies;

			public ExternalTuning(float offset, float multiplier)
			{
				Offset = offset;
				Multiplier = multiplier;
				Applies = offset != 0f || multiplier != 1f;
			}
		}

		private static ExternalTuning[] BuildExternalTuning(List<BiomeDef> biomes)
		{
			if (!ModCompat.TryGetWorldbuilderBiomeTuning(out var offsets, out var commonalities))
			{
				return null;
			}

			var tuning = new ExternalTuning[biomes.Count];
			bool any = false;
			int adjusted = 0;
			for (int i = 0; i < biomes.Count; i++)
			{
				// Skip what selection itself skips, in the same order. This walks the whole
				// biome database rather than the candidates for one tile, so it meets defs
				// selection never does.
				if (!biomes[i].implemented || !biomes[i].generatesNaturally)
				{
					continue;
				}

				// Sea, lake and their modded cousins are left out of Planetsmith's own list
				// deliberately: they have no dry-land alternative to fall back on, so
				// turning one off could only produce a planet that fails to draw. The other
				// mod's list does include them, so its settings for them are ignored here
				// rather than carried through a door we closed on purpose.
				if (BiomeProfiler.IsWaterBiome(biomes[i]))
				{
					tuning[i] = new ExternalTuning(0f, 1f);
					continue;
				}

				string defName = biomes[i].defName;
				float offset = 0f;
				if (offsets != null)
				{
					int storedOffset;
					if (offsets.TryGetValue(defName, out storedOffset))
					{
						offset = storedOffset;
					}
				}

				// Their frequency is stored in tenths, so ten is a biome left alone. Read
				// with a default rather than through their own accessor, which fills the
				// entry in as a side effect; their settings are theirs to write.
				float multiplier = 1f;
				if (commonalities != null)
				{
					int storedCommonality;
					if (commonalities.TryGetValue(defName, out storedCommonality))
					{
						multiplier = storedCommonality / 10f;
					}
				}
				tuning[i] = new ExternalTuning(offset, multiplier);
				if (tuning[i].Applies)
				{
					adjusted++;
					any = true;
				}
			}

			if (any)
			{
				// Worth saying out loud. A handover that failed to bind would look exactly
				// like one that worked, and the symptom would be a menu that silently does
				// nothing, which is the very thing this is here to stop.
				Log.Message($"[Planetsmith] Carrying Worldbuilder's own settings for {adjusted} biome(s) into biome selection.");
			}
			return any ? tuning : null;
		}

		/// <summary>
		/// Applies Worldbuilder's adjustment exactly as it would have been applied to
		/// vanilla's own selection, including its convention that a frequency of zero
		/// banishes the biome outright unless an offset was set to argue otherwise.
		/// </summary>
		private static float ApplyExternalTuning(float score, ExternalTuning tuning)
		{
			score += tuning.Offset;
			if (tuning.Multiplier == 0f)
			{
				return tuning.Offset != 0f ? tuning.Offset : -999f;
			}
			return score >= 0f ? score * tuning.Multiplier : score / tuning.Multiplier;
		}

		private static BiomeDef SelectBiome(List<BiomeDef> biomes, ExternalTuning[] external, Tile tile, PlanetLayer layer, float winterMin, float aridityIndex, PlanetsmithWorldSettings world, out float bestScore)
		{
			BiomeDef best = null;
			bestScore = 0f;
			for (int i = 0; i < biomes.Count; i++)
			{
				BiomeDef biome = biomes[i];
				if (!biome.implemented || !biome.generatesNaturally)
				{
					continue;
				}
				BiomeSettings tuning = world?.ForBiomeOrNull(biome.defName);
				if (tuning != null && !tuning.enabled)
				{
					continue; // the player has banished this biome from the planet
				}
				BiomeWorker worker = biome.Worker;
				if (worker == null || !worker.CanPlaceOnLayer(biome, layer))
				{
					continue;
				}
				try
				{
					float score = worker.GetScore(biome, tile, tile.tile);
					// Straight after the worker, which is where the mod that owns these
					// settings puts them, so the two agree about what a score means before
					// anything else touches it.
					if (external != null && external[i].Applies)
					{
						score = ApplyExternalTuning(score, external[i]);
					}
					score = ApplyFrostGate(biome, score, winterMin);
					score = ApplyAridityGate(biome, score, aridityIndex);
					score = ApplyPlayerTuning(score, tuning);
					if (best == null || score > bestScore)
					{
						best = biome;
						bestScore = score;
					}
				}
				catch (Exception e)
				{
					Log.ErrorOnce($"[Planetsmith] Biome worker '{biome.defName}' failed during selection: {e.Message}", biome.shortHash ^ 0x570B10);
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

		/// <summary>
		/// Applies the player's own adjustments last, on top of everything the climate
		/// decided. The offset shifts a biome across the line in places it would narrowly
		/// win or lose; the commonality multiplier then stretches or shrinks the ground it
		/// takes, and is applied only to a winning score so that scaling a biome up cannot
		/// drag a negative score further down.
		/// </summary>
		private static float ApplyPlayerTuning(float score, BiomeSettings tuning)
		{
			if (tuning == null)
			{
				return score;
			}
			score += tuning.scoreOffset;
			if (score > 0f)
			{
				score *= tuning.commonality;
			}
			return score;
		}

		/// <summary>
		/// Holds dry-country biomes back from ground that is not really water starved.
		/// Vanilla decides deserts on rainfall alone, so a chilly place with little rain
		/// looks like a desert to it, when in truth almost nothing evaporates there and
		/// the ground stays damp. Comparing effective moisture instead keeps deserts in
		/// country that is genuinely parched.
		/// </summary>
		private static float ApplyAridityGate(BiomeDef biome, float score, float aridityIndex)
		{
			if (score <= 0f)
			{
				return score;
			}
			if (aridityIndex <= HumidAridityIndex)
			{
				return score; // genuinely dry ground; the biome is welcome to it
			}
			float wetLimit = BiomeProfiler.WetLimit(biome);
			if (wetLimit > DryBiomeRainfallLimit)
			{
				return score; // not a dry-country biome
			}
			return score - AridityPenaltyPerUnit * (aridityIndex - HumidAridityIndex);
		}
	}
}
