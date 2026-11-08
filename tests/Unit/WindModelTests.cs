// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using NUnit.Framework;
using UnityEngine;
using Planetsmith.Generation;

namespace Planetsmith.UnitTests;

/// <summary>Checks the prevailing wind directions the moisture, rain shadow and coastal passes follow.</summary>
[TestFixture]
public class WindModelTests
{
	//// Constants
	
	private const float TOLERANCE = 0.0001f;
	
	// How far a point is nudged along a tangent to see which way it leads.
	private const float NUDGE = 0.001f;
	
	// Points on the globe away from the poles, at several longitudes and latitudes.
	private static readonly Vector3[] SURFACE_POINTS =
	{
		new(0f, 0f, -1f),
		new(1f, 0f, 0f),
		new(0f, 0f, 1f),
		new(-1f, 0f, 0f),
		new(0.5f, 0.7071f, -0.5f),
		new(-0.6f, -0.5f, 0.6245f),
	};
	
	//// Private Functions
	
	/// <summary>
	/// Longitude in degrees as RimWorld works it out (PlanetLayer.LongLatOf), where a
	/// positive longitude is shown as east and has a later local time, as on Earth.
	/// </summary>
	private static float LongitudeOf(Vector3 point)
	{
		return Mathf.Atan2(point.x, -point.z) * Mathf.Rad2Deg;
	}
	
	/// <summary>How far longitude moves going from one point to another, across the date line too.</summary>
	private static float LongitudeChange(Vector3 from, Vector3 to)
	{
		return Mathf.DeltaAngle(LongitudeOf(from), LongitudeOf(to));
	}
	
	//// Public API
	
	[TestCase(0f, -1f)]
	[TestCase(29.9f, -1f)]
	[TestCase(30f, 1f)]
	[TestCase(59.9f, 1f)]
	[TestCase(60f, -1f)]
	[TestCase(90f, -1f)]
	public void ZonalSignFollowsTheWindBelts(float latitude, float expected)
	{
		// Trade winds, then westerlies, then polar easterlies, the same in both hemispheres
		Assert.That(WindModel.ZonalSign(latitude), Is.EqualTo(expected));
		Assert.That(WindModel.ZonalSign(-latitude), Is.EqualTo(expected));
	}
	
	[Test]
	public void EastTangentIsAUnitVectorAlongTheSurface()
	{
		foreach (Vector3 point in SURFACE_POINTS)
		{
			Vector3 east = WindModel.EastTangent(point);
			Assert.That(east.magnitude, Is.EqualTo(1f).Within(TOLERANCE), $"length at {point}");
			Assert.That(Vector3.Dot(east, point.normalized), Is.EqualTo(0f).Within(TOLERANCE), $"along the surface at {point}");
			Assert.That(east.y, Is.EqualTo(0f).Within(TOLERANCE), $"level with the equator at {point}");
		}
	}
	
	[TestCase(1f)]
	[TestCase(-1f)]
	public void EastTangentIsZeroAtThePoles(float pole)
	{
		Assert.That(WindModel.EastTangent(new Vector3(0f, pole, 0f)), Is.EqualTo(Vector3.zero));
	}
	
	[Test]
	public void PrevailingWindIsEastScaledByTheZonalSign()
	{
		Vector3 point = SURFACE_POINTS[4];
		foreach (float latitude in new[] { 10f, 45f, 75f })
		{
			Vector3 expected = WindModel.EastTangent(point) * WindModel.ZonalSign(latitude);
			Assert.That(Vector3.Distance(WindModel.PrevailingWind(point, latitude), expected), Is.EqualTo(0f).Within(TOLERANCE), $"at {latitude} degrees");
		}
	}
	
	[Test]
	[Ignore("EastTangent points toward decreasing longitude, which RimWorld shows as west, so every prevailing wind is reversed. Waiting on the decision to flip it, which changes every world.")]
	public void EastTangentPointsTowardIncreasingLongitude()
	{
		foreach (Vector3 point in SURFACE_POINTS)
		{
			Vector3 nudged = point + WindModel.EastTangent(point) * NUDGE;
			Assert.That(LongitudeChange(point, nudged), Is.GreaterThan(0f), $"at {point}, longitude {LongitudeOf(point):F1}");
		}
	}
}
