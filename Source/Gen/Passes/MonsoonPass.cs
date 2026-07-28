// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;

namespace Worldsmith.Gen.Passes
{
	/// <summary>
	/// Monsoon rains. Land gains and loses heat far faster than the sea, so a big
	/// landmass in the tropics ends up much hotter than the water beside it each summer.
	/// The air above it rises, ocean air floods in to replace it, and it arrives carrying
	/// the sea with it: months of torrential rain, then a dry season when the flow
	/// reverses in winter.
	///
	/// Three things have to line up for that to happen, and all three are already known
	/// by the time this runs: the tile has to be in the tropics or subtropics, its
	/// climate has to swing hard between summer and winter (which is the land-sea heat
	/// contrast driving the whole thing), and the sea has to be close enough for the
	/// inflowing air to still be wet. Where all three hold, a great deal of rain is
	/// added; where any one fails, nothing happens.
	///
	/// Runs after seasonality, which supplies the swing, and before aridity, which has to
	/// see the final rainfall.
	/// </summary>
	public sealed class MonsoonPass : IGenPass
	{
		public string Name => "Monsoon";

		// Monsoons belong to the tropics and subtropics, strongest around this latitude.
		private const float PeakLatitude = 18f;
		private const float LatitudeSpread = 13f;

		// Half the summer-to-winter swing (deg C) at which land-sea contrast is at full
		// strength. Calibrated to what the tropics actually reach, not to temperate
		// extremes: a monsoon is driven by how much hotter the land gets than the sea in
		// summer, and the belt this happens in has a modest annual range to begin with.
		// Judging it against temperate swings leaves every monsoon at half strength.
		private const float FullContrastSwing = 7f;

		// Distance inland (tiles) beyond which the inflowing air has given up its water.
		private const float MoistureReach = 18f;

		// Rain (mm/year) delivered by a monsoon at full strength.
		private const float MonsoonRainfall = 1400f;

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
					continue;
				}

				float latitude = Mathf.Abs(layer.LongLatOf(i).y);
				float latitudeFactor = Gaussian(latitude, PeakLatitude, LatitudeSpread);
				if (latitudeFactor <= 0.01f)
				{
					continue;
				}

				float swing = (ctx.SummerMaxTemp[i] - ctx.WinterMinTemp[i]) * 0.5f;
				float contrast = Mathf.Clamp01(swing / FullContrastSwing);

				float moistureAccess = Mathf.Clamp01(1f - ctx.CoastDistance[i] / MoistureReach);

				float strength = latitudeFactor * contrast * moistureAccess;
				if (strength <= 0f)
				{
					continue;
				}

				ctx.MonsoonStrength[i] = strength;
				tile.rainfall += MonsoonRainfall * strength;
			}
		}

		private static float Gaussian(float x, float mean, float sigma)
		{
			float d = (x - mean) / sigma;
			return Mathf.Exp(-d * d);
		}
	}
}
