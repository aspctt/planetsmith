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

		/// <summary>When set, Worldsmith moves sea level to hit <see cref="targetLandFraction"/>.</summary>
		public bool enableSeaLevelControl = false;

		/// <summary>Fraction of the planet's surface that should be dry land.</summary>
		public float targetLandFraction = 0.4f;

		/// <summary>Planet's axial tilt in degrees. Earth is 23.4; 0 means no seasons.</summary>
		public float axialTilt = 23.4f;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref enableClimateOverhaul, "enableClimateOverhaul", defaultValue: true);
			Scribe_Values.Look(ref enableSeaLevelControl, "enableSeaLevelControl", defaultValue: false);
			Scribe_Values.Look(ref targetLandFraction, "targetLandFraction", 0.4f);
			Scribe_Values.Look(ref axialTilt, "axialTilt", 23.4f);
		}
	}
}
