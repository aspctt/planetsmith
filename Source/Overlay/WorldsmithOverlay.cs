// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Worldsmith.Overlay
{
	public enum OverlayMode
	{
		None,
		Temperature,
		Rainfall,
		Swampiness,
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

		public static void Cycle()
		{
			OverlayMode next = Mode switch
			{
				OverlayMode.None => OverlayMode.Temperature,
				OverlayMode.Temperature => OverlayMode.Rainfall,
				OverlayMode.Rainfall => OverlayMode.Swampiness,
				_ => OverlayMode.None,
			};
			SetMode(next);
		}

		public static Color32 ColorFor(Tile tile)
		{
			switch (Mode)
			{
				case OverlayMode.Temperature:
					return Evaluate(TemperatureStops, tile.temperature);
				case OverlayMode.Rainfall:
					return Evaluate(RainfallStops, tile.rainfall);
				case OverlayMode.Swampiness:
					return Evaluate(SwampinessStops, tile.swampiness);
				default:
					return new Color32(0, 0, 0, 0);
			}
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
