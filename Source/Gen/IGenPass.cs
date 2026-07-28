// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
namespace Planetsmith.Gen
{
	/// <summary>
	/// One stage of the world-generation pipeline. Passes run in order, each
	/// reading and refining the shared <see cref="GenContext"/> and the tiles on
	/// its layer. Keeping stages behind this interface lets the climate model grow
	/// from a couple of passes into the full dependency-ordered layer set.
	/// </summary>
	public interface IGenPass
	{
		string Name { get; }

		void Run(GenContext ctx);
	}
}
