// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RimWorld.Planet;
using Verse;
using Planetsmith.Generation.Passes;

namespace Planetsmith.Generation;

/// <summary>
/// Entry point for Planetsmith's world-generation work. Invoked after vanilla's
/// terrain step has populated the surface layer; runs the ordered pass pipeline
/// to override climate and biome assignment with Planetsmith's own model. A
/// failing pass is logged and skipped, leaving vanilla's result in place.
/// </summary>
public static class PlanetsmithGeneration
{
	//// Constants
	
	private static readonly IGenerationPass[] PASSES =
	{
		new SeaLevelPass(),
		new ClimatePass(),
		new OrographyPass(),
		new MoistureAdvectionPass(),
		new ContinentalityPass(),
		new OceanCurrentPass(),
		// Seasonality precedes the monsoon, which needs its summer-to-winter swing,
		// and aridity comes after both so it weighs the final rainfall.
		new SeasonalityPass(),
		new MonsoonPass(),
		new AridityPass(),
		// Drainage needs the final rainfall, and swampiness needs the drainage.
		new DrainagePass(),
		new BasinLakePass(),
		new SwampinessPass(),
		new BiomePass(),
	};
	
	//// References and State
	
	private static List<KeyValuePair<string, long>> lastPassTimings = new();
	
	/// <summary>
	/// How many milliseconds each pass took on the last world generated, in the order they
	/// ran. Kept for the test harness, which compares them between builds; a failed pass is
	/// left out.
	/// </summary>
	public static IReadOnlyList<KeyValuePair<string, long>> LastPassTimings => lastPassTimings;
	
	//// Private Functions
	
	/// <summary>
	/// Whether an exception is the game telling us the player has walked away from the
	/// world being built. Backing out of generation aborts the thread it runs on, which
	/// arrives here as a failure like any other and used to be reported as one, in red,
	/// twice over: an aborting thread re-raises itself at the end of every catch it
	/// passes through, so each handler on the way out had its say. Nothing has gone
	/// wrong, and a player who has just pressed Back does not need telling.
	/// </summary>
	private static bool IsCancellation(Exception exception)
	{
		return exception is ThreadAbortException;
	}
	
	//// Public API
	
	/// <summary>Runs every pass over the finished surface, then reports and keeps the climate it made.</summary>
	public static void RunPostTerrain(PlanetLayer layer)
	{
		// Say which planet this is
		// Reports are only comparable between runs of the same seed and settings, and
		// nothing else in the log records them.
		WorldInfo info = Find.World.info;
		Log.Message($"[Planetsmith] Generating seed '{info.seedString}' at {info.planetCoverage:P0} coverage, {info.overallRainfall} rainfall, {info.overallTemperature} temperature.");
		
		// Say where the tilt came from
		GenerationContext context = new(layer);
		if (context.IsTiltHandledExternally)
		{
			// Worth saying out loud: a handover that quietly failed to happen would look
			// exactly like one that worked, right up until the biomes came out describing a
			// different planet from the one being played.
			Log.Message($"[Planetsmith] Axial tilt of {context.AxialTilt:F1}° taken from Realistic Axial Tilt, along with what it does to each latitude's temperature.");
		}
		
		// Run the passes, timing each
		Stopwatch timer = new();
		List<string> timings = new(PASSES.Length);
		List<KeyValuePair<string, long>> passTimings = new(PASSES.Length);
		long total = 0L;
		foreach (IGenerationPass pass in PASSES)
		{
			try
			{
				timer.Restart();
				pass.Run(context);
				timer.Stop();
				total += timer.ElapsedMilliseconds;
				timings.Add($"{pass.Name} {timer.ElapsedMilliseconds}ms");
				passTimings.Add(new KeyValuePair<string, long>(pass.Name, timer.ElapsedMilliseconds));
			}
			catch (Exception exception)
			{
				timer.Stop();
				if (IsCancellation(exception))
				{
					throw;
				}
				Log.Error($"[Planetsmith] Generation pass '{pass.Name}' failed: {exception}");
			}
		}
		
		// Report the time taken
		// World generation already takes a while, so it is worth being able to see at a
		// glance whether Planetsmith is a meaningful part of the wait.
		Log.Message($"[Planetsmith] Generated {context.TileCount} tiles in {total}ms ({string.Join(", ", timings)}).");
		lastPassTimings = passTimings;
		
		// Report the climate and keep it for the overlays
		ClimateReport.LogSummary(context);
		ClimateReport.LogBiomes(context);
		PlanetsmithClimateCache.Store(context);
	}
}
