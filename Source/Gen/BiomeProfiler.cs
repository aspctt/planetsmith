// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Worldsmith.Gen
{
	/// <summary>
	/// Learns a biome's climate niche by probing its own worker, so Worldsmith can
	/// reason about biomes it has no hardcoded knowledge of, including modded ones.
	/// Currently derives the cold tolerance: the lowest temperature at which the
	/// biome scores positively for any rainfall. Frost-intolerant biomes (tropical
	/// and friends) have a high cold tolerance, which the biome pass gates against
	/// the winter minimum rather than the annual mean.
	///
	/// Results are cached per biome for the session; workers are deterministic in the
	/// temperature/rainfall inputs we vary, so one probe is enough.
	/// </summary>
	public static class BiomeProfiler
	{
		/// <summary>Sentinel for biomes that never score positive in the probe (e.g. water biomes on a land tile).</summary>
		public const float NoColdTolerance = -999f;

		/// <summary>Returned for biomes with no upper limit on rainfall.</summary>
		public const float NoWetLimit = float.MaxValue;

		private static readonly Dictionary<BiomeDef, float> coldToleranceCache = new Dictionary<BiomeDef, float>();
		private static readonly Dictionary<BiomeDef, float> wetLimitCache = new Dictionary<BiomeDef, float>();

		/// <summary>
		/// A real tile reference to probe with. Some workers, including those other mods
		/// configure through XML, build their context from the tile's id and would throw
		/// on an invalid one, which would cost us the profile and litter the log.
		/// </summary>
		private static PlanetTile ProbeTile
		{
			get
			{
				PlanetLayer surface = Find.WorldGrid?.Surface;
				return surface != null && surface.Tiles.Count > 0 ? new PlanetTile(0, surface) : PlanetTile.Invalid;
			}
		}

		public static float ColdTolerance(BiomeDef biome)
		{
			if (coldToleranceCache.TryGetValue(biome, out float cached))
			{
				return cached;
			}
			float value = ProbeColdTolerance(biome);
			coldToleranceCache[biome] = value;
			return value;
		}

		/// <summary>
		/// The wettest conditions a biome will still accept, in mm of rainfall. A low
		/// value marks a biome that only belongs in dry country, which is what lets the
		/// aridity gate tell a desert from a forest without naming either.
		/// </summary>
		public static float WetLimit(BiomeDef biome)
		{
			if (wetLimitCache.TryGetValue(biome, out float cached))
			{
				return cached;
			}
			float value = ProbeWetLimit(biome);
			wetLimitCache[biome] = value;
			return value;
		}

		private static float ProbeWetLimit(BiomeDef biome)
		{
			BiomeWorker worker = biome?.Worker;
			if (worker == null)
			{
				return NoWetLimit;
			}

			var probe = new Tile { elevation = 100f };
			PlanetTile probeTile = ProbeTile;
			float wettest = -1f;
			for (float rainfall = 0f; rainfall <= 6000f; rainfall += 500f)
			{
				for (float temperature = -60f; temperature <= 50f; temperature += 5f)
				{
					probe.temperature = temperature;
					probe.rainfall = rainfall;
					float score;
					try
					{
						score = worker.GetScore(biome, probe, probeTile);
					}
					catch
					{
						return NoWetLimit;
					}
					if (score > 0f)
					{
						wettest = rainfall;
						break;
					}
				}
			}
			return wettest < 0f ? NoWetLimit : wettest;
		}

		private static float ProbeColdTolerance(BiomeDef biome)
		{
			BiomeWorker worker = biome?.Worker;
			if (worker == null)
			{
				return NoColdTolerance;
			}

			// A bare land tile: WaterCovered is false, and we sweep temperature and
			// rainfall directly. Sweep temperature from cold to warm and return the
			// first temperature that scores positively for some rainfall.
			var probe = new Tile { elevation = 100f };
			PlanetTile probeTile = ProbeTile;
			for (float temperature = -60f; temperature <= 50f; temperature += 5f)
			{
				for (float rainfall = 0f; rainfall <= 6000f; rainfall += 500f)
				{
					probe.temperature = temperature;
					probe.rainfall = rainfall;
					float score;
					try
					{
						score = worker.GetScore(biome, probe, probeTile);
					}
					catch
					{
						// Worker relies on real tile geometry we can't synthesise; give
						// up on profiling it and let it use its unmodified vanilla score.
						return NoColdTolerance;
					}
					if (score > 0f)
					{
						return temperature;
					}
				}
			}
			return NoColdTolerance;
		}
	}
}
