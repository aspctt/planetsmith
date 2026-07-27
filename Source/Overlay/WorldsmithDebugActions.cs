// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using LudeonTK;

namespace Worldsmith.Overlay
{
	/// <summary>
	/// Dev-mode actions (Debug actions menu, category "Worldsmith") for toggling the
	/// climate map-mode overlay while a world is on screen, during both world
	/// creation and play.
	/// </summary>
	public static class WorldsmithDebugActions
	{
		[DebugAction("Worldsmith", "Climate overlay: off", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayOff()
		{
			WorldsmithOverlay.SetMode(OverlayMode.None);
		}

		[DebugAction("Worldsmith", "Climate overlay: temperature", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayTemperature()
		{
			WorldsmithOverlay.SetMode(OverlayMode.Temperature);
		}

		[DebugAction("Worldsmith", "Climate overlay: rainfall", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayRainfall()
		{
			WorldsmithOverlay.SetMode(OverlayMode.Rainfall);
		}

		[DebugAction("Worldsmith", "Climate overlay: swampiness", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlaySwampiness()
		{
			WorldsmithOverlay.SetMode(OverlayMode.Swampiness);
		}
	}
}
