// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Worldsmith.Gen
{
	/// <summary>
	/// Shared state threaded through the generation passes for a single layer.
	/// Baselines are derived once, up front, from the world's overall temperature
	/// and rainfall settings so every pass sees the player's chosen climate.
	/// </summary>
	public sealed class GenContext
	{
		public readonly PlanetLayer Layer;
		public readonly int TileCount;
		public readonly int Seed;

		/// <summary>Mean annual temperature (deg C) at the equator, from the world temp setting.</summary>
		public readonly float EquatorMeanTemp;

		/// <summary>Mean annual temperature (deg C) at the poles, from the world temp setting.</summary>
		public readonly float PoleMeanTemp;

		/// <summary>Global rainfall scale from the world rainfall setting (1.0 = Normal).</summary>
		public readonly float RainfallMultiplier;

		public GenContext(PlanetLayer layer)
		{
			Layer = layer;
			TileCount = layer.Tiles.Count;
			Seed = Find.World.info.Seed;

			// Both settings are 7-step enums (index 3 == Normal). Anchor the model to
			// an Earth-like planet at Normal and shift the whole globe per step.
			int tempIdx = Mathf.Clamp((int)Find.World.info.overallTemperature, 0, 6);
			EquatorMeanTemp = 30f + (tempIdx - 3) * 7f;
			PoleMeanTemp = -45f + (tempIdx - 3) * 10f;

			int rainIdx = Mathf.Clamp((int)Find.World.info.overallRainfall, 0, 6);
			RainfallMultiplier = (rainIdx + 1) / 4f;
		}
	}
}
