// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using Verse;

namespace Planetsmith
{
	/// <summary>
	/// A player's adjustments to one biome on one planet: whether it may appear at all,
	/// how strongly it competes for ground it already suits, and a flat nudge to its
	/// score that can push it into or out of places it would otherwise narrowly lose or
	/// win. Defaults leave the biome exactly as its own worker scores it.
	/// </summary>
	public class BiomeSettings : IExposable
	{
		public const float MaxCommonality = 5f;
		public const float MaxScoreOffset = 50f;

		public bool enabled = true;
		public float commonality = 1f;
		public float scoreOffset = 0f;

		public bool IsDefault => enabled && commonality == 1f && scoreOffset == 0f;

		public void ExposeData()
		{
			Scribe_Values.Look(ref enabled, "enabled", defaultValue: true);
			Scribe_Values.Look(ref commonality, "commonality", 1f);
			Scribe_Values.Look(ref scoreOffset, "scoreOffset", 0f);
		}

		public BiomeSettings Clone()
		{
			return new BiomeSettings
			{
				enabled = enabled,
				commonality = commonality,
				scoreOffset = scoreOffset,
			};
		}

		public void Reset()
		{
			enabled = true;
			commonality = 1f;
			scoreOffset = 0f;
		}
	}
}
