// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using UnityEngine;
using Verse;

namespace Worldsmith
{
	/// <summary>
	/// Mod entry point. RimWorld instantiates this once at startup and uses it to
	/// surface the settings window. The heavy world-generation machinery lives
	/// elsewhere; this class only owns configuration and the settings UI.
	/// </summary>
	public class WorldsmithMod : Verse.Mod
	{
		public const string PackageId = "aspctt.worldsmith";

		public static WorldsmithMod Instance { get; private set; }
		public static WorldsmithSettings Settings { get; private set; }

		public WorldsmithMod(ModContentPack content) : base(content)
		{
			Instance = this;
			Settings = GetSettings<WorldsmithSettings>();
		}

		public override string SettingsCategory() => "Worldsmith";

		public override void DoSettingsWindowContents(Rect inRect)
		{
			var listing = new Listing_Standard();
			listing.Begin(inRect);
			listing.Label("Worldsmith");
			listing.Gap(6f);
			listing.CheckboxLabeled(
				"Enable climate & biome overhaul",
				ref Settings.enableClimateOverhaul,
				"When on, Worldsmith recomputes each tile's temperature, rainfall, and biome from its own climate model during world generation. When off, worlds generate as vanilla.");
			listing.Gap(12f);
			listing.CheckboxLabeled(
				"Control sea level",
				ref Settings.enableSeaLevelControl,
				"When on, Worldsmith raises or lowers sea level during world generation until the planet has the land fraction set below. Leave off to let vanilla, or another mod, decide the coastline.");
			if (Settings.enableSeaLevelControl)
			{
				listing.Label($"Land: {Settings.targetLandFraction:P0} of the planet's surface");
				Settings.targetLandFraction = listing.Slider(Settings.targetLandFraction, 0.05f, 0.95f);
			}
			listing.Gap(12f);
			listing.CheckboxLabeled(
				"Show map-mode button on the planet view",
				ref Settings.showMapModeButton,
				"Adds a button to the planet view for shading the world by temperature, rainfall and Worldsmith's other climate layers.");

			listing.Gap(12f);
			listing.Label($"Axial tilt: {Settings.axialTilt:F1}°  ({TiltDescription(Settings.axialTilt)})");
			Settings.axialTilt = listing.Slider(Settings.axialTilt, 0f, 90f);
			listing.Gap(6f);
			listing.Label("These are the defaults new planets start from. To shape one planet, use the Worldsmith button on the world creation page.");
			listing.End();
		}

		public static string TiltDescription(float tilt)
		{
			if (tilt < 5f)
			{
				return "almost no seasons";
			}
			if (tilt < 18f)
			{
				return "mild seasons";
			}
			if (tilt < 30f)
			{
				return "Earth-like";
			}
			if (tilt < 50f)
			{
				return "harsh seasons";
			}
			return "extreme seasons";
		}
	}
}
