// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using Verse;
using Worldsmith.UI;

namespace Worldsmith
{
	/// <summary>
	/// Carries a planet's Worldsmith parameters inside the save. RimWorld builds its
	/// world components before the generation steps run, so this captures whatever the
	/// player chose on the creation page and hands the same values to the passes; on a
	/// later load it restores them from the save instead.
	/// </summary>
	public class WorldsmithWorldComponent : WorldComponent
	{
		private WorldsmithWorldSettings settings;

		public WorldsmithWorldSettings Settings => settings;

		public WorldsmithWorldComponent(World world) : base(world)
		{
			settings = WorldsmithWorldParams.Pending.Clone();
		}

		public override void WorldComponentOnGUI()
		{
			base.WorldComponentOnGUI();
			WorldsmithMapModeBar.DoGUI();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look(ref settings, "settings");
			if (Scribe.mode == LoadSaveMode.PostLoadInit && settings == null)
			{
				// Saved before Worldsmith was added, or from an older version.
				settings = new WorldsmithWorldSettings();
			}
		}
	}
}
