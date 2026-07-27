// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;

namespace Worldsmith.Gen.Passes
{
	/// <summary>
	/// Coastal climate anomalies from the sea and its currents. Which coast of a
	/// landmass you are on matters enormously on a real planet, and the deciding factor
	/// is whether the prevailing wind arrives off the water or off the land.
	///
	/// Where the wind blows onshore, the sea hands the coast mild, moist air, so those
	/// shores run warmer and wetter than their latitude suggests. Where it blows
	/// offshore in the subtropics, water welling up from the deep runs cold along the
	/// shore: it chills the air and kills the rainfall, which is how Earth ends up with
	/// deserts (the Atacama, the Namib) sitting directly on an ocean.
	///
	/// The onshore/offshore call is made once at the waterline, where the advected
	/// moisture still cleanly reflects wind direction, and is then carried inland across
	/// the coastal band. Testing every tile against the moisture directly would instead
	/// measure distance from the sea, since moisture decays inland on any coast, and
	/// would wrongly flip a genuine maritime coast to offshore a few tiles in.
	/// </summary>
	public sealed class OceanCurrentPass : IGenPass
	{
		public string Name => "OceanCurrents";

		// How far inland (in tiles) each influence reaches. Upwelling deserts hug the
		// shore far more tightly than general maritime mildness.
		private const int CoastalReach = 6;
		private const int UpwellingReach = 3;

		// Share of ocean moisture still in the air above which a shoreline tile counts
		// as having the wind at its back off the water.
		private const float OnshoreMoisture = 0.75f;

		// Onshore wind: mild, damp maritime air.
		private const float MaritimeWarming = 2.5f;
		private const float MaritimeRainBoost = 0.25f;

		// Offshore wind over subtropical upwelling: cold water, suppressed rainfall.
		private const float UpwellingCooling = 3.5f;
		private const float UpwellingRainSuppression = 0.55f;
		private const float UpwellingLatitudeMin = 12f;
		private const float UpwellingLatitudeMax = 38f;

		private const sbyte Unclassified = 0;
		private const sbyte Onshore = 1;
		private const sbyte Offshore = -1;

		private static readonly List<PlanetTile> neighbors = new List<PlanetTile>();

		public void Run(GenContext ctx)
		{
			PlanetLayer layer = ctx.Layer;
			var tiles = layer.Tiles;
			int count = tiles.Count;

			sbyte[] wind = ClassifyCoast(ctx, layer, count);

			for (int i = 0; i < count; i++)
			{
				sbyte kind = wind[i];
				if (kind == Unclassified)
				{
					continue;
				}

				int distance = ctx.CoastDistance[i];
				float coastal = 1f - (distance - 1f) / CoastalReach;
				Tile tile = tiles[i];

				if (kind == Onshore)
				{
					tile.temperature += MaritimeWarming * coastal;
					tile.rainfall *= 1f + MaritimeRainBoost * coastal;
					ctx.CoastalAnomaly[i] = coastal;
					continue;
				}

				if (distance > UpwellingReach)
				{
					continue;
				}
				float absLat = Mathf.Abs(layer.LongLatOf(i).y);
				if (absLat >= UpwellingLatitudeMin && absLat <= UpwellingLatitudeMax)
				{
					tile.temperature -= UpwellingCooling * coastal;
					tile.rainfall *= 1f - UpwellingRainSuppression * coastal;
					ctx.CoastalAnomaly[i] = -coastal;
				}
			}
		}

		/// <summary>
		/// Labels every tile in the coastal band as sitting on an onshore or offshore
		/// wind. Tiles touching the sea are judged from the moisture the wind carries;
		/// tiles further in adopt the verdict of the shore they belong to, walking out
		/// one ring at a time so a coastal strip stays of one mind.
		/// </summary>
		private static sbyte[] ClassifyCoast(GenContext ctx, PlanetLayer layer, int count)
		{
			var tiles = layer.Tiles;
			sbyte[] wind = new sbyte[count];

			for (int i = 0; i < count; i++)
			{
				if (tiles[i].elevation > 0f && ctx.CoastDistance[i] == 1)
				{
					wind[i] = ctx.OceanicMoisture[i] >= OnshoreMoisture ? Onshore : Offshore;
				}
			}

			for (int ring = 2; ring <= CoastalReach; ring++)
			{
				for (int i = 0; i < count; i++)
				{
					if (wind[i] != Unclassified || tiles[i].elevation <= 0f || ctx.CoastDistance[i] != ring)
					{
						continue;
					}
					layer.GetTileNeighbors(i, neighbors);
					for (int k = 0; k < neighbors.Count; k++)
					{
						int nid = neighbors[k].tileId;
						if (nid < 0 || nid >= count || ctx.CoastDistance[nid] != ring - 1)
						{
							continue;
						}
						if (wind[nid] != Unclassified)
						{
							wind[i] = wind[nid];
							break;
						}
					}
				}
			}

			return wind;
		}
	}
}
