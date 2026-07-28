// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Planetsmith.UI;

namespace Planetsmith.Patches
{
	/// <summary>
	/// Adds a Planetsmith button to the world creation page, opening the per-world
	/// parameters dialog.
	///
	/// It goes on the bottom button row rather than into the page's two-column body,
	/// which is laid out at fixed offsets and which other mods rewrite wholesale. The
	/// row is already crowded: vanilla puts Back hard left, Generate hard right, and
	/// centres Reset all and Reset factions around the middle. That leaves one gap,
	/// between Back and Reset all, so the button is sized like a standard one and
	/// tucked into it.
	/// </summary>
	[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.DoWindowContents))]
	public static class Patch_Page_CreateWorldParams
	{
		// Matches Page.BottomButSize and the page's own spacing, which are protected.
		private const float ButtonWidth = 150f;
		private const float ButtonHeight = 38f;
		private const float ButtonGap = 8.5f;

		public static void Postfix(Rect rect)
		{
			const float width = ButtonWidth;
			var buttonRect = new Rect(
				rect.x + width + ButtonGap,
				rect.y + rect.height - ButtonHeight,
				width,
				ButtonHeight);

			if (Widgets.ButtonText(buttonRect, "Planetsmith..."))
			{
				Find.WindowStack.Add(new Dialog_PlanetsmithWorldParams());
			}
		}
	}

	/// <summary>
	/// Each time the creation page opens, start from the mod-wide defaults so a fresh
	/// planet is not silently shaped by whatever the previous one used.
	/// </summary>
	[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.PreOpen))]
	public static class Patch_Page_CreateWorldParams_PreOpen
	{
		public static void Postfix()
		{
			PlanetsmithWorldParams.ResetToDefaults();
		}
	}
}
