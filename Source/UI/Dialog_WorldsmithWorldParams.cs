// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using UnityEngine;
using Verse;

namespace Worldsmith.UI
{
	/// <summary>
	/// Per-world generation parameters, opened from the world creation page so a planet
	/// can be shaped without leaving for the mod options. Edits apply to the world about
	/// to be generated; the mod settings remain the defaults new worlds start from.
	/// </summary>
	public class Dialog_WorldsmithWorldParams : Window
	{
		public Dialog_WorldsmithWorldParams()
		{
			forcePause = true;
			absorbInputAroundWindow = true;
			doCloseX = true;
			closeOnClickedOutside = true;
		}

		public override Vector2 InitialSize => new Vector2(620f, 420f);

		public override void DoWindowContents(Rect inRect)
		{
			WorldsmithWorldSettings world = WorldsmithWorldParams.Pending;

			var listing = new Listing_Standard();
			listing.Begin(inRect);

			Text.Font = GameFont.Medium;
			listing.Label("Worldsmith: planet parameters");
			Text.Font = GameFont.Small;
			listing.Gap(4f);
			listing.Label("These apply to the world you are about to generate.");
			listing.GapLine(12f);

			listing.CheckboxLabeled(
				"Control sea level",
				ref world.enableSeaLevelControl,
				"When on, Worldsmith raises or lowers sea level until the planet has the land fraction set below. Leave off to let vanilla, or another mod, decide the coastline.");
			if (world.enableSeaLevelControl)
			{
				listing.Label($"Land: {world.targetLandFraction:P0} of the planet's surface");
				world.targetLandFraction = listing.Slider(world.targetLandFraction, 0.05f, 0.95f);
			}

			listing.Gap(12f);
			listing.Label($"Axial tilt: {world.axialTilt:F1}°  ({WorldsmithMod.TiltDescription(world.axialTilt)})");
			world.axialTilt = listing.Slider(world.axialTilt, 0f, 90f);
			listing.Label("Tilt is why a planet has seasons. Upright worlds barely change through the year; steeply tilted ones swing between harsh summers and winters.");

			listing.Gap(12f);
			if (listing.ButtonText("Reset to mod defaults"))
			{
				WorldsmithWorldParams.ResetToDefaults();
			}

			listing.End();
		}
	}
}
