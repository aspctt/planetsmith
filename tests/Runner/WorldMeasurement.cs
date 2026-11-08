// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Planetsmith.Generation;

namespace Planetsmith.TestRunner;

/// <summary>
/// Turns a finished world into the statistics a test compares. Everything is read
/// from the tiles the game keeps, after every generation step has run, so the numbers
/// describe the planet a player would get rather than what Planetsmith's own passes
/// handed on. Other mods' steps run after ours, and their changes count too.
/// </summary>
public static class WorldMeasurement
{
	//// Constants
	
	// The same bands and thresholds as Planetsmith's own ClimateReport, so a test result
	// and a log from a hand-made world can be read side by side. They are private there,
	// which is why they are repeated rather than shared.
	private const int BAND_DEGREES = 10;
	private const int BAND_COUNT = 90 / BAND_DEGREES;
	private const float ARID_RAINFALL = 400f;
	private const float SOAKED_RAINFALL = 2000f;
	private const float WATERLOGGED_SWAMPINESS = 0.5f;
	
	// FNV-1a, 64-bit. Chosen for being short and fully specified, not for strength: it
	// only has to tell two worlds apart, and the same input must give the same hash on
	// every run, which string.GetHashCode does not promise.
	private const ulong FNV_OFFSET = 14695981039346656037UL;
	private const ulong FNV_PRIME = 1099511628211UL;
	
	//// Private Functions
	
	private static ulong HashByte(ulong hash, byte value)
	{
		return (hash ^ value) * FNV_PRIME;
	}
	
	private static ulong HashInt(ulong hash, int value)
	{
		hash = HashByte(hash, (byte)value);
		hash = HashByte(hash, (byte)(value >> 8));
		hash = HashByte(hash, (byte)(value >> 16));
		return HashByte(hash, (byte)(value >> 24));
	}
	
