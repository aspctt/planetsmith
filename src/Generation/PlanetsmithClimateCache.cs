// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using UnityEngine;
using Verse;

namespace Planetsmith.Generation;

/// <summary>
/// Holds the climate fields worked out while a world is generated that are not kept on
/// the tiles themselves (continentality, seasonal extremes, coastal currents, aridity,
/// monsoon, drainage and rain shadow), so the map modes can show them. They are filled
/// as a world is generated and saved with it, so loading the save brings them back.
/// Worlds saved before they were stored have none, and those map modes stay unavailable.
/// </summary>
public static class PlanetsmithClimateCache
{
	//// Constants
	
	private const string SAVE_NODE = "climateFields";
	
	// The label each field is saved under, in the order Fields() lists them. Never rename
	// one: a save made before the rename would load without that field, and since every
	// field has to be present, it would lose its map modes altogether.
	private static readonly string[] SAVE_LABELS =
	{
		"continentality",
		"winterMinimumTemperature",
		"summerMaximumTemperature",
		"coastalAnomaly",
		"aridityIndex",
		"monsoonStrength",
		"flowAccumulation",
		"rainShadow",
	};
	
	//// References and State
	
	public static bool IsValid { get; private set; }
	public static float[]? Continentality { get; private set; }
	public static float[]? WinterMinimumTemperature { get; private set; }
	public static float[]? SummerMaximumTemperature { get; private set; }
	public static float[]? CoastalAnomaly { get; private set; }
	public static float[]? AridityIndex { get; private set; }
	public static float[]? MonsoonStrength { get; private set; }
	public static float[]? FlowAccumulation { get; private set; }
	public static float[]? RainShadow { get; private set; }
	
	// The fields as read from a save, held from when the world's data is read until
	// loading finishes and the world they belong to can be checked against them.
	private static byte[]?[]? loadedFields;
	
	//// Private Functions
	
	/// <summary>Every field, in the order SAVE_LABELS names them.</summary>
	private static float[]?[] Fields()
	{
		return new[]
		{
			Continentality,
			WinterMinimumTemperature,
			SummerMaximumTemperature,
			CoastalAnomaly,
			AridityIndex,
			MonsoonStrength,
			FlowAccumulation,
			RainShadow,
		};
	}
	
	/// <summary>
	/// Packs a field as 16-bit floats. They keep about three significant digits, far
	/// finer than any map mode can show, at half the size of full floats; a world at full
	/// coverage has over half a million tiles, and each field costs two bytes for every
	/// one of them before compression. Every field fits a 16-bit float's range: the
	/// temperatures and anomalies are in degrees and the rest run from 0 to a few.
	/// </summary>
	private static byte[] Encode(float[] values)
	{
		ushort[] halves = new ushort[values.Length];
		for (int index = 0; index < values.Length; index++)
		{
			halves[index] = Mathf.FloatToHalf(values[index]);
		}
		return DataSerializeUtility.SerializeUshort(halves);
	}
	
	private static float[] Decode(byte[] bytes)
	{
		ushort[] halves = DataSerializeUtility.DeserializeUshort(bytes);
		float[] values = new float[halves.Length];
		for (int index = 0; index < halves.Length; index++)
		{
			values[index] = Mathf.HalfToFloat(halves[index]);
		}
		return values;
	}
	
	/// <summary>The number of tiles on the surface the fields cover, or 0 when there is no world.</summary>
	private static int SurfaceTileCount()
	{
		return Find.WorldGrid?.Surface?.TilesCount ?? 0;
	}
	
	private static void SaveFields()
	{
		// Sanity check
		// A world loaded from a save made before the fields were stored has none, and
		// writes none, so it keeps offering to regenerate rather than saving empty data.
		if (!IsValid)
		{
			return;
		}
		
		// Make sure every field belongs to this world before writing any
		float[]?[] fields = Fields();
		int tileCount = SurfaceTileCount();
		foreach (float[]? field in fields)
		{
			if (field == null || field.Length != tileCount)
			{
				Log.Warning($"[Planetsmith] Not saving the climate fields: they do not match this world's {tileCount} tiles.");
				return;
			}
		}
		
		// Write them
		if (!Scribe.EnterNode(SAVE_NODE))
		{
			return;
		}
		try
		{
			for (int index = 0; index < fields.Length; index++)
			{
				byte[] bytes = Encode(fields[index]!);
				DataExposeUtility.LookByteArray(ref bytes, SAVE_LABELS[index]);
			}
		}
		finally
		{
			Scribe.ExitNode();
		}
	}
	
