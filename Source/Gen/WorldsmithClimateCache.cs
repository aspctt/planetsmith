// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
namespace Worldsmith.Gen
{
	/// <summary>
	/// Holds the last-generated derived climate fields (continentality and seasonal
	/// extremes) in memory so the debug overlays can display layers that aren't stored
	/// on the tile itself. This is a dev/inspection aid, not saved with the world: it
	/// is refreshed each time a world is generated and is empty after a reload until
	/// the next generation.
	/// </summary>
	public static class WorldsmithClimateCache
	{
		public static bool Valid { get; private set; }
		public static int TileCount { get; private set; }
		public static float[] Continentality { get; private set; }
		public static float[] WinterMinTemp { get; private set; }
		public static float[] SummerMaxTemp { get; private set; }
		public static float[] CoastalAnomaly { get; private set; }
		public static float[] AridityIndex { get; private set; }
		public static float[] MonsoonStrength { get; private set; }
		public static float[] FlowAccumulation { get; private set; }

		public static void Store(GenContext ctx)
		{
			TileCount = ctx.TileCount;
			Continentality = ctx.Continentality;
			WinterMinTemp = ctx.WinterMinTemp;
			SummerMaxTemp = ctx.SummerMaxTemp;
			CoastalAnomaly = ctx.CoastalAnomaly;
			AridityIndex = ctx.AridityIndex;
			MonsoonStrength = ctx.MonsoonStrength;
			FlowAccumulation = ctx.FlowAccumulation;
			Valid = true;
		}
	}
}
