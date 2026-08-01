// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Planetsmith.Gen.Passes
{
	/// <summary>
	/// Terrain-driven rainfall adjustment. Where the prevailing wind climbs into higher
	/// ground the air is forced upward and rains out on the windward slope; whatever
	/// crosses the top arrives with that much less water to give.
	///
	/// The loss is carried downwind rather than applied where it happens. A rain shadow
	/// is not really a property of the slope behind a range, it is a property of the air
	/// that came over it, and that air stays dry for a long way: the Great Basin lies
	/// several hundred kilometres behind the Sierra Nevada and is dry the whole way
	/// across. So each tile's climb is turned into a share of moisture taken from the
	/// passing air, and that depletion is then advected along the wind, recovering
	/// gradually as the air draws moisture back off the ground it crosses, and resetting
	/// entirely once it reaches open sea.
	///
	/// Runs after the base climate, before biomes.
	/// </summary>
	public sealed class OrographyPass : IGenPass
	{
		public string Name => "Orography";

		// Both of the numbers below are calibrated against what the terrain actually
		// offers, which is a much narrower range than it looks from the map: measured over
		// a whole planet, 74% of land climbs less than 25m to the tile upwind of it, 98%
		// climbs less than 100m, and the single steepest rise anywhere on the globe was
		// 504m. A scale set by eye from how tall the mountains are, rather than from how
		// abruptly they rise between one tile and the next, leaves them inert.
		//
		// Rise to the upwind neighbour that wrings the air out in a single tile. Reachable
		// on a mountain front, which is the point: a threshold nothing ever crosses is the
		// same as having no mountains at all.
		private const float SlopeScale = 320f;
		// Rise below which the air is not lifted enough to rain out, and simply flows over.
		// Set above the rolling country that makes up most of a continent. Without a floor
		// here every slight undulation sheds a crumb, and those crumbs compound along the
		// wind until the whole landmass carries a haze of shadow belonging to no mountain
		// in particular, which the ranges that should own it never stand out from.
		private const float MinLiftingRise = 80f;
		// Air forced up a slope sheds a great deal of its water there, so the windward
		// flank gains more than the lee loses. Worth being generous with: a range whose
		// two sides look alike is the clearest sign the mountains are doing nothing.
		private const float WindwardBoost = 1.2f;
		// Rain lost where the arriving air has been wrung out completely. Can afford to be
		// this heavy because so little ground is ever deeply shaded: measured over a
		// planet, the mean depletion is about 0.03, so this costs the world under 2% of
		// its rain overall while halving it in the places that have genuinely earned it.
		// Still a long way short of all of it, though, since dry country is the aim rather
		// than dead ground, and rain that reaches nothing is a floor everything downstream
		// multiplies back into nothing.
		private const float LeewardShadow = 0.55f;
		// Share of the depletion still carried after one tile of travel. Sets how far a
		// shadow reaches: 0.85 leaves a range still drying the land six or seven tiles
		// behind it, and spent by twenty.
		private const float LeeRecovery = 0.85f;
		// Tiles of reach resolved. Past this the depletion left is under a hundredth.
		private const int PropagationSteps = 32;
		// Depletion at which a tile counts as meaningfully shaded, for the log line.
		private const float NotableShadow = 0.25f;
		// Bucket edges, in metres, for reporting what the terrain actually offers the wind.
		private static readonly float[] RiseBuckets = { 25f, 50f, 100f, 200f, 400f };

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			int count = tiles.Count;

			bool[] isLand = new bool[count];
			for (int i = 0; i < count; i++)
			{
				isLand[i] = tiles[i].elevation > 0f;
			}

			float[] shed = new float[count];
			MeasureClimbs(ctx, layer, tiles, count, isLand, shed);
			CarryDownwind(ctx, isLand, count, shed);
			ApplyShadow(ctx, tiles, count, isLand);
		}

		/// <summary>
		/// Rains out the windward slopes and records how much each climb takes from the
		/// air crossing it. Both come from the same measured rise, so a slope's own gain
		/// and the loss it inflicts downwind can never disagree.
		/// </summary>
		private static void MeasureClimbs(GenContext ctx, PlanetLayer layer, List<Tile> tiles, int count, bool[] isLand, float[] shed)
		{
			int[] histogram = new int[RiseBuckets.Length + 1];
			float steepest = 0f;
			int land = 0;

			for (int i = 0; i < count; i++)
			{
				Tile tile = tiles[i];
				if (!isLand[i])
				{
					continue;
				}

				float lat = layer.LongLatOf(i).y;
				Vector3 center = layer.GetTileCenter(i);
				Vector3 wind = WindModel.PrevailingWind(center, lat);
				if (wind == Vector3.zero)
				{
					continue;
				}

				land++;
				int upwind = TileGeometry.UpwindNeighbor(layer, i, center, wind);
				float upwindElevation = upwind >= 0 ? tiles[upwind].elevation : tile.elevation;
				float rise = tile.elevation - upwindElevation;
				Record(histogram, ref steepest, rise);
				if (rise <= MinLiftingRise)
				{
					continue; // level, descending, or too slight a rise to lift the air
				}

				float climb = Mathf.Clamp01((rise - MinLiftingRise) / (SlopeScale - MinLiftingRise));
				tile.rainfall = Mathf.Max(0f, tile.rainfall * (1f + WindwardBoost * climb));

				// The player's dial belongs here rather than on the final loss, so that
				// turning it up both deepens a shadow and lengthens it: air stripped
				// harder at the peaks stays dry further downwind. It also means no
				// setting can push rainfall toward nothing, since the loss it feeds is
				// capped and rain never falls all the way to zero.
				shed[i] = Mathf.Clamp01(climb * ctx.Tuning.rainShadow);
			}

			ReportRises(histogram, steepest, land);
		}

		private static void Record(int[] histogram, ref float steepest, float rise)
		{
			if (rise > steepest)
			{
				steepest = rise;
			}
			for (int b = 0; b < RiseBuckets.Length; b++)
			{
				if (rise < RiseBuckets[b])
				{
					histogram[b]++;
					return;
				}
			}
			histogram[RiseBuckets.Length]++;
		}

		/// <summary>
		/// Reports what the terrain gives the wind to climb. Everything this pass does
		/// hangs off that one distribution, and it is not something the map screen can
		/// show: two worlds whose mountains look identical can offer the air completely
		/// different slopes, depending on how finely the planet happens to be divided.
		/// </summary>
		private static void ReportRises(int[] histogram, float steepest, int land)
		{
			if (land <= 0)
			{
				return;
			}
			var text = new System.Text.StringBuilder("[Planetsmith] Upwind rise per tile: ");
			for (int b = 0; b <= RiseBuckets.Length; b++)
			{
				float share = (float)histogram[b] / land;
				string band = b == 0 ? $"under {RiseBuckets[0]:F0}m"
					: b == RiseBuckets.Length ? $"over {RiseBuckets[b - 1]:F0}m"
					: $"{RiseBuckets[b - 1]:F0}-{RiseBuckets[b]:F0}m";
				text.Append($"{share:P1} {band}, ");
			}
			text.Append($"steepest {steepest:F0}m.");
			Log.Message(text.ToString());
		}

		/// <summary>
		/// Settles how depleted the air arriving at each tile is, by relaxation along the
		/// wind: one tile of reach resolves per step. Depletions combine as shares of
		/// what is left rather than adding up, so a second range behind the first dries
		/// the air further without the total ever passing the whole of it.
		/// </summary>
		private static void CarryDownwind(GenContext ctx, bool[] isLand, int count, float[] shed)
		{
			UpwindGraph graph = ctx.Upwind;
			float[] arriving = ctx.RainShadow;
			float[] leaving = new float[count];
			float[] next = new float[count];

			for (int step = 0; step < PropagationSteps; step++)
			{
				for (int i = 0; i < count; i++)
				{
					if (!isLand[i])
					{
						// Open water hands the air its moisture back almost at once, so a
						// shadow never survives a crossing.
						next[i] = 0f;
						continue;
					}

					int[] sources = graph.Sources[i];
					float carried = 0f;
					if (sources != null)
					{
						float[] weights = graph.Weights[i];
						for (int k = 0; k < sources.Length; k++)
						{
							carried += weights[k] * leaving[sources[k]];
						}
						carried *= LeeRecovery;
					}

					// What this tile receives is what the air brought; what it passes on
					// includes its own climb. Keeping the two apart matters, or a peak
					// would stand in its own shadow while it is busy raining.
					arriving[i] = carried;
					next[i] = 1f - (1f - carried) * (1f - shed[i]);
				}

				float[] swap = leaving;
				leaving = next;
				next = swap;
			}
		}

		private static void ApplyShadow(GenContext ctx, List<Tile> tiles, int count, bool[] isLand)
		{
			float land = 0f;
			float shaded = 0f;
			float total = 0f;

			for (int i = 0; i < count; i++)
			{
				if (!isLand[i])
				{
					continue;
				}

				Tile tile = tiles[i];
				float shadow = ctx.RainShadow[i];
				land++;
				total += shadow;
				if (shadow >= NotableShadow)
				{
					shaded++;
				}
				if (shadow > 0f)
				{
					tile.rainfall = Mathf.Max(0f, tile.rainfall * (1f - LeewardShadow * shadow));
				}
			}

			if (land <= 0f)
			{
				return;
			}

			// How much of a planet ends up in shade is the one thing about this that is
			// hard to judge by eye, since the deep shadow beside a range looks the same
			// whether it fades over three tiles or fifteen. Worth a number.
			Log.Message($"[Planetsmith] Rain shadow over {shaded / land:P1} of land (mean depletion {total / land:F2}).");
		}
	}
}
