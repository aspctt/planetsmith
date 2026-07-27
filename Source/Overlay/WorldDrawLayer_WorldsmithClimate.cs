// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 aspctt
using System.Collections;
using RimWorld.Planet;
using Unity.Collections;
using UnityEngine;
using Verse;

namespace Worldsmith.Overlay
{
	/// <summary>
	/// Colours every surface tile by its generated temperature or rainfall. Registered
	/// on the Surface planet layer via a Def patch and driven by <see cref="WorldsmithOverlay"/>;
	/// only draws when a mode is active. This is a debug/inspection tool, not a gameplay layer.
	/// </summary>
	public class WorldDrawLayer_WorldsmithClimate : WorldDrawLayer
	{
		private const byte OverlayAlpha = 205;

		private Material material;

		public override bool Visible => base.Visible && WorldsmithOverlay.Mode != OverlayMode.None;

		public override bool VisibleWhenLayerNotSelected => false;

		public override bool VisibleInBackground => false;

		public override IEnumerable Regenerate()
		{
			foreach (object item in base.Regenerate())
			{
				yield return item;
			}

			if (WorldsmithOverlay.Mode == OverlayMode.None)
			{
				FinalizeMesh(MeshParts.All);
				yield break;
			}

			if (material == null)
			{
				material = new Material(WorldMaterials.VertexColorTransparent);
			}

			NativeArray<int> vertsOffsets = planetLayer.UnsafeTileIDToVerts_offsets;
			NativeArray<Vector3> verts = planetLayer.UnsafeVerts;
			var tiles = planetLayer.Tiles;
			int tileCount = Mathf.Min(planetLayer.TilesCount, tiles.Count);

			for (int i = 0; i < tileCount; i++)
			{
				Color32 color = WorldsmithOverlay.ColorFor(tiles[i]);
				LayerSubMesh subMesh = GetSubMesh(material, out _);
				int baseVert = subMesh.verts.Count;
				int local = 0;
				int end = (i + 1 < vertsOffsets.Length) ? vertsOffsets[i + 1] : verts.Length;
				for (int j = vertsOffsets[i]; j < end; j++)
				{
					subMesh.verts.Add(verts[j]);
					subMesh.colors.Add(new Color32(color.r, color.g, color.b, OverlayAlpha));
					if (j < end - 2)
					{
						subMesh.tris.Add(baseVert + local + 2);
						subMesh.tris.Add(baseVert + local + 1);
						subMesh.tris.Add(baseVert);
					}
					local++;
				}
			}

			FinalizeMesh(MeshParts.All);
		}
	}
}