	private static void ReadFields()
	{
		// Sanity check
		// A save made before the fields were stored has no node at all.
		loadedFields = null;
		if (!Scribe.EnterNode(SAVE_NODE))
		{
			return;
		}
		
		try
		{
			loadedFields = new byte[]?[SAVE_LABELS.Length];
			for (int index = 0; index < SAVE_LABELS.Length; index++)
			{
				byte[]? bytes = null;
				DataExposeUtility.LookByteArray(ref bytes, SAVE_LABELS[index]);
				loadedFields[index] = bytes;
			}
		}
		finally
		{
			Scribe.ExitNode();
		}
	}
	
	/// <summary>
	/// Puts the fields read from a save back, once the whole world has loaded. Only a
	/// complete set that matches the world's tiles is used: half a set, or one from a
	/// grid of another size, would paint the wrong climate on the map.
	/// </summary>
	private static void RestoreFields()
	{
		// Sanity check
		byte[]?[]? loaded = loadedFields;
		loadedFields = null;
		if (loaded == null)
		{
			return;
		}
		
		// Unpack every field, checking each against the world
		int tileCount = SurfaceTileCount();
		float[][] fields = new float[loaded.Length][];
		for (int index = 0; index < loaded.Length; index++)
		{
			byte[]? bytes = loaded[index];
			if (bytes == null)
			{
				Log.Warning($"[Planetsmith] The saved climate fields are missing '{SAVE_LABELS[index]}', so the map modes that need them stay unavailable.");
				return;
			}
			
			fields[index] = Decode(bytes);
			if (fields[index].Length != tileCount)
			{
				Log.Warning($"[Planetsmith] The saved climate fields cover {fields[index].Length} tiles but this world has {tileCount}, so the map modes that need them stay unavailable.");
				return;
			}
		}
		
		// Use them
		Continentality = fields[0];
		WinterMinimumTemperature = fields[1];
		SummerMaximumTemperature = fields[2];
		CoastalAnomaly = fields[3];
		AridityIndex = fields[4];
		MonsoonStrength = fields[5];
		FlowAccumulation = fields[6];
		RainShadow = fields[7];
		IsValid = true;
	}
	
	//// Public API
	
	/// <summary>
	/// Forgets the last world's fields. Called as each world is built, including one
	/// being loaded from a save, before that world's own fields are generated or read.
	/// Without this the overlays would happily paint one planet's climate onto another
	/// of the same size.
	/// </summary>
	public static void Invalidate()
	{
		IsValid = false;
		Continentality = null;
		WinterMinimumTemperature = null;
		SummerMaximumTemperature = null;
		CoastalAnomaly = null;
		AridityIndex = null;
		MonsoonStrength = null;
		FlowAccumulation = null;
		RainShadow = null;
		loadedFields = null;
	}
	
	public static void Store(GenerationContext context)
	{
		Continentality = context.Continentality;
		WinterMinimumTemperature = context.WinterMinimumTemperature;
		SummerMaximumTemperature = context.SummerMaximumTemperature;
		CoastalAnomaly = context.CoastalAnomaly;
		AridityIndex = context.AridityIndex;
		MonsoonStrength = context.MonsoonStrength;
		FlowAccumulation = context.FlowAccumulation;
		RainShadow = context.RainShadow;
		IsValid = true;
	}
	
	/// <summary>
	/// Saves the fields with the world, or reads them back while that world loads. Called
	/// from the world component's ExposeData, so it runs in each of the game's save and
	/// load passes: the data is read in the first and only checked and used in the last,
	/// once the world it has to match exists.
	/// </summary>
	public static void ExposeData()
	{
		switch (Scribe.mode)
		{
			case LoadSaveMode.Saving:
				SaveFields();
				break;
			case LoadSaveMode.LoadingVars:
				ReadFields();
				break;
			case LoadSaveMode.PostLoadInit:
				RestoreFields();
				break;
		}
	}
}
