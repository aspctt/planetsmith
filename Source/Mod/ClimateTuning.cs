// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using Verse;

namespace Planetsmith
{
	/// <summary>
	/// Dials on the climate model for one planet. Everything here is a multiplier or an
	/// offset around the model's own behaviour, so the defaults leave a world exactly as
	/// Planetsmith would have made it anyway, and nothing has to be understood before a
	/// planet can be generated.
	/// </summary>
	public class ClimateTuning : IExposable
	{
		/// <summary>Degrees added to every tile, before anything else is worked out.</summary>
		public float temperatureOffset = 0f;

		/// <summary>How sharply the air cools with height.</summary>
		public float elevationCooling = 1f;

		/// <summary>Overall wetness of the planet.</summary>
		public float rainfallScale = 1f;

		/// <summary>How thoroughly mountains wring out the air crossing them.</summary>
		public float rainShadow = 1f;

		/// <summary>How far inland sea moisture is carried before it runs out.</summary>
		public float moistureReach = 1f;

		/// <summary>Strength of the seasonal rains in the tropics.</summary>
		public float monsoonStrength = 1f;

		/// <summary>How far temperatures swing between summer and winter.</summary>
		public float seasonIntensity = 1f;

		/// <summary>How strongly the sea tempers the coasts beside it.</summary>
		public float coastalInfluence = 1f;

		public bool IsDefault =>
			temperatureOffset == 0f
			&& elevationCooling == 1f
			&& rainfallScale == 1f
			&& rainShadow == 1f
			&& moistureReach == 1f
			&& monsoonStrength == 1f
			&& seasonIntensity == 1f
			&& coastalInfluence == 1f;

		public void Reset()
		{
			temperatureOffset = 0f;
			elevationCooling = 1f;
			rainfallScale = 1f;
			rainShadow = 1f;
			moistureReach = 1f;
			monsoonStrength = 1f;
			seasonIntensity = 1f;
			coastalInfluence = 1f;
		}

		public ClimateTuning Clone()
		{
			return new ClimateTuning
			{
				temperatureOffset = temperatureOffset,
				elevationCooling = elevationCooling,
				rainfallScale = rainfallScale,
				rainShadow = rainShadow,
				moistureReach = moistureReach,
				monsoonStrength = monsoonStrength,
				seasonIntensity = seasonIntensity,
				coastalInfluence = coastalInfluence,
			};
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref temperatureOffset, "temperatureOffset", 0f);
			Scribe_Values.Look(ref elevationCooling, "elevationCooling", 1f);
			Scribe_Values.Look(ref rainfallScale, "rainfallScale", 1f);
			Scribe_Values.Look(ref rainShadow, "rainShadow", 1f);
			Scribe_Values.Look(ref moistureReach, "moistureReach", 1f);
			Scribe_Values.Look(ref monsoonStrength, "monsoonStrength", 1f);
			Scribe_Values.Look(ref seasonIntensity, "seasonIntensity", 1f);
			Scribe_Values.Look(ref coastalInfluence, "coastalInfluence", 1f);
		}
	}
}
