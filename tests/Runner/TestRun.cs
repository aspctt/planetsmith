// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System;
using System.Diagnostics;
using System.IO;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Planetsmith.TestRunner;

/// <summary>
/// Generates every world in the test job, writes their statistics and closes the game.
/// It runs when the game is started with -planetsmithtest, after loading finishes, and
/// does nothing otherwise.
///
/// The job and results live in a PlanetsmithTest folder inside the save data folder.
/// The test script points -savedatafolder at a folder of its own, so a test run never
/// touches the player's config, mod list or saves, and passing a path through the
/// command line is avoided: the game splits arguments on '=' and would cut a path that
/// has one.
/// </summary>
[StaticConstructorOnStartup]
public static class TestRun
{
	//// Constants
	
	private const string COMMAND_LINE_FLAG = "planetsmithtest";
	private const string FOLDER_NAME = "PlanetsmithTest";
	private const string JOB_FILE = "job.xml";
	private const string RESULTS_FILE = "results.json";
	
	// Written once the results are final. Quitting has taken over three minutes after a
	// finished run, inside Unity with nothing in the log, so the test script closes the
	// game itself once it sees this.
	private const string DONE_FILE = "done";
	
	//// References and State
	
	private static readonly TestResults results = new();
	
	//// Private Functions
	
	private static string FolderPath => Path.Combine(GenFilePaths.SaveDataFolderPath, FOLDER_NAME);
	
	private static void WriteResults()
	{
		File.WriteAllText(Path.Combine(FolderPath, RESULTS_FILE), results.ToJson());
	}
	
	/// <summary>
	/// Starts a fresh game with nothing in it, the way the game's own quick test does
	/// (Root_Play.SetupForQuickTestPlay) up to the point it generates a world. World
	/// generation reads the scenario and the storyteller, so they have to exist, and a new
	/// game for every world keeps one from leaking into the next.
	/// </summary>
	internal static void StartBlankGame()
	{
		Current.ProgramState = ProgramState.Entry;
		Game.ClearCaches();
		Current.Game = new Game();
		Current.Game.InitData = new GameInitData();
		Current.Game.Scenario = ScenarioDefOf.Crashlanded.scenario;
		Find.Scenario.PreConfigure();
		Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
		Find.GameInitData.ResetWorldRelatedMapInitData();
	}
	
	private static void GenerateWorld(TestWorld testWorld, int repeat)
	{
		// Start from Planetsmith's defaults
		// The test folder starts with no mod settings, so these are the defaults a new
		// player gets. Resetting each time keeps one world's parameters out of the next.
		PlanetsmithWorldParameters.ResetToDefaults();
		StartBlankGame();
		
		// Generate the world, timing it
		Stopwatch timer = Stopwatch.StartNew();
		World world = WorldGenerator.GenerateWorld(testWorld.coverage, testWorld.seed, testWorld.rainfall, testWorld.temperature, testWorld.population, testWorld.landmarkDensity);
		Current.Game.World = world;
		timer.Stop();
		
		// Measure it
		WorldReport report = new()
		{
			name = testWorld.name,
			seed = testWorld.seed,
			coverage = testWorld.coverage,
			rainfall = testWorld.rainfall.ToString(),
			temperature = testWorld.temperature.ToString(),
			repeat = repeat,
			seconds = (float)timer.Elapsed.TotalSeconds,
		};
		WorldMeasurement.Measure(world, report);
		if (testWorld.saveAndLoad)
		{
			report.roundTrip = WorldRoundTrip.Check(world, FolderPath);
		}
		ErrorWatcher.TakeInto(report);
		results.worlds.Add(report);
		Log.Message($"[Planetsmith Test] Generated '{testWorld.name}' ({repeat}/{testWorld.repeats}) in {report.seconds:F1}s, tiles {report.tileFingerprint}.");
		
		// Write after every world, so a crash later on still leaves what was finished
		WriteResults();
	}
	
	/// <summary>Runs on a worker thread, as the world creation page does.</summary>
	private static void RunJob()
	{
		// Record what the results came from
		results.gameVersion = VersionControl.CurrentVersionStringWithRev;
		foreach (ModMetaData mod in ModsConfig.ActiveModsInLoadOrder)
		{
			results.mods.Add(mod.PackageIdPlayerFacing);
		}
		
		// Read the job
		string jobPath = Path.Combine(FolderPath, JOB_FILE);
		// Cross-references are left alone: the job names no defs, and resolving them
		// would touch the game's shared list of pending references from this thread.
		TestJob job = DirectXmlLoader.ItemFromXmlFile<TestJob>(jobPath, false);
		if (job.worlds.Count == 0)
		{
			throw new InvalidDataException($"{jobPath} lists no worlds.");
		}
		
		// Generate every world, each as many times as asked
		ErrorWatcher.Start();
		foreach (TestWorld testWorld in job.worlds)
		{
			int repeats = Math.Max(1, testWorld.repeats);
			for (int repeat = 1; repeat <= repeats; repeat++)
			{
				GenerateWorld(testWorld, repeat);
			}
		}
		ErrorWatcher.Stop();
		
		// Finish up
		// The game is left in place: setting it to null made a sound queued during
		// generation throw when it later looked for the tick manager.
		results.isComplete = true;
		WriteResults();
		File.WriteAllText(Path.Combine(FolderPath, DONE_FILE), "");
		LongEventHandler.ExecuteWhenFinished(Root.Shutdown);
	}
	
	/// <summary>
	/// Records why the run stopped and closes the game anyway. Left open, a failed run
	/// would sit at the main menu until the test script gives up on it.
	/// </summary>
	private static void OnRunFailed(Exception exception)
	{
		ErrorWatcher.Stop();
		Log.Error($"[Planetsmith Test] Run failed: {exception}");
		results.failure = exception.ToString();
		try
		{
			WriteResults();
		}
		catch (Exception writeException)
		{
			Log.Error($"[Planetsmith Test] Could not write the results: {writeException}");
		}
		LongEventHandler.ExecuteWhenFinished(Root.Shutdown);
	}
	
	//// Event Wiring
	
	static TestRun()
	{
		// Sanity check
		if (!GenCommandLine.CommandLineArgPassed(COMMAND_LINE_FLAG))
		{
			return;
		}
		
		// Queue the run behind whatever loading is still waiting
		// Static constructors run near the end of loading, while mods such as Faster Game
		// Loading may still have work queued. Long events run in order, so this one starts
		// only once everything before it has finished.
		Log.Message($"[Planetsmith Test] Test run requested, reading {Path.Combine(FolderPath, JOB_FILE)}.");
		LongEventHandler.QueueLongEvent(RunJob, "GeneratingWorld", true, OnRunFailed);
	}
}
