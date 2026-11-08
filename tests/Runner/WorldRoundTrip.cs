// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

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
	
	//// Public API
	
	/// <summary>
	/// Saves the world, loads it into a fresh game and reports what changed. The game
	/// holds the loaded world afterwards, not the one passed in.
	/// </summary>
	public static RoundTripReport Check(World generated, string folderPath)
	{
		// Remember the world as generated
		RoundTripReport report = new() { isChecked = true, climateBefore = WorldMeasurement.FingerprintClimate() };
		TileSnapshot before = new(generated.grid.Surface.Tiles);
		
		// Save and load it, timing both
		Stopwatch timer = Stopwatch.StartNew();
		string path = Path.Combine(folderPath, SAVE_FILE);
		Save(generated, path);
		World loaded = Load(path);
		timer.Stop();
		report.seconds = (float)timer.Elapsed.TotalSeconds;
		
		// Compare what came back
		// The climate cache is read after loading because loading is what has to refill
		// it. Until the map mode fields are saved, it comes back empty.
		CompareTiles(before, loaded.grid.Surface.Tiles, report);
		report.climateAfter = WorldMeasurement.FingerprintClimate();
		return report;
	}
}
