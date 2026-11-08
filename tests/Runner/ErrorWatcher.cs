// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt

//// Dependencies

using System.Collections.Generic;
using UnityEngine;

namespace Planetsmith.TestRunner;

/// <summary>
/// Collects the errors logged while a world generates, from any mod. A pass that throws
/// is caught and logged rather than stopping generation, so a broken world still comes
/// out looking finished; counting the errors is how a test notices.
/// </summary>
public static class ErrorWatcher
{
	//// Constants
	
	// Enough to see what went wrong without one repeating error filling the results.
	private const int KEPT_ERRORS = 20;
	
	//// References and State
	
	// The threaded event is the one to use: world generation runs on a worker thread, and
	// the plain logMessageReceived only reports messages logged on the main thread.
	// Messages can arrive from both at once, hence the lock.
	private static readonly object GATE = new();
	private static readonly List<string> errors = new();
	private static int errorCount;
	private static bool isWatching;
	
	//// Private Functions
	
	private static void OnLogMessage(string condition, string stackTrace, LogType type)
	{
		// Sanity check
		if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
		{
			return;
		}
		
		lock (GATE)
		{
			errorCount++;
			if (errors.Count < KEPT_ERRORS)
			{
				errors.Add(condition);
			}
		}
	}
	
	//// Public API
	
	public static void Start()
	{
		// Sanity check
		if (isWatching)
		{
			return;
		}
		
		isWatching = true;
		Application.logMessageReceivedThreaded += OnLogMessage;
	}
	
	public static void Stop()
	{
		// Sanity check
		if (!isWatching)
		{
			return;
		}
		
		isWatching = false;
		Application.logMessageReceivedThreaded -= OnLogMessage;
	}
	
	/// <summary>Moves the errors collected so far into a report and starts counting afresh.</summary>
	public static void TakeInto(WorldReport report)
	{
		lock (GATE)
		{
			report.errorCount = errorCount;
			report.errors = new List<string>(errors);
			errorCount = 0;
			errors.Clear();
		}
	}
}
