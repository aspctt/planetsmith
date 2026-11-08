// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Planetsmith.TestRunner;

//// Types

/// <summary>Everything one test run produced.</summary>
public class TestResults
{
	public string gameVersion = "";
	public List<string> mods = new();
	
	// Set only once every world has been written. A results file without it came from a
	// run that crashed or was killed partway.
	public bool isComplete;
	public string failure = "";
	public List<WorldReport> worlds = new();
	
	/// <summary>
	/// Writes the results as JSON, which the test script reads with ConvertFrom-Json.
	/// Written by hand because Unity's JsonUtility cannot see the game's mod assemblies,
	/// and the game ships no other JSON library.
	/// </summary>
	public string ToJson()
	{
		JsonWriter writer = new();
		writer.BeginObject();
		writer.Property("gameVersion", gameVersion);
		writer.Property("mods", mods);
		writer.Property("isComplete", isComplete);
		writer.Property("failure", failure);
		writer.BeginArray("worlds");
		foreach (WorldReport world in worlds)
		{
			world.Write(writer);
		}
		writer.EndArray();
		writer.EndObject();
		return writer.ToString();
	}
}

/// <summary>Statistics for one generated world.</summary>
public class WorldReport
{
	public string name = "";
	public string seed = "";
	public float coverage;
	public string rainfall = "";
	public string temperature = "";
	public string preset = "";
	
	// Which of the job's repeats this is, counted from 1.
	public int repeat;
	public float seconds;
	
	// Hashes of every tile's final data and of Planetsmith's generation-only climate
	// fields. Equal hashes mean the worlds are identical, so they settle determinism and
	// show at a glance whether a change touched a world at all.
	public string tileFingerprint = "";
	public string climateFingerprint = "";
	
	public int tiles;
	public int landTiles;
	public float meanTemperature;
	public float meanRainfall;
	public float riverShare;
	public List<BiomeShare> biomes = new();
	public List<BandReport> bands = new();
	
	public RoundTripReport roundTrip = new();
	
	// Errors logged while this world was generated, saved or loaded, by any mod.
	public int errorCount;
	public List<string> errors = new();
	
	public void Write(JsonWriter writer)
	{
		writer.BeginObject();
		writer.Property("name", name);
		writer.Property("seed", seed);
		writer.Property("coverage", coverage);
		writer.Property("rainfall", rainfall);
		writer.Property("temperature", temperature);
		writer.Property("preset", preset);
		writer.Property("repeat", repeat);
		writer.Property("seconds", seconds);
		writer.Property("tileFingerprint", tileFingerprint);
		writer.Property("climateFingerprint", climateFingerprint);
		writer.Property("tiles", tiles);
		writer.Property("landTiles", landTiles);
		writer.Property("meanTemperature", meanTemperature);
		writer.Property("meanRainfall", meanRainfall);
		writer.Property("riverShare", riverShare);
		BiomeShare.WriteAll(writer, "biomes", biomes);
		writer.BeginArray("bands");
		foreach (BandReport band in bands)
		{
			band.Write(writer);
		}
		writer.EndArray();
		roundTrip.Write(writer);
		writer.Property("errorCount", errorCount);
		writer.Property("errors", errors);
		writer.EndObject();
	}
}

/// <summary>
/// What survived saving a world and loading it back. Always written, with isChecked
/// false for worlds the job did not ask to check, so the test script can read it
/// without testing whether it exists.
/// </summary>
public class RoundTripReport
{
	public bool isChecked;
	public float seconds;
	public int tilesChecked;
	
	// Tiles where any value came back further off than the save's own rounding allows,
	// then how many tiles each value was off on.
	public int tilesChanged;
	public int elevation;
	public int hilliness;
	public int temperature;
	public int rainfall;
	public int swampiness;
	public int biome;
	public int rivers;
	
	// Planetsmith's climate fields as generated and as loaded. Empty after loading
	// while they are not stored in saves.
	public string climateBefore = "";
	public string climateAfter = "";
	
	public void Write(JsonWriter writer)
	{
		writer.BeginObject("roundTrip");
		writer.Property("isChecked", isChecked);
		writer.Property("seconds", seconds);
		writer.Property("tilesChecked", tilesChecked);
		writer.Property("tilesChanged", tilesChanged);
		writer.Property("elevation", elevation);
		writer.Property("hilliness", hilliness);
		writer.Property("temperature", temperature);
		writer.Property("rainfall", rainfall);
		writer.Property("swampiness", swampiness);
		writer.Property("biome", biome);
		writer.Property("rivers", rivers);
		writer.Property("climateBefore", climateBefore);
		writer.Property("climateAfter", climateAfter);
		writer.EndObject();
	}
}

