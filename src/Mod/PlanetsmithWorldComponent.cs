// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using RimWorld.Planet;
using Verse;
using Planetsmith.Generation;
using Planetsmith.UI;

namespace Planetsmith;

/// <summary>
/// Carries a planet's Planetsmith parameters inside the save. RimWorld builds its
/// world components before the generation steps run, so this captures whatever the
/// player chose on the creation page and hands the same values to the passes; on a
/// later load it restores them from the save instead.
/// </summary>
public class PlanetsmithWorldComponent : WorldComponent
{
	//// References and State
	
	private PlanetsmithWorldSettings settings;
	
	public PlanetsmithWorldSettings Settings => settings;
	
	//// Public API
	
	public PlanetsmithWorldComponent(World world) : base(world)
	{
		// Take the parameters chosen on the creation page
		settings = PlanetsmithWorldParameters.Pending.Clone();
		
		// Forget the previous world's climate
		// A new world is being built or loaded, so anything the overlays were holding
		// belongs to the previous one. RimWorld builds components before the generation
		// steps run, so a world we are about to generate refills this immediately, while
		// one loaded from a save refills it from the save once loading finishes, or leaves
		// it empty when the save is older than the stored fields.
		PlanetsmithClimateCache.Invalidate();
	}
	
	//// Event Wiring
	
	public override void WorldComponentOnGUI()
	{
		base.WorldComponentOnGUI();
		PlanetsmithMapModeBar.OnGUI();
	}
	
	public override void ExposeData()
	{
		// Save or load the parameters
		base.ExposeData();
		Scribe_Deep.Look(ref settings, "settings");
		
		// Fill in parameters for a save that has none
		// Saved before Planetsmith was added, or from an older version.
		if (Scribe.mode == LoadSaveMode.PostLoadInit && settings == null)
		{
			settings = new PlanetsmithWorldSettings();
		}

		// Save or load the climate fields the map modes show
		PlanetsmithClimateCache.ExposeData();
	}
}
