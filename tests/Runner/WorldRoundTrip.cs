// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Planetsmith.Generation;

namespace Planetsmith.TestRunner;

/// <summary>
/// Saves a generated world and loads it back, then checks what came back against what
/// was generated.
///
/// Only the world goes through the round trip, not a whole game. Loading a game needs
/// the play scene (Game.LoadGame reaches for the camera driver), while the world is
/// saved and loaded by the same calls inside it: World.ExposeData, then the cross
/// references, FinalizeInit and FinalizeLoading, in the order Game.LoadGame uses. Every
/// world component's ExposeData runs, Planetsmith's included, which is what matters.
/// </summary>
public static class WorldRoundTrip
{
	//// Types
	
	/// <summary>The generated tiles' values, kept to compare against once the world is gone.</summary>
	private sealed class TileSnapshot
	{
		public readonly float[] Elevation;
		public readonly int[] Hilliness;
		public readonly float[] Temperature;
		public readonly float[] Rainfall;
		public readonly float[] Swampiness;
		public readonly BiomeDef?[] Biome;
		public readonly bool[] HasRiver;
		
		public TileSnapshot(List<Tile> tiles)
		{
			int count = tiles.Count;
			Elevation = new float[count];
			Hilliness = new int[count];
			Temperature = new float[count];
			Rainfall = new float[count];
			Swampiness = new float[count];
			Biome = new BiomeDef?[count];
			HasRiver = new bool[count];
			for (int tileId = 0; tileId < count; tileId++)
			{
				Tile tile = tiles[tileId];
				Elevation[tileId] = tile.elevation;
				Hilliness[tileId] = (int)tile.hilliness;
				Temperature[tileId] = tile.temperature;
				Rainfall[tileId] = tile.rainfall;
				Swampiness[tileId] = tile.swampiness;
				Biome[tileId] = tile.PrimaryBiome;
				HasRiver[tileId] = WorldMeasurement.HasRiver(tile);
			}
		}
	}
	
	//// Constants
	
	private const string SAVE_FILE = "roundtrip.xml";
	private const string OLD_SAVE_FILE = "roundtrip-old.xml";
	
	// The node Planetsmith saves its climate fields under, taken out to make an old save.
	private const string CLIMATE_NODE = "climateFields";
	
	// How far a climate field's value may come back from where it was generated.
	// Planetsmith stores them as 16-bit floats, which keep 11 significant bits, so a value
	// moves by at most one part in 2048 of itself. The floor covers values so close to 0
	// that the format stores them with fewer bits.
	private const float CLIMATE_RELATIVE_TOLERANCE = 0.001f;
	private const float CLIMATE_ABSOLUTE_TOLERANCE = 0.000001f;
	
	// How far a value may come back from where it was generated. The game rounds tile
	// data as it saves it (SurfaceLayer.TilesToRawData): elevation to whole metres with
	// land raised to at least 1, temperature to tenths of a degree, rainfall to whole
	// millimetres and swampiness to 255 steps. Each tolerance is that rounding plus a
	// sliver, so only a change beyond what the format itself does counts.
	private const float ELEVATION_TOLERANCE = 1.001f;
	private const float TEMPERATURE_TOLERANCE = 0.051f;
	private const float RAINFALL_TOLERANCE = 0.501f;
	private const float SWAMPINESS_TOLERANCE = 0.501f / 255f;
	
	//// Private Functions
	
	private static void Save(World world, string path)
	{
		try
		{
			// Write a header like a real save's, so loading reads the same version info
			Scribe.saver.InitSaving(path, "world");
			ScribeMetaHeaderUtility.WriteMetaHeader();
			world.ExposeData();
			Scribe.saver.FinalizeSaving();
		}
		catch
		{
			// Leaving the scribe mid-save would break every later save and load
			Scribe.ForceStop();
			throw;
		}
	}
	
	private static World Load(string path)
	{
		// Load into a fresh game, so nothing of the generated world is left to lean on
		TestRun.StartBlankGame();
		
		// The game is in this state while it reads a save, and some loading code checks it
		Current.ProgramState = ProgramState.MapInitializing;
		try
		{
			Scribe.loader.InitLoading(path);
			ScribeMetaHeaderUtility.LoadGameDataHeader(ScribeMetaHeaderUtility.ScribeHeaderMode.Map, false);
			
			// Read the world, in Game.LoadGame's order
			// The world is handed to the game before it reads itself, as there, because
			// loading code finds it through Find.World.
			World world = new();
			Current.Game.World = world;
			world.ExposeData();
			Scribe.loader.crossRefs.RegisterForCrossRefResolve(world);
			world.FinalizeInit(true);
			Scribe.loader.FinalizeLoading();
			return world;
		}
		catch
		{
			Scribe.ForceStop();
			throw;
		}
		finally
		{
			Current.ProgramState = ProgramState.Entry;
		}
	}
	
