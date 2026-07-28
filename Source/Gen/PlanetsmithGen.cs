// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System;
using System.Collections.Generic;
using RimWorld.Planet;
using Verse;
using Planetsmith.Gen.Passes;

namespace Planetsmith.Gen
{
	/// <summary>
	/// Entry point for Planetsmith's world-generation work. Invoked after vanilla's
	/// terrain step has populated the surface layer; runs the ordered pass pipeline
	/// to override climate and biome assignment with Planetsmith's own model. A
	/// failing pass is logged and skipped, leaving vanilla's result in place.
	/// </summary>
	public static class PlanetsmithGen
	{
		private static readonly List<IGenPass> Passes = new List<IGenPass>
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

		public static void RunPostTerrain(PlanetLayer layer)
		{
			var ctx = new GenContext(layer);
			var timer = new System.Diagnostics.Stopwatch();
			var timings = new System.Text.StringBuilder();
			long total = 0L;

			for (int i = 0; i < Passes.Count; i++)
			{
				IGenPass pass = Passes[i];
				try
				{
					timer.Restart();
					pass.Run(ctx);
					timer.Stop();
					total += timer.ElapsedMilliseconds;
					timings.Append($"{pass.Name} {timer.ElapsedMilliseconds}ms, ");
				}
				catch (Exception e)
				{
					timer.Stop();
					Log.Error($"[Planetsmith] Generation pass '{pass.Name}' failed: {e}");
				}
			}

			// World generation already takes a while, so it is worth being able to see at
			// a glance whether Planetsmith is a meaningful part of the wait.
			Log.Message($"[Planetsmith] Generated {ctx.TileCount} tiles in {total}ms ({timings.ToString().TrimEnd(' ', ',')}).");

			// Keep the derived climate fields around for the debug overlays.
			PlanetsmithClimateCache.Store(ctx);
		}
	}
}
