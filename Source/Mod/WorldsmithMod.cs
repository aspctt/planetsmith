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
			listing.Gap(6f);
			listing.Label("More world-generation options will appear here as features are added.");
			listing.End();
		}
	}
}
