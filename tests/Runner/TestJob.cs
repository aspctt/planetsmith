// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;

namespace Planetsmith.TestRunner;

//// Types

// Job files are XML read with the game's own loader, the one that reads defs, so each
// tag is a field name, lists use <li> and enums are written by name. Fields a job leaves
// out keep the values written here.
//
// They were JSON read with Unity's JsonUtility at first, which came back empty: Unity's
// serializer did not see the classes at all, presumably because the game loads mod
// assemblies itself rather than through Unity.

/// <summary>A test job: the mods to load and the worlds to generate, in order.</summary>
public class TestJob
{
	// A file in tests/profiles, read only by the test script, which writes the mod list.
	public string profile = "core";
	public List<TestWorld> worlds = new();
}

/// <summary>One world to generate, with the same settings the world creation page offers.</summary>
public class TestWorld
{
	// Name used in the results and when comparing against a baseline.
	public string name = "";
	public string seed = "gray";
	public float coverage = 0.3f;
	public OverallRainfall rainfall = OverallRainfall.Normal;
	public OverallTemperature temperature = OverallTemperature.Normal;
	public OverallPopulation population = OverallPopulation.Normal;
	public LandmarkDensity landmarkDensity = LandmarkDensity.Normal;
	
	// Generating the same world more than once checks that a seed always gives the same
	// planet. Every comparison against a baseline depends on that being true.
	public int repeats = 1;
	
	// A Planetsmith preset from tests/presets, by file name without the extension, made
	// the planet being set up as the preset dialog does. Empty keeps the defaults.
	public string preset = "";
	
	// Saves the world and loads it back, then checks the loaded tiles and Planetsmith's
	// climate fields against the ones generated.
	public bool saveAndLoad;
}
