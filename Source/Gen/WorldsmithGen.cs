// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System;
using System.Collections.Generic;
using RimWorld.Planet;
using Verse;
using Worldsmith.Gen.Passes;

namespace Worldsmith.Gen
{
	/// <summary>
	/// Entry point for Worldsmith's world-generation work. Invoked after vanilla's
	/// terrain step has populated the surface layer; runs the ordered pass pipeline
	/// to override climate and biome assignment with Worldsmith's own model. A
	/// failing pass is logged and skipped, leaving vanilla's result in place.
	/// </summary>
	public static class WorldsmithGen
	{
		private static readonly List<IGenPass> Passes = new List<IGenPass>
		{
			new ClimatePass(),
			new OrographyPass(),
			new MoistureAdvectionPass(),
			new SeasonalityPass(),
			new SwampinessPass(),
			new BiomePass(),
		};

		public static void RunPostTerrain(PlanetLayer layer)
		{
			var ctx = new GenContext(layer);
			for (int i = 0; i < Passes.Count; i++)
			{
				IGenPass pass = Passes[i];
				try
				{
					pass.Run(ctx);
				}
				catch (Exception e)
				{
					Log.Error($"[Worldsmith] Generation pass '{pass.Name}' failed: {e}");
				}
			}

			// Keep the derived climate fields around for the debug overlays.
			WorldsmithClimateCache.Store(ctx);
		}
	}
}