	/// <summary>Counts the tiles that came back further off than the save's rounding allows.</summary>
	private static void CompareTiles(TileSnapshot before, List<Tile> after, RoundTripReport report)
	{
		// Sanity check
		if (after.Count != before.Elevation.Length)
		{
			throw new InvalidDataException($"The loaded world has {after.Count} tiles, the generated one had {before.Elevation.Length}.");
		}
		
		report.tilesChecked = after.Count;
		for (int tileId = 0; tileId < after.Count; tileId++)
		{
			Tile tile = after[tileId];
			int changes = 0;
			if (Mathf.Abs(tile.elevation - before.Elevation[tileId]) > ELEVATION_TOLERANCE)
			{
				report.elevation++;
				changes++;
			}
			if ((int)tile.hilliness != before.Hilliness[tileId])
			{
				report.hilliness++;
				changes++;
			}
			if (Mathf.Abs(tile.temperature - before.Temperature[tileId]) > TEMPERATURE_TOLERANCE)
			{
				report.temperature++;
				changes++;
			}
			if (Mathf.Abs(tile.rainfall - before.Rainfall[tileId]) > RAINFALL_TOLERANCE)
			{
				report.rainfall++;
				changes++;
			}
			if (Mathf.Abs(tile.swampiness - before.Swampiness[tileId]) > SWAMPINESS_TOLERANCE)
			{
				report.swampiness++;
				changes++;
			}
			if (tile.PrimaryBiome != before.Biome[tileId])
			{
				report.biome++;
				changes++;
			}
			if (WorldMeasurement.HasRiver(tile) != before.HasRiver[tileId])
			{
				report.rivers++;
				changes++;
			}
			if (changes > 0)
			{
				report.tilesChanged++;
			}
		}
	}
	
	/// <summary>Planetsmith's climate fields as they stand, in a fixed order.</summary>
	private static float[]?[] ClimateFields()
	{
		return new[]
		{
			PlanetsmithClimateCache.Continentality,
			PlanetsmithClimateCache.WinterMinimumTemperature,
			PlanetsmithClimateCache.SummerMaximumTemperature,
			PlanetsmithClimateCache.CoastalAnomaly,
			PlanetsmithClimateCache.AridityIndex,
			PlanetsmithClimateCache.MonsoonStrength,
			PlanetsmithClimateCache.FlowAccumulation,
			PlanetsmithClimateCache.RainShadow,
		};
	}
	
	/// <summary>Counts the climate values that came back further off than their storage allows.</summary>
	private static void CompareClimate(float[]?[] before, RoundTripReport report)
	{
		// Sanity check
		report.climateRestored = PlanetsmithClimateCache.IsValid;
		if (!report.climateRestored)
		{
			return;
		}
		
		float[]?[] after = ClimateFields();
		for (int field = 0; field < before.Length; field++)
		{
			float[]? generated = before[field];
			float[]? loaded = after[field];
			if (generated == null || loaded == null || generated.Length != loaded.Length)
			{
				report.climateRestored = false;
				return;
			}
			
			for (int tileId = 0; tileId < generated.Length; tileId++)
			{
				float tolerance = Mathf.Abs(generated[tileId]) * CLIMATE_RELATIVE_TOLERANCE + CLIMATE_ABSOLUTE_TOLERANCE;
				if (Mathf.Abs(loaded[tileId] - generated[tileId]) > tolerance)
				{
					report.climateValuesChanged++;
				}
			}
		}
	}
	
	/// <summary>Writes a copy of a save with the climate fields taken out, as a save from before they were stored.</summary>
	private static void WriteOldSave(string path, string oldPath)
	{
		XmlDocument document = new();
		document.Load(path);
		XmlNodeList nodes = document.GetElementsByTagName(CLIMATE_NODE);
		
		// Removing a node changes the live list, so they are collected first
		List<XmlNode> found = new();
		foreach (XmlNode node in nodes)
		{
			found.Add(node);
		}
		foreach (XmlNode node in found)
		{
			node.ParentNode?.RemoveChild(node);
		}
		document.Save(oldPath);
	}
	
	//// Public API
	
	/// <summary>
	/// Saves the world, loads it into a fresh game and reports what changed. The game
	/// holds the loaded world afterwards, not the one passed in.
	/// </summary>
	public static RoundTripReport Check(World generated, string folderPath)
	{
		// Remember the world as generated
		// The climate fields are held by reference: loading puts new arrays in the cache
		// rather than writing into these.
		RoundTripReport report = new() { isChecked = true };
		TileSnapshot before = new(generated.grid.Surface.Tiles);
		float[]?[] climateBefore = ClimateFields();
		
		// Save and load it, timing both
		Stopwatch timer = Stopwatch.StartNew();
		string path = Path.Combine(folderPath, SAVE_FILE);
		Save(generated, path);
		World loaded = Load(path);
		timer.Stop();
		report.seconds = (float)timer.Elapsed.TotalSeconds;
		
		// Compare what came back
		CompareTiles(before, loaded.grid.Surface.Tiles, report);
		CompareClimate(climateBefore, report);
		
		// Load it again as a save from before the climate fields were stored
		// Players keep loading those, so they have to load without errors and leave the
		// map modes that need the fields unavailable rather than showing stale data.
		string oldPath = Path.Combine(folderPath, OLD_SAVE_FILE);
		WriteOldSave(path, oldPath);
		Load(oldPath);
		report.oldSaveChecked = true;
		report.oldSaveHasNoClimate = !PlanetsmithClimateCache.IsValid;
		return report;
	}
}