	/// <summary>Hashes a float's exact bits, so even the smallest change in a value shows.</summary>
	private static ulong HashFloat(ulong hash, float value)
	{
		return HashInt(hash, BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
	}
	
	private static ulong HashString(ulong hash, string value)
	{
		foreach (char character in value)
		{
			hash = HashInt(hash, character);
		}
		
		// End the string, so "ab" then "c" never hashes the same as "a" then "bc"
		return HashInt(hash, -1);
	}
	
	private static ulong HashArray(ulong hash, float[]? values)
	{
		// Sanity check
		if (values == null)
		{
			return HashInt(hash, -1);
		}
		
		hash = HashInt(hash, values.Length);
		foreach (float value in values)
		{
			hash = HashFloat(hash, value);
		}
		return hash;
	}
	
	private static string FormatHash(ulong hash)
	{
		return hash.ToString("x16");
	}
	
	private static int BandOf(PlanetLayer layer, int tileId)
	{
		// Truncating the latitude is what picks the band it falls in.
		return Mathf.Clamp((int)(Mathf.Abs(layer.LongLatOf(tileId).y) / BAND_DEGREES), 0, BAND_COUNT - 1);
	}
	
	internal static bool HasRiver(Tile tile)
	{
		return tile is SurfaceTile surfaceTile && surfaceTile.Rivers != null && surfaceTile.Rivers.Count > 0;
	}
	
	/// <summary>Orders biomes from the largest share to the smallest, then by name so ties always list the same way.</summary>
	private static int CompareByShare(BiomeShare first, BiomeShare second)
	{
		int byShare = second.share.CompareTo(first.share);
		return byShare != 0 ? byShare : string.CompareOrdinal(first.biome, second.biome);
	}
	
	private static List<BiomeShare> ToShares(Dictionary<string, int> counts, int land)
	{
		List<BiomeShare> shares = new(counts.Count);
		foreach (KeyValuePair<string, int> entry in counts)
		{
			// Whole tile counts, so one side becomes a float to keep the division from
			// rounding down to nothing.
			shares.Add(new BiomeShare { biome = entry.Key, share = (float)entry.Value / land });
		}
		shares.Sort(CompareByShare);
		return shares;
	}
	
	private static void Count(Dictionary<string, int> counts, string key)
	{
		counts.TryGetValue(key, out int count);
		counts[key] = count + 1;
	}
	
	/// <summary>Hashes every tile's final data, in tile order.</summary>
	private static string FingerprintTiles(List<Tile> tiles)
	{
		ulong hash = FNV_OFFSET;
		foreach (Tile tile in tiles)
		{
			hash = HashFloat(hash, tile.elevation);
			hash = HashInt(hash, (int)tile.hilliness);
			hash = HashFloat(hash, tile.temperature);
			hash = HashFloat(hash, tile.rainfall);
			hash = HashFloat(hash, tile.swampiness);
			hash = HashString(hash, tile.PrimaryBiome?.defName ?? "");
			hash = HashInt(hash, HasRiver(tile) ? 1 : 0);
		}
		return FormatHash(hash);
	}
	
	/// <summary>
	/// Hashes the fields Planetsmith works out during generation but does not keep on
	/// the tiles, which feed the map modes. They are saved at lower precision, so a loaded
	/// world is checked value by value in WorldRoundTrip rather than by this hash.
	/// </summary>
	private static string FingerprintClimate()
	{
		// Sanity check
		if (!PlanetsmithClimateCache.IsValid)
		{
			return "";
		}
		
		ulong hash = FNV_OFFSET;
		hash = HashArray(hash, PlanetsmithClimateCache.Continentality);
		hash = HashArray(hash, PlanetsmithClimateCache.WinterMinimumTemperature);
		hash = HashArray(hash, PlanetsmithClimateCache.SummerMaximumTemperature);
		hash = HashArray(hash, PlanetsmithClimateCache.CoastalAnomaly);
		hash = HashArray(hash, PlanetsmithClimateCache.AridityIndex);
		hash = HashArray(hash, PlanetsmithClimateCache.MonsoonStrength);
		hash = HashArray(hash, PlanetsmithClimateCache.FlowAccumulation);
		hash = HashArray(hash, PlanetsmithClimateCache.RainShadow);
		return FormatHash(hash);
	}
	
	//// Public API
	
	/// <summary>Fills a report with the statistics of a finished world's surface.</summary>
	public static void Measure(World world, WorldReport report)
	{
		PlanetLayer layer = world.grid.Surface;
		List<Tile> tiles = layer.Tiles;
		report.tiles = tiles.Count;
		report.tileFingerprint = FingerprintTiles(tiles);
		report.climateFingerprint = FingerprintClimate();
		
		// Tally the land, overall and by band
		// Land is anything above sea level, as in ClimateReport. Lakes Planetsmith digs are
		// lowered below it, so they count as water here too.
		int[] land = new int[BAND_COUNT];
		int[] arid = new int[BAND_COUNT];
		int[] soaked = new int[BAND_COUNT];
		int[] waterlogged = new int[BAND_COUNT];
		double[] rainfall = new double[BAND_COUNT];
		double[] temperature = new double[BAND_COUNT];
		Dictionary<string, int>[] bandBiomes = new Dictionary<string, int>[BAND_COUNT];
		for (int band = 0; band < BAND_COUNT; band++)
		{
			bandBiomes[band] = new Dictionary<string, int>();
		}
		Dictionary<string, int> biomes = new();
		int rivers = 0;
		for (int tileId = 0; tileId < tiles.Count; tileId++)
		{
			Tile tile = tiles[tileId];
			if (tile.elevation <= 0f)
			{
				continue;
			}
			
			int band = BandOf(layer, tileId);
			land[band]++;
			rainfall[band] += tile.rainfall;
			temperature[band] += tile.temperature;
			if (tile.rainfall < ARID_RAINFALL)
			{
				arid[band]++;
			}
			else if (tile.rainfall >= SOAKED_RAINFALL)
			{
				soaked[band]++;
			}
			if (tile.swampiness >= WATERLOGGED_SWAMPINESS)
			{
				waterlogged[band]++;
			}
			if (HasRiver(tile))
			{
				rivers++;
			}
			
			string biome = tile.PrimaryBiome?.defName ?? "none";
			Count(biomes, biome);
			Count(bandBiomes[band], biome);
		}
		
		// Describe each band
		// Sums are kept in double so a whole planet's rainfall stays exact; a float is
		// plenty for the means that get written.
		int landTotal = 0;
		double rainfallTotal = 0d;
		double temperatureTotal = 0d;
		for (int band = 0; band < BAND_COUNT; band++)
		{
			landTotal += land[band];
			rainfallTotal += rainfall[band];
			temperatureTotal += temperature[band];
			
			BandReport bandReport = new() { band = $"{band * BAND_DEGREES}-{(band + 1) * BAND_DEGREES}", landTiles = land[band] };
			if (land[band] > 0)
			{
				bandReport.meanRainfall = (float)(rainfall[band] / land[band]);
				bandReport.meanTemperature = (float)(temperature[band] / land[band]);
				bandReport.aridShare = (float)arid[band] / land[band];
				bandReport.soakedShare = (float)soaked[band] / land[band];
				bandReport.waterloggedShare = (float)waterlogged[band] / land[band];
				bandReport.biomes = ToShares(bandBiomes[band], land[band]);
			}
			report.bands.Add(bandReport);
		}
		
		// Sum up the planet
		report.landTiles = landTotal;
		if (landTotal > 0)
		{
			report.meanRainfall = (float)(rainfallTotal / landTotal);
			report.meanTemperature = (float)(temperatureTotal / landTotal);
			report.riverShare = (float)rivers / landTotal;
			report.biomes = ToShares(biomes, landTotal);
		}
	}
}
