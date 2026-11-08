// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using NUnit.Framework;
using Planetsmith.Generation;

namespace Planetsmith.UnitTests;

/// <summary>Checks the bell curve the biome and climate scoring is built on.</summary>
[TestFixture]
public class ClimateMathTests
{
	//// Constants
	
	private const float TOLERANCE = 0.0001f;
	
	//// Public API
	
	[Test]
	public void GaussianIsOneOnTheMean()
	{
		Assert.That(ClimateMath.Gaussian(25f, 25f, 10f), Is.EqualTo(1f).Within(TOLERANCE));
	}
	
	[TestCase(35f)]
	[TestCase(15f)]
	public void GaussianFallsToOneOverEOneSpreadAway(float value)
	{
		// The documented contract: 1/e a spread away on either side
		Assert.That(ClimateMath.Gaussian(value, 25f, 10f), Is.EqualTo(0.36788f).Within(TOLERANCE));
	}
	
	[Test]
	public void GaussianIsSymmetric()
	{
		Assert.That(ClimateMath.Gaussian(18f, 25f, 4f), Is.EqualTo(ClimateMath.Gaussian(32f, 25f, 4f)).Within(TOLERANCE));
	}
}
