// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using UnityEngine;

namespace Planetsmith.Generation;

/// <summary>
/// Prevailing surface wind as a function of latitude: the three classic zonal
/// bands (tropical trade winds, mid-latitude westerlies, polar easterlies).
/// Only the dominant east-west component is modelled for now. Shared so later
/// moisture-transport passes can reuse the same wind field.
/// </summary>
public static class WindModel
{
	//// Constants
	
	// Latitudes where the trade winds give way to the westerlies, and the westerlies to
	// the polar easterlies.
	private const float TRADE_WIND_LIMIT = 30f;
	private const float WESTERLY_LIMIT = 60f;
	
	// Below this squared length there is no east to speak of, as at the poles.
	private const float MINIMUM_EAST_SQUARED = 1e-8f;
	
	//// Public API
	
	/// <summary>+1 = wind blows toward the east, -1 = toward the west.</summary>
	public static float ZonalSign(float latitude)
	{
		float absoluteLatitude = Mathf.Abs(latitude);
		if (absoluteLatitude < TRADE_WIND_LIMIT)
		{
			return -1f; // trade winds
		}
		if (absoluteLatitude < WESTERLY_LIMIT)
		{
			return 1f; // westerlies
		}
		return -1f; // polar easterlies
	}
	
	/// <summary>Unit tangent pointing east (increasing longitude) at a point on the globe.</summary>
	public static Vector3 EastTangent(Vector3 tileCenter)
	{
		// The centre crossed with the axis, in that order, is what points toward increasing
		// longitude, which RimWorld labels east and gives a later local time. The other order
		// pointed west and turned every prevailing wind around.
		Vector3 east = Vector3.Cross(tileCenter.normalized, Vector3.up);
		return east.sqrMagnitude < MINIMUM_EAST_SQUARED ? Vector3.zero : east.normalized;
	}
	
	/// <summary>Unit prevailing wind direction (the way the air moves) at a tile.</summary>
	public static Vector3 PrevailingWind(Vector3 tileCenter, float latitude)
	{
		return EastTangent(tileCenter) * ZonalSign(latitude);
	}
}
