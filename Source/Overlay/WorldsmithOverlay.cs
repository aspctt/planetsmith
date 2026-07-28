// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Worldsmith.Gen;

namespace Worldsmith.Overlay
{
	public enum OverlayMode
	{
		None,
		Temperature,
		Rainfall,
		Swampiness,
		Continentality,
		WinterTemperature,
		OceanCurrents,
		Aridity,
		Monsoon,
		Drainage,
	}

	/// <summary>
	/// Debug map-mode state and colour mapping for visualising Worldsmith's
	/// generated climate. Switching mode marks the climate draw layer dirty so it
	/// regenerates on the next frame.
	/// </summary>
	public static class WorldsmithOverlay
	{
		public static OverlayMode Mode { get; private set; } = OverlayMode.None;

		private readonly struct Stop
		{
			public readonly float Value;
			public readonly Color Color;

			public Stop(float value, Color color)
			{
				Value = value;
				Color = color;
			}
		}

		// Cold -> hot, in degrees Celsius.
		private static readonly Stop[] TemperatureStops =
		{
			new Stop(-50f, new Color(0.10f, 0.10f, 0.55f)),
			new Stop(-20f, new Color(0.00f, 0.55f, 0.90f)),
			new Stop(0f, new Color(0.20f, 0.80f, 0.65f)),
			new Stop(15f, new Color(0.45f, 0.85f, 0.25f)),
			new Stop(30f, new Color(0.95f, 0.85f, 0.10f)),
			new Stop(45f, new Color(0.90f, 0.20f, 0.10f)),
		};

		// Arid -> wet, in mm/year.
		private static readonly Stop[] RainfallStops =
		{
			new Stop(0f, new Color(0.85f, 0.75f, 0.45f)),
			new Stop(400f, new Color(0.90f, 0.80f, 0.30f)),
			new Stop(1000f, new Color(0.55f, 0.80f, 0.30f)),
			new Stop(2000f, new Color(0.10f, 0.60f, 0.30f)),
			new Stop(3500f, new Color(0.10f, 0.50f, 0.70f)),
			new Stop(5000f, new Color(0.10f, 0.20f, 0.80f)),
		};

		// Dry -> waterlogged, swampiness 0..1.
		private static readonly Stop[] SwampinessStops =
		{
			new Stop(0f, new Color(0.80f, 0.72f, 0.50f)),
			new Stop(0.5f, new Color(0.55f, 0.65f, 0.35f)),
			new Stop(0.75f, new Color(0.25f, 0.50f, 0.35f)),
			new Stop(1f, new Color(0.10f, 0.35f, 0.40f)),
		};

		// Cold upwelling (-1) -> none (0) -> mild maritime (+1). Grey marks tiles the
		// ocean-current pass left alone, so the affected coasts stand out on their own.
		private static readonly Stop[] CoastalAnomalyStops =
		{
			new Stop(-1f, new Color(0.75f, 0.25f, 0.15f)),
			new Stop(-0.05f, new Color(0.90f, 0.70f, 0.55f)),
			new Stop(0f, new Color(0.28f, 0.28f, 0.30f)),
			new Stop(0.05f, new Color(0.55f, 0.85f, 0.75f)),
			new Stop(1f, new Color(0.10f, 0.45f, 0.85f)),
		};

		// Effective moisture, on the standard aridity bands: hyper-arid, arid, semi-arid,
		// dry sub-humid, then humid.
		private static readonly Stop[] AridityStops =
		{
			new Stop(0f, new Color(0.75f, 0.35f, 0.20f)),
			new Stop(0.2f, new Color(0.90f, 0.75f, 0.35f)),
			new Stop(0.5f, new Color(0.80f, 0.85f, 0.35f)),
			new Stop(0.65f, new Color(0.45f, 0.80f, 0.35f)),
			new Stop(1.5f, new Color(0.15f, 0.55f, 0.35f)),
			new Stop(3f, new Color(0.10f, 0.35f, 0.65f)),
		};

		// No monsoon (grey) through to a drenching one. Grey marks tiles the seasonal
		// rains never reach, so the monsoon belt stands out on its own.
		private static readonly Stop[] MonsoonStops =
		{
			new Stop(0f, new Color(0.28f, 0.28f, 0.30f)),
			new Stop(0.05f, new Color(0.70f, 0.75f, 0.45f)),
			new Stop(0.4f, new Color(0.35f, 0.70f, 0.45f)),
			new Stop(0.7f, new Color(0.15f, 0.55f, 0.70f)),
			new Stop(1f, new Color(0.35f, 0.20f, 0.75f)),
		};

		// Ridges (dry, pale) through to the valleys whole watersheds drain along.
		private static readonly Stop[] DrainageStops =
		{
			new Stop(0f, new Color(0.82f, 0.78f, 0.62f)),
			new Stop(0.35f, new Color(0.55f, 0.72f, 0.45f)),
			new Stop(0.6f, new Color(0.25f, 0.60f, 0.60f)),
			new Stop(0.8f, new Color(0.12f, 0.40f, 0.80f)),
			new Stop(1f, new Color(0.05f, 0.15f, 0.55f)),
		};

