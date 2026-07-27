// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System;
using HarmonyLib;
using RimWorld.Planet;
using Verse;
using Worldsmith.Compat;
using Worldsmith.Gen;

namespace Worldsmith.Patches
{
	/// <summary>
	/// After vanilla builds the surface terrain, hand the layer to Worldsmith so it
	/// can re-derive climate and biomes. Runs only on the root surface layer and
	/// only when the overhaul is enabled; any failure is logged and vanilla's
	/// already-generated result is left untouched.
	/// </summary>
	[HarmonyPatch(typeof(WorldGenStep_Terrain), nameof(WorldGenStep_Terrain.GenerateFresh))]
	public static class Patch_WorldGenStep_Terrain
	{
		public static void Postfix(PlanetLayer layer)
		{
			var settings = WorldsmithMod.Settings;
			if (settings == null || !settings.enableClimateOverhaul)
			{
				return;
			}
			if (layer == null || !layer.IsRootSurface)
			{
				return;
			}
			if (ModCompat.AlienWorldsCustomPlanetActive())
			{
				Log.Message("[Worldsmith] An AlienWorlds planet type is active; deferring climate overhaul to it.");
				return;
			}
			try
			{
				WorldsmithGen.RunPostTerrain(layer);
			}
			catch (Exception e)
			{
				Log.Error($"[Worldsmith] World generation override failed; keeping vanilla result. {e}");
			}
		}
	}
}
