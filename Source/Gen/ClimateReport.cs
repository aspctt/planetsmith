// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Planetsmith.Gen
{
	/// <summary>
	/// Reports the finished rainfall as a band-by-band summary over land.
	///
	/// The latitude model can be worked out on paper, but what a planet ends up with
	/// cannot: moisture running out inland, mountains, monsoons and coastal currents
	/// all multiply into it afterwards, and only land is ever looked at by a player.
	/// A belt that reads as reasonable in the formula can still arrive as desert
	/// everywhere it matters, which is exactly the complaint this is here to settle.
	/// </summary>
	public static class ClimateReport
	{
		// Width of each reported band, in degrees of latitude.
		private const int BandDegrees = 10;
		private const int BandCount = 90 / BandDegrees;

		// Rainfall below which ground is dry enough that only arid biomes will take it, and
		// above which only the wettest will. Neither is a threshold the generator uses:
		// they are there so a band's spread can be read, since a mean alone cannot tell a
		// belt that is uniformly damp from one running between desert and rainforest, and
		// the difference is the whole of what "no variation" means.
		private const float AridRainfall = 400f;
		private const float SoakedRainfall = 2000f;

		public static void Log(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			int count = tiles.Count;

			var land = new int[BandCount];
			var arid = new int[BandCount];
			var soaked = new int[BandCount];
			var total = new double[BandCount];

			for (int i = 0; i < count; i++)
			{
				Tile tile = tiles[i];
				if (tile.elevation <= 0f)
				{
					continue; // the sea has rainfall of its own, and nobody lives in it
				}

				int band = Mathf.Clamp((int)(Mathf.Abs(layer.LongLatOf(i).y) / BandDegrees), 0, BandCount - 1);
				land[band]++;
				total[band] += tile.rainfall;
				if (tile.rainfall < AridRainfall)
				{
					arid[band]++;
				}
				else if (tile.rainfall >= SoakedRainfall)
				{
					soaked[band]++;
				}
			}

			var text = new System.Text.StringBuilder("[Planetsmith] Rainfall over land by latitude: ");
			for (int b = 0; b < BandCount; b++)
			{
				if (land[b] == 0)
				{
					text.Append($"{b * BandDegrees}-{(b + 1) * BandDegrees} no land, ");
					continue;
				}
				double mean = total[b] / land[b];
				float aridShare = (float)arid[b] / land[b];
				float soakedShare = (float)soaked[b] / land[b];
				text.Append($"{b * BandDegrees}-{(b + 1) * BandDegrees} {mean:F0}mm ({aridShare:P0} arid, {soakedShare:P0} soaked), ");
			}
			text.Length -= 2;
			text.Append('.');
			Verse.Log.Message(text.ToString());

			// What the game's own river step used to have to work with, against what it
			// gets now. Rivers are sized by the rainfall running through them, so a planet
			// handed less water grows smaller rivers however high the density is set.
			double ours = 0d;
			int landTotal = 0;
			for (int b = 0; b < BandCount; b++)
			{
				ours += total[b];
				landTotal += land[b];
			}
			if (landTotal > 0 && ctx.VanillaRainfallMean > 0f)
			{
				float mean = (float)(ours / landTotal);
				Verse.Log.Message($"[Planetsmith] Rainfall feeding the rivers: {mean:F0}mm against vanilla's {ctx.VanillaRainfallMean:F0}mm ({mean / ctx.VanillaRainfallMean:P0}).");
			}
		}
	}
}
