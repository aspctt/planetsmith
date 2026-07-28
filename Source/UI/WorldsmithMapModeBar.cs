// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Worldsmith.Gen;
using Worldsmith.Overlay;

namespace Worldsmith.UI
{
	/// <summary>
	/// A button on the planet view for switching Worldsmith's map modes, so the climate
	/// behind a world can be read without turning on development mode. Sits against the
	/// empty left edge of the screen, clear of the tile inspector, the page buttons and
	/// the compass.
	/// </summary>
	public static class WorldsmithMapModeBar
	{
		// Two lines tall, so the mode name sits under its heading rather than being
		// squeezed onto one long line beside it.
		private const float ButtonWidth = 172f;
		private const float ButtonHeight = 52f;
		private const float EdgeMargin = 10f;
		private const float TopOffset = 200f;

		/// <summary>
		/// Every mode, listed alphabetically by the name the player sees. Sorted once at
		/// startup rather than written out in order, so a mode added later cannot end up
		/// out of place. Off stays pinned at the top: it turns the shading off rather than
		/// being a view of its own, and sorting it into the middle of the list would hide
		/// it among them.
		/// </summary>
		private static readonly OverlayMode[] Modes = BuildModeOrder();

		private static OverlayMode[] BuildModeOrder()
		{
			var modes = new List<OverlayMode>((OverlayMode[])Enum.GetValues(typeof(OverlayMode)));
			modes.Remove(OverlayMode.None);
			modes.Sort((a, b) => string.Compare(
				WorldsmithOverlay.Label(a),
				WorldsmithOverlay.Label(b),
				StringComparison.CurrentCultureIgnoreCase));
			modes.Insert(0, OverlayMode.None);
			return modes.ToArray();
		}

		public static void DoGUI()
		{
			WorldsmithSettings settings = WorldsmithMod.Settings;
			if (settings == null || !settings.showMapModeButton)
			{
				return;
			}
			if (!WorldRendererUtility.WorldRendered)
			{
				return;
			}

			OverlayMode current = WorldsmithOverlay.Mode;
			if (WorldsmithOverlay.RequiresGeneratedData(current) && !WorldsmithClimateCache.Valid)
			{
				// Loading a save leaves a generated layer selected but with nothing behind
				// it. Fall back rather than naming a view that cannot be drawn.
				WorldsmithOverlay.SetMode(OverlayMode.None);
				current = OverlayMode.None;
			}
			string label = current == OverlayMode.None
				? "Worldsmith\nMap modes"
				: "Map mode:\n" + WorldsmithOverlay.Label(current);

			var rect = new Rect(EdgeMargin, TopOffset, ButtonWidth, ButtonHeight);
			if (Widgets.ButtonText(rect, label))
			{
				Find.WindowStack.Add(new FloatMenu(BuildOptions()));
			}
			TooltipHandler.TipRegion(rect, "Shade the planet by the climate Worldsmith generated for it.");
		}

		private static List<FloatMenuOption> BuildOptions()
		{
			var options = new List<FloatMenuOption>();
			bool haveData = WorldsmithClimateCache.Valid;
			for (int i = 0; i < Modes.Length; i++)
			{
				OverlayMode mode = Modes[i];
				string label = WorldsmithOverlay.Label(mode);
				if (WorldsmithOverlay.RequiresGeneratedData(mode) && !haveData)
				{
					// These layers are worked out while a world is generated and are not
					// kept in the save, so after a reload there is nothing to show.
					options.Add(new FloatMenuOption(label + " (regenerate to view)", null));
					continue;
				}
				OverlayMode captured = mode;
				options.Add(new FloatMenuOption(label, (Action)(() => WorldsmithOverlay.SetMode(captured))));
			}
			return options;
		}
	}
}
