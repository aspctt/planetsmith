// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using UnityEngine;
using Verse;

namespace Worldsmith.UI
{
	/// <summary>
	/// Dials on the climate model for the planet being generated. Every one of these
	/// nudges a part of the simulation rather than setting a value outright, so leaving
	/// them alone produces the world Worldsmith would have built anyway.
	/// </summary>
	public class Dialog_ClimateTuning : Window
	{
		public Dialog_ClimateTuning()
		{
			forcePause = true;
			absorbInputAroundWindow = true;
			doCloseX = true;
			closeOnClickedOutside = true;
		}

		public override Vector2 InitialSize => new Vector2(640f, 660f);

		public override void DoWindowContents(Rect inRect)
		{
			ClimateTuning tuning = WorldsmithWorldParams.Pending.tuning;

			var listing = new Listing_Standard();
			listing.Begin(inRect);

			Text.Font = GameFont.Medium;
			listing.Label("Worldsmith: climate tuning");
			Text.Font = GameFont.Small;
			listing.Gap(4f);
			listing.Label("These adjust the simulation for the world you are about to generate. Left at their defaults they change nothing.");
			listing.GapLine(10f);

			Offset(listing, "Global temperature", ref tuning.temperatureOffset, -20f, 20f, "C",
				"Warms or cools the whole planet before anything else is worked out.");

			Factor(listing, "Cooling with altitude", ref tuning.elevationCooling,
				"How much colder high ground becomes. Higher values put snow on smaller mountains.");

			Factor(listing, "Overall rainfall", ref tuning.rainfallScale,
				"Scales the rain falling everywhere, before mountains, coasts and monsoons have their say.");

			Factor(listing, "Rain shadows", ref tuning.rainShadow,
				"How thoroughly mountains wring out the air crossing them, leaving the far side dry.");

			Factor(listing, "Moisture reach inland", ref tuning.moistureReach,
				"How far sea air carries its water before running dry. Higher values green the interiors.");

			Factor(listing, "Monsoon strength", ref tuning.monsoonStrength,
				"How heavy the seasonal rains are on tropical coasts.");

			Factor(listing, "Season intensity", ref tuning.seasonIntensity,
				"How far temperatures swing between summer and winter, which decides where frost-shy biomes can live.");

			Factor(listing, "Coastal influence", ref tuning.coastalInfluence,
				"How strongly the sea tempers the coasts beside it, mild where the wind comes off the water and cold and dry where it does not.");

			listing.Gap(10f);
			if (listing.ButtonText(tuning.IsDefault ? "Defaults" : "Reset to defaults"))
			{
				tuning.Reset();
			}

			listing.End();
		}

		private static void Factor(Listing_Standard listing, string label, ref float value, string tooltip)
		{
			Rect row = listing.GetRect(28f);
			TooltipHandler.TipRegion(row, tooltip);
			Widgets.Label(row.LeftPart(0.52f), $"{label}: {value:0.00}x");
			value = Widgets.HorizontalSlider(row.RightPart(0.46f).ContractedBy(0f, 4f), value, 0f, 2f, roundTo: 0.05f);
		}

		private static void Offset(Listing_Standard listing, string label, ref float value, float min, float max, string unit, string tooltip)
		{
			Rect row = listing.GetRect(28f);
			TooltipHandler.TipRegion(row, tooltip);
			Widgets.Label(row.LeftPart(0.52f), $"{label}: {value:+0;-0;0}{unit}");
			value = Widgets.HorizontalSlider(row.RightPart(0.46f).ContractedBy(0f, 4f), value, min, max, roundTo: 1f);
		}
	}
}
