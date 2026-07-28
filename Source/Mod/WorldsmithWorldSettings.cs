// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using Verse;

namespace Worldsmith
{
	/// <summary>
	/// The generation parameters belonging to one particular planet, as opposed to the
	/// mod-wide preferences that seed them. These travel with the world: they are chosen
	/// on the world creation page, used while generating, and saved into the game.
	/// </summary>
	public class WorldsmithWorldSettings : IExposable
	{
		public bool enableSeaLevelControl;
		public float targetLandFraction;
		public float axialTilt;

		public WorldsmithWorldSettings()
		{
			CopyFrom(WorldsmithMod.Settings);
		}

		public void CopyFrom(WorldsmithSettings defaults)
		{
			if (defaults == null)
			{
				enableSeaLevelControl = false;
				targetLandFraction = 0.4f;
				axialTilt = 23.4f;
				return;
			}
			enableSeaLevelControl = defaults.enableSeaLevelControl;
			targetLandFraction = defaults.targetLandFraction;
			axialTilt = defaults.axialTilt;
		}

		public WorldsmithWorldSettings Clone()
		{
			return new WorldsmithWorldSettings
			{
				enableSeaLevelControl = enableSeaLevelControl,
				targetLandFraction = targetLandFraction,
				axialTilt = axialTilt,
			};
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref enableSeaLevelControl, "enableSeaLevelControl", defaultValue: false);
			Scribe_Values.Look(ref targetLandFraction, "targetLandFraction", 0.4f);
			Scribe_Values.Look(ref axialTilt, "axialTilt", 23.4f);
		}
	}

	/// <summary>
	/// Holds the parameters the player is currently editing on the world creation page.
	/// Generation reads these, and the world component copies them so they persist with
	/// the finished planet.
	/// </summary>
	public static class WorldsmithWorldParams
	{
		private static WorldsmithWorldSettings pending;

		/// <summary>Parameters for the world about to be created; seeded from the mod defaults.</summary>
		public static WorldsmithWorldSettings Pending
		{
			get
			{
				if (pending == null)
				{
					pending = new WorldsmithWorldSettings();
				}
				return pending;
			}
			set => pending = value;
		}

		/// <summary>Re-seed the pending parameters from the mod-wide defaults.</summary>
		public static void ResetToDefaults()
		{
			pending = new WorldsmithWorldSettings();
		}

		/// <summary>
		/// The parameters generation should obey: the current world's own settings when
		/// it has them, otherwise whatever is pending for the world being made.
		/// </summary>
		public static WorldsmithWorldSettings Active
		{
			get
			{
				WorldsmithWorldSettings stored = Find.World?.GetComponent<WorldsmithWorldComponent>()?.Settings;
				return stored ?? Pending;
			}
		}
	}
}
