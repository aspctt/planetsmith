// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Worldsmith
{
	/// <summary>
	/// Runs once when RimWorld finishes loading assemblies. Applies all Harmony
	/// patches declared in this assembly and confirms the mod is live.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class WorldsmithBootstrap
	{
		static WorldsmithBootstrap()
		{
			var harmony = new Harmony(WorldsmithMod.PackageId);
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			Log.Message("[Worldsmith] Loaded and patched.");
		}
	}
}
