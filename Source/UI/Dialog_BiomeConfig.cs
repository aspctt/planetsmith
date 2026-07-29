// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Planetsmith.Gen;

namespace Planetsmith.UI
{
	/// <summary>
	/// Per-biome controls for the planet being generated: whether a biome may appear at
	/// all, how much ground it takes where it already suits the climate, and a flat nudge
	/// to its score. Lists every biome that generates naturally, including modded ones,
	/// since they are read straight from the def database.
	/// </summary>
	public class Dialog_BiomeConfig : Window
	{
		private const float RowHeight = 34f;
		private const float NameWidth = 240f;
		private const float ValueWidth = 60f;
		private const float SliderWidth = 190f;
		private const float ColumnGap = 12f;

		private Vector2 scrollPosition;
		private List<BiomeDef> biomes;

		public Dialog_BiomeConfig()
		{
			forcePause = true;
			absorbInputAroundWindow = true;
			doCloseX = true;
			closeOnClickedOutside = true;
		}

		public override Vector2 InitialSize => new Vector2(900f, 760f);

		public override void PreOpen()
		{
			base.PreOpen();
			// Sea, lake and ice-sheet water are left out: those tiles have no dry-land
			// alternative to fall back on, so switching one off could only produce a
			// planet that fails to draw. Ice sheet itself stays, being dry land, and a
			// world without polar caps is a perfectly sensible thing to ask for.
			biomes = DefDatabase<BiomeDef>.AllDefsListForReading
				.Where(b => b.implemented && b.generatesNaturally && !BiomeProfiler.IsWaterBiome(b))
				.OrderBy(b => b.label ?? b.defName)
				.ToList();
		}

		public override void DoWindowContents(Rect inRect)
		{
			PlanetsmithWorldSettings world = PlanetsmithWorldParams.Pending;

			float y = inRect.y;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 36f), "Planetsmith: biomes");
			Text.Font = GameFont.Small;
			y += 40f;

			// Two lines tall: this sentence wraps at the dialog's width.
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 52f),
				"Turn a biome off to keep it off this planet. Frequency scales how much ground it takes where the climate already suits it; offset nudges it into or out of places it narrowly loses or wins.");
			y += 62f;

			DrawHeader(new Rect(inRect.x, y, inRect.width, 24f));
			y += 26f;

			float bottomBar = 40f;
			var outRect = new Rect(inRect.x, y, inRect.width, inRect.height - y - bottomBar);
			var viewRect = new Rect(0f, 0f, outRect.width - 20f, biomes.Count * RowHeight);

			Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
			float rowY = 0f;
			for (int i = 0; i < biomes.Count; i++)
			{
				DrawBiomeRow(new Rect(0f, rowY, viewRect.width, RowHeight), biomes[i], world);
				rowY += RowHeight;
			}
			Widgets.EndScrollView();

			var resetRect = new Rect(inRect.x, inRect.yMax - 32f, 200f, 30f);
			if (Widgets.ButtonText(resetRect, "Reset all biomes"))
			{
				world.ResetAllBiomes();
			}
		}

		private static void DrawHeader(Rect rect)
		{
			GUI.color = new Color(1f, 1f, 1f, 0.55f);
			float x = rect.x + NameWidth + ColumnGap;
			Widgets.Label(new Rect(x, rect.y, ValueWidth + SliderWidth, rect.height), "Frequency");
			x += ValueWidth + SliderWidth + ColumnGap;
			Widgets.Label(new Rect(x, rect.y, ValueWidth + SliderWidth, rect.height), "Score offset");
			GUI.color = Color.white;
		}

		private static void DrawBiomeRow(Rect rect, BiomeDef biome, PlanetsmithWorldSettings world)
		{
			if (Mouse.IsOver(rect))
			{
				Widgets.DrawHighlight(rect);
			}

			BiomeSettings existing = world.ForBiomeOrNull(biome.defName);
			bool enabled = existing?.enabled ?? true;
			float commonality = existing?.commonality ?? 1f;
			float offset = existing?.scoreOffset ?? 0f;

			var checkboxRect = new Rect(rect.x, rect.y + 4f, 24f, 24f);
			bool wasEnabled = enabled;
			Widgets.Checkbox(checkboxRect.position, ref enabled, 24f);

			var labelRect = new Rect(rect.x + 30f, rect.y + 4f, NameWidth - 30f, 26f);
			Widgets.Label(labelRect, biome.LabelCap);
			if (!biome.description.NullOrEmpty())
			{
				TooltipHandler.TipRegion(labelRect, biome.description);
			}

			float x = rect.x + NameWidth + ColumnGap;
			Widgets.Label(new Rect(x, rect.y + 4f, ValueWidth, 26f), commonality.ToString("0.0") + "x");
			float newCommonality = Widgets.HorizontalSlider(
				new Rect(x + ValueWidth, rect.y + 8f, SliderWidth, 20f),
				commonality, 0f, BiomeSettings.MaxCommonality, roundTo: 0.1f);

			x += ValueWidth + SliderWidth + ColumnGap;
			Widgets.Label(new Rect(x, rect.y + 4f, ValueWidth, 26f), offset.ToString("+0;-0;0"));
			float newOffset = Widgets.HorizontalSlider(
				new Rect(x + ValueWidth, rect.y + 8f, SliderWidth, 20f),
				offset, -BiomeSettings.MaxScoreOffset, BiomeSettings.MaxScoreOffset, roundTo: 1f);

			// Only materialise an entry once the player actually changes something, so a
			// planet's saved settings stay limited to what was deliberately adjusted.
			if (enabled != wasEnabled || newCommonality != commonality || newOffset != offset)
			{
				BiomeSettings settings = world.ForBiome(biome.defName);
				settings.enabled = enabled;
				settings.commonality = newCommonality;
				settings.scoreOffset = newOffset;
			}
		}
	}
}