/// <summary>The land in one band of latitude, north and south together.</summary>
public class BandReport
{
	public string band = "";
	public int landTiles;
	public float meanRainfall;
	public float meanTemperature;
	public float aridShare;
	public float soakedShare;
	public float waterloggedShare;
	public List<BiomeShare> biomes = new();
	
	public void Write(JsonWriter writer)
	{
		writer.BeginObject();
		writer.Property("band", band);
		writer.Property("landTiles", landTiles);
		writer.Property("meanRainfall", meanRainfall);
		writer.Property("meanTemperature", meanTemperature);
		writer.Property("aridShare", aridShare);
		writer.Property("soakedShare", soakedShare);
		writer.Property("waterloggedShare", waterloggedShare);
		BiomeShare.WriteAll(writer, "biomes", biomes);
		writer.EndObject();
	}
}

/// <summary>How much of an area's land a biome took.</summary>
public class BiomeShare
{
	public string biome = "";
	public float share;
	
	public static void WriteAll(JsonWriter writer, string name, List<BiomeShare> shares)
	{
		writer.BeginArray(name);
		foreach (BiomeShare share in shares)
		{
			writer.BeginObject();
			writer.Property("biome", share.biome);
			writer.Property("share", share.share);
			writer.EndObject();
		}
		writer.EndArray();
	}
}

/// <summary>
/// Just enough of a JSON writer for the results: objects, arrays, strings, numbers and
/// booleans, indented with tabs so a results file can be read and diffed by hand.
/// </summary>
public class JsonWriter
{
	//// References and State
	
	private readonly StringBuilder builder = new();
	private int depth;
	
	// Whether the object or array being written already holds a value, which decides
	// whether the next one needs a comma before it.
	private bool hasValue;
	
	//// Private Functions
	
	private void BeginValue(string? name)
	{
		if (hasValue)
		{
			builder.Append(',');
		}
		if (depth > 0)
		{
			builder.Append('\n').Append('\t', depth);
		}
		if (name != null)
		{
			AppendString(name);
			builder.Append(": ");
		}
		hasValue = true;
	}
	
	private void Open(string? name, char bracket)
	{
		BeginValue(name);
		builder.Append(bracket);
		depth++;
		hasValue = false;
	}
	
	private void Close(char bracket)
	{
		depth--;
		builder.Append('\n').Append('\t', depth).Append(bracket);
		hasValue = true;
	}
	
	private void AppendString(string value)
	{
		builder.Append('"');
		foreach (char character in value)
		{
			switch (character)
			{
				case '"': builder.Append("\\\""); break;
				case '\\': builder.Append("\\\\"); break;
				case '\n': builder.Append("\\n"); break;
				case '\r': builder.Append("\\r"); break;
				case '\t': builder.Append("\\t"); break;
				default:
					// Any other control character has to be escaped as a code point, or the
					// file stops being valid JSON. Error messages can carry them.
					if (character < ' ')
					{
						builder.Append("\\u").Append(((int)character).ToString("x4"));
					}
					else
					{
						builder.Append(character);
					}
					break;
			}
		}
		builder.Append('"');
	}
	
	//// Public API
	
	public void BeginObject(string? name = null) => Open(name, '{');
	public void EndObject() => Close('}');
	public void BeginArray(string name) => Open(name, '[');
	public void EndArray() => Close(']');
	
	public void Property(string name, string value)
	{
		BeginValue(name);
		AppendString(value);
	}
	
	public void Property(string name, bool value)
	{
		BeginValue(name);
		builder.Append(value ? "true" : "false");
	}
	
	public void Property(string name, int value)
	{
		BeginValue(name);
		builder.Append(value.ToString(CultureInfo.InvariantCulture));
	}
	
	public void Property(string name, float value)
	{
		// "R" keeps every digit a float holds, and the invariant culture keeps a decimal
		// point where the player's locale would put a comma.
		BeginValue(name);
		builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
	}
	
	public void Property(string name, List<string> values)
	{
		BeginArray(name);
		foreach (string value in values)
		{
			BeginValue(null);
			AppendString(value);
		}
		EndArray();
	}
	
	public override string ToString() => builder.ToString();
}
