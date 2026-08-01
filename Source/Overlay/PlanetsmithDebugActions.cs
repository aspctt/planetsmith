// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using LudeonTK;

namespace Planetsmith.Overlay
{
	/// <summary>
	/// Dev-mode actions (Debug actions menu, category "Planetsmith") for toggling the
	/// climate map-mode overlay while a world is on screen, during both world
	/// creation and play.
	/// </summary>
	public static class PlanetsmithDebugActions
	{
		[DebugAction("Planetsmith", "Climate overlay: off", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayOff()
		{
			PlanetsmithOverlay.SetMode(OverlayMode.None);
		}

		[DebugAction("Planetsmith", "Climate overlay: temperature", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayTemperature()
		{
			PlanetsmithOverlay.SetMode(OverlayMode.Temperature);
		}

		[DebugAction("Planetsmith", "Climate overlay: rainfall", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayRainfall()
		{
			PlanetsmithOverlay.SetMode(OverlayMode.Rainfall);
		}

		[DebugAction("Planetsmith", "Climate overlay: swampiness", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlaySwampiness()
		{
			PlanetsmithOverlay.SetMode(OverlayMode.Swampiness);
		}

		[DebugAction("Planetsmith", "Climate overlay: continentality", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayContinentality()
		{
			PlanetsmithOverlay.SetMode(OverlayMode.Continentality);
		}

		[DebugAction("Planetsmith", "Climate overlay: winter temperature", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayWinterTemperature()
		{
			PlanetsmithOverlay.SetMode(OverlayMode.WinterTemperature);
		}

		[DebugAction("Planetsmith", "Climate overlay: ocean currents", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayOceanCurrents()
		{
			PlanetsmithOverlay.SetMode(OverlayMode.OceanCurrents);
		}

		[DebugAction("Planetsmith", "Climate overlay: aridity", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayAridity()
		{
			PlanetsmithOverlay.SetMode(OverlayMode.Aridity);
		}

		[DebugAction("Planetsmith", "Climate overlay: monsoon", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayMonsoon()
		{
			PlanetsmithOverlay.SetMode(OverlayMode.Monsoon);
		}

		[DebugAction("Planetsmith", "Climate overlay: rain shadow", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayRainShadow()
		{
			PlanetsmithOverlay.SetMode(OverlayMode.RainShadow);
		}

		[DebugAction("Planetsmith", "Climate overlay: water flow", allowedGameStates = AllowedGameStates.WorldRenderedNow)]
		private static void OverlayDrainage()
		{
			PlanetsmithOverlay.SetMode(OverlayMode.Drainage);
		}
	}
}
