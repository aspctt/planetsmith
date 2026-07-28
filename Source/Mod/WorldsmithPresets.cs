// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace Worldsmith
{
	/// <summary>
	/// Named sets of planet parameters, kept as individual files beside RimWorld's own
	/// saves so they outlive any one world and can be shared. A preset stores everything
	/// the world creation dialog can change, biome adjustments included.
	/// </summary>
	public static class WorldsmithPresets
	{
		private const string FileExtension = ".xml";
		private const string DocumentRoot = "worldsmithPreset";

		public static string FolderPath
		{
			get
			{
				string path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Worldsmith", "Presets");
				Directory.CreateDirectory(path);
				return path;
			}
		}

		/// <summary>Preset names, alphabetically. Empty when none have been saved.</summary>
		public static List<string> AllNames()
		{
			try
			{
				return new DirectoryInfo(FolderPath)
					.GetFiles("*" + FileExtension)
					.Select(f => Path.GetFileNameWithoutExtension(f.Name))
					.OrderBy(n => n)
					.ToList();
			}
			catch (Exception e)
			{
				Log.Error($"[Worldsmith] Could not list presets: {e}");
				return new List<string>();
			}
		}

		public static bool Exists(string name)
		{
			return File.Exists(PathFor(name));
		}

		public static bool Save(string name, WorldsmithWorldSettings settings)
		{
			if (name.NullOrEmpty() || settings == null)
			{
				return false;
			}
			try
			{
				WorldsmithWorldSettings copy = settings.Clone();
				Scribe.saver.InitSaving(PathFor(name), DocumentRoot);
				try
				{
					Scribe_Deep.Look(ref copy, "settings");
				}
				finally
				{
					Scribe.saver.FinalizeSaving();
				}
				return true;
			}
			catch (Exception e)
			{
				Log.Error($"[Worldsmith] Could not save preset '{name}': {e}");
				Scribe.ForceStop();
				return false;
			}
		}

		public static WorldsmithWorldSettings Load(string name)
		{
			string path = PathFor(name);
			if (!File.Exists(path))
			{
				return null;
			}
			WorldsmithWorldSettings loaded = null;
			try
			{
				Scribe.loader.InitLoading(path);
				try
				{
					Scribe_Deep.Look(ref loaded, "settings");
				}
				finally
				{
					Scribe.loader.FinalizeLoading();
				}
			}
			catch (Exception e)
			{
				Log.Error($"[Worldsmith] Could not load preset '{name}': {e}");
				Scribe.ForceStop();
				return null;
			}
			return loaded;
		}

		public static bool Delete(string name)
		{
			try
			{
				string path = PathFor(name);
				if (!File.Exists(path))
				{
					return false;
				}
				File.Delete(path);
				return true;
			}
			catch (Exception e)
			{
				Log.Error($"[Worldsmith] Could not delete preset '{name}': {e}");
				return false;
			}
		}

		/// <summary>Strips anything the filesystem would reject, so any typed name is usable.</summary>
		public static string SanitizeName(string name)
		{
			if (name.NullOrEmpty())
			{
				return string.Empty;
			}
			char[] invalid = Path.GetInvalidFileNameChars();
			var sanitized = name.Where(c => Array.IndexOf(invalid, c) < 0).ToArray();
			return new string(sanitized).Trim();
		}

		private static string PathFor(string name)
		{
			return Path.Combine(FolderPath, SanitizeName(name) + FileExtension);
		}
	}
}
