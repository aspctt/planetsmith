// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using Verse;

namespace Worldsmith
{
	/// <summary>
	/// Persistent, global mod settings. Fields are added here as features land;
	/// per-world generation parameters are stored separately with the save.
	/// </summary>
	public class WorldsmithSettings : ModSettings
	{
		/// <summary>Master toggle for the climate and biome generation overhaul.</summary>
		public bool enableClimateOverhaul = true;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref enableClimateOverhaul, "enableClimateOverhaul", defaultValue: true);
		}
	}
}
