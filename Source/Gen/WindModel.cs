// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using UnityEngine;

namespace Worldsmith.Gen
{
	/// <summary>
	/// Prevailing surface wind as a function of latitude: the three classic zonal
	/// bands (tropical trade winds, mid-latitude westerlies, polar easterlies).
	/// Only the dominant east-west component is modelled for now. Shared so later
	/// moisture-transport passes can reuse the same wind field.
	/// </summary>
	public static class WindModel
	{
		/// <summary>+1 = wind blows toward the east, -1 = toward the west.</summary>
		public static float ZonalSign(float latitude)
		{
			float a = Mathf.Abs(latitude);
			if (a < 30f)
			{
				return -1f; // trade winds
			}
			if (a < 60f)
			{
				return 1f; // westerlies
			}
			return -1f; // polar easterlies
		}

		/// <summary>Unit tangent pointing east (increasing longitude) at a point on the globe.</summary>
		public static Vector3 EastTangent(Vector3 tileCenter)
		{
			Vector3 east = Vector3.Cross(Vector3.up, tileCenter.normalized);
			return east.sqrMagnitude < 1e-8f ? Vector3.zero : east.normalized;
		}

		/// <summary>Unit prevailing wind direction (the way the air moves) at a tile.</summary>
		public static Vector3 PrevailingWind(Vector3 tileCenter, float latitude)
		{
			return EastTangent(tileCenter) * ZonalSign(latitude);
		}
	}
}
