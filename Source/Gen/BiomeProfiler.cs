// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Planetsmith.Gen
{
	/// <summary>
	/// Learns a biome's climate niche by probing its own worker, so Planetsmith can
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
		private static readonly Dictionary<BiomeDef, bool> waterBiomeCache = new Dictionary<BiomeDef, bool>();

		/// <summary>
		/// Whether a biome belongs to open water rather than dry land. Worked out by
		/// offering the worker a submerged tile: sea and lake biomes will take it, while
		/// land biomes reject it outright. Asking the worker keeps this true for biomes
		/// added by other mods, which a list of names never could.
		/// </summary>
		public static bool IsWaterBiome(BiomeDef biome)
		{
			if (waterBiomeCache.TryGetValue(biome, out bool cached))
			{
				return cached;
			}
			bool value = ProbeIsWaterBiome(biome);
			waterBiomeCache[biome] = value;
			return value;
		}

		private static bool ProbeIsWaterBiome(BiomeDef biome)
		{
			BiomeWorker worker = biome?.Worker;
			if (worker == null)
			{
				return false;
			}

			// Offer dry land in every climate and see whether anything is refused
			// outright. A sea or lake biome turns all of it down flat, and crucially does
			// so on its very first check, before consulting the wider world. That matters
			// because this runs from the world creation page, where no world exists yet
			// and any worker reaching for one would throw.
			var probe = new SurfaceTile { elevation = 100f };
			PlanetTile probeTile = ProbeTile;
			for (float temperature = -60f; temperature <= 50f; temperature += 10f)
			{
				for (float rainfall = 0f; rainfall <= 4000f; rainfall += 1000f)
				{
					probe.temperature = temperature;
					probe.rainfall = rainfall;
					try
					{
						if (worker.GetScore(biome, probe, probeTile) > RejectedScore)
						{
							return false; // it will consider dry land, so it is a land biome
						}
					}
					catch
					{
						// Reaching for a world we do not have marks it as caring about
						// land. Treat it as land and leave it configurable.
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>Workers reject ground they cannot use with a large negative score.</summary>
		private const float RejectedScore = -50f;

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
