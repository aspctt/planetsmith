// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Planetsmith.UI
{
	/// <summary>
	/// Saves the current planet parameters under a name, and brings saved ones back.
	/// Presets live outside any world, so a favourite set of settings can be reused for
	/// every planet you make afterwards.
	/// </summary>
	public class Dialog_PlanetsmithPresets : Window
	{
		private const float RowHeight = 34f;

		private string nameBuffer = string.Empty;
		private Vector2 scrollPosition;
		private List<string> presets;

		public Dialog_PlanetsmithPresets()
		{
			forcePause = true;
			absorbInputAroundWindow = true;
			doCloseX = true;
			closeOnClickedOutside = true;
		}

		public override Vector2 InitialSize => new Vector2(560f, 520f);

		public override void PreOpen()
		{
			base.PreOpen();
			Refresh();
		}

		private void Refresh()
		{
			presets = PlanetsmithPresets.AllNames();
		}

		public override void DoWindowContents(Rect inRect)
		{
			float y = inRect.y;

			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 36f), "Planetsmith: presets");
			Text.Font = GameFont.Small;
			y += 44f;

			// Save row.
			var fieldRect = new Rect(inRect.x, y, inRect.width - 130f, 30f);
			nameBuffer = Widgets.TextField(fieldRect, nameBuffer);
			var saveRect = new Rect(fieldRect.xMax + 10f, y, 120f, 30f);
			string trimmed = PlanetsmithPresets.SanitizeName(nameBuffer);
			bool canSave = !trimmed.NullOrEmpty();
			if (!canSave)
			{
				GUI.color = new Color(1f, 1f, 1f, 0.4f);
			}
			if (Widgets.ButtonText(saveRect, PlanetsmithPresets.Exists(trimmed) ? "Overwrite" : "Save") && canSave)
			{
				if (PlanetsmithPresets.Save(trimmed, PlanetsmithWorldParams.Pending))
				{
					Messages.Message($"Saved preset '{trimmed}'.", MessageTypeDefOf.TaskCompletion, historical: false);
					nameBuffer = string.Empty;
					Refresh();
				}
			}
			GUI.color = Color.white;
			y += 40f;

			Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
			y += 8f;

			if (presets.Count == 0)
			{
				GUI.color = new Color(1f, 1f, 1f, 0.5f);
				Widgets.Label(new Rect(inRect.x, y, inRect.width, 30f), "No presets saved yet.");
				GUI.color = Color.white;
				return;
			}

			var outRect = new Rect(inRect.x, y, inRect.width, inRect.height - y - 8f);
			var viewRect = new Rect(0f, 0f, outRect.width - 20f, presets.Count * RowHeight);
			Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

			float rowY = 0f;
			string toDelete = null;
			for (int i = 0; i < presets.Count; i++)
			{
				string preset = presets[i];
				var rowRect = new Rect(0f, rowY, viewRect.width, RowHeight);
				if (Mouse.IsOver(rowRect))
				{
					Widgets.DrawHighlight(rowRect);
				}

				Widgets.Label(new Rect(rowRect.x, rowRect.y + 5f, rowRect.width - 190f, 26f), preset);

				var loadRect = new Rect(rowRect.xMax - 185f, rowRect.y + 2f, 90f, 28f);
				if (Widgets.ButtonText(loadRect, "Load"))
				{
					PlanetsmithWorldSettings loaded = PlanetsmithPresets.Load(preset);
					if (loaded != null)
					{
						PlanetsmithWorldParams.Pending = loaded;
						Messages.Message($"Loaded preset '{preset}'.", MessageTypeDefOf.TaskCompletion, historical: false);
					}
					else
					{
						Messages.Message($"Could not load preset '{preset}'.", MessageTypeDefOf.RejectInput, historical: false);
					}
				}

				var deleteRect = new Rect(rowRect.xMax - 90f, rowRect.y + 2f, 90f, 28f);
				if (Widgets.ButtonText(deleteRect, "Delete"))
				{
					toDelete = preset;
				}

				rowY += RowHeight;
			}
			Widgets.EndScrollView();

			if (toDelete != null)
			{
				string target = toDelete;
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					$"Delete the preset '{target}'?",
					delegate
					{
						PlanetsmithPresets.Delete(target);
						Refresh();
					},
					destructive: true));
			}
		}
	}
}