		// Maritime -> continental, continentality 0..1.
		private static readonly Stop[] ContinentalityStops =
		{
			new Stop(0f, new Color(0.20f, 0.60f, 0.75f)),
			new Stop(0.5f, new Color(0.85f, 0.80f, 0.50f)),
			new Stop(1f, new Color(0.70f, 0.35f, 0.20f)),
		};

		public static void SetMode(OverlayMode mode)
		{
			Mode = mode;
			World world = Find.World;
			PlanetLayer surface = Find.WorldGrid?.Surface;
			if (world?.renderer != null && surface != null)
			{
				world.renderer.SetDirty<WorldDrawLayer_WorldsmithClimate>(surface);
			}
		}

		/// <summary>Player-facing name for a map mode.</summary>
		public static string Label(OverlayMode mode)
		{
			switch (mode)
			{
				case OverlayMode.Temperature: return "Temperature";
				case OverlayMode.Rainfall: return "Rainfall";
				case OverlayMode.Swampiness: return "Wetlands";
				case OverlayMode.Continentality: return "Distance from sea";
				case OverlayMode.WinterTemperature: return "Winter temperature";
				case OverlayMode.OceanCurrents: return "Coastal currents";
				case OverlayMode.Aridity: return "Effective moisture";
				case OverlayMode.Monsoon: return "Monsoon";
				case OverlayMode.Drainage: return "Water flow";
				default: return "Off";
			}
		}

		/// <summary>Modes that need generated data Worldsmith only has for the current world.</summary>
		public static bool RequiresGeneratedData(OverlayMode mode)
		{
			switch (mode)
			{
				case OverlayMode.Continentality:
				case OverlayMode.WinterTemperature:
				case OverlayMode.OceanCurrents:
				case OverlayMode.Aridity:
				case OverlayMode.Monsoon:
				case OverlayMode.Drainage:
					return true;
				default:
					return false;
			}
		}

		public static void Cycle()
		{
			OverlayMode next = Mode switch
			{
				OverlayMode.None => OverlayMode.Temperature,
				OverlayMode.Temperature => OverlayMode.Rainfall,
				OverlayMode.Rainfall => OverlayMode.Swampiness,
				OverlayMode.Swampiness => OverlayMode.Continentality,
				OverlayMode.Continentality => OverlayMode.WinterTemperature,
				OverlayMode.WinterTemperature => OverlayMode.OceanCurrents,
				OverlayMode.OceanCurrents => OverlayMode.Aridity,
				OverlayMode.Aridity => OverlayMode.Monsoon,
				OverlayMode.Monsoon => OverlayMode.Drainage,
				_ => OverlayMode.None,
			};
			SetMode(next);
		}

		public static Color32 ColorFor(int tileIndex, Tile tile)
		{
			switch (Mode)
			{
				case OverlayMode.Temperature:
					return Evaluate(TemperatureStops, tile.temperature);
				case OverlayMode.Rainfall:
					return Evaluate(RainfallStops, tile.rainfall);
				case OverlayMode.Swampiness:
					return Evaluate(SwampinessStops, tile.swampiness);
				case OverlayMode.Continentality:
					return CachedColor(ContinentalityStops, WorldsmithClimateCache.Continentality, tileIndex);
				case OverlayMode.WinterTemperature:
					return CachedColor(TemperatureStops, WorldsmithClimateCache.WinterMinTemp, tileIndex);
				case OverlayMode.OceanCurrents:
					return CachedColor(CoastalAnomalyStops, WorldsmithClimateCache.CoastalAnomaly, tileIndex);
				case OverlayMode.Aridity:
					return CachedColor(AridityStops, WorldsmithClimateCache.AridityIndex, tileIndex);
				case OverlayMode.Monsoon:
					return CachedColor(MonsoonStops, WorldsmithClimateCache.MonsoonStrength, tileIndex);
				case OverlayMode.Drainage:
					// Water carries no runoff of its own, and shading it would make the
					// sea look like the driest ground there is. Leave it showing through
					// so the drainage networks read against a real coastline.
					return tile.WaterCovered
						? new Color32(0, 0, 0, 0)
						: CachedColor(DrainageStops, WorldsmithClimateCache.FlowAccumulation, tileIndex);
				default:
					return new Color32(0, 0, 0, 0);
			}
		}

		private static Color32 CachedColor(Stop[] stops, float[] field, int tileIndex)
		{
			if (!WorldsmithClimateCache.Valid || field == null || tileIndex < 0 || tileIndex >= field.Length)
			{
				return new Color32(0, 0, 0, 0);
			}
			return Evaluate(stops, field[tileIndex]);
		}

		private static Color Evaluate(Stop[] stops, float value)
		{
			if (value <= stops[0].Value)
			{
				return stops[0].Color;
			}
			int last = stops.Length - 1;
			if (value >= stops[last].Value)
			{
				return stops[last].Color;
			}
			for (int i = 1; i < stops.Length; i++)
			{
				if (value <= stops[i].Value)
				{
					float t = Mathf.InverseLerp(stops[i - 1].Value, stops[i].Value, value);
					return Color.Lerp(stops[i - 1].Color, stops[i].Color, t);
				}
			}
			return stops[last].Color;
		}
	}
}
