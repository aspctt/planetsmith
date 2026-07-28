// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using Verse;
using Planetsmith.UI;

namespace Planetsmith
{
	/// <summary>
	/// Carries a planet's Planetsmith parameters inside the save. RimWorld builds its
	/// world components before the generation steps run, so this captures whatever the
	/// player chose on the creation page and hands the same values to the passes; on a
	/// later load it restores them from the save instead.
	/// </summary>
	public class PlanetsmithWorldComponent : WorldComponent
	{
		private PlanetsmithWorldSettings settings;

		public PlanetsmithWorldSettings Settings => settings;

		public PlanetsmithWorldComponent(World world) : base(world)
		{
			settings = PlanetsmithWorldParams.Pending.Clone();

			// A new world is being built or loaded, so anything the overlays were holding
			// belongs to the previous one. RimWorld builds components before the
			// generation steps run, so a world we are about to generate refills this
			// immediately, while one loaded from a save correctly leaves it empty.
			Gen.PlanetsmithClimateCache.Invalidate();
		}

		public override void WorldComponentOnGUI()
		{
			base.WorldComponentOnGUI();
			PlanetsmithMapModeBar.DoGUI();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look(ref settings, "settings");
			if (Scribe.mode == LoadSaveMode.PostLoadInit && settings == null)
			{
				// Saved before Planetsmith was added, or from an older version.
				settings = new PlanetsmithWorldSettings();
			}
		}
	}
}
