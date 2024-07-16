using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

namespace FlowField
{
	public class MapDisplayController : MonoBehaviour
	{
		public MeshFilter LinesMeshFilter;
		public MeshFilter SourceMeshFilter;
		public MeshFilter BlockedMeshFilter;

		private FlowFieldManager _flowFieldManager;

		private Mesh _linesMesh;
		private Mesh _blockedMesh;
		private Mesh _sourceMesh;

		private void Awake()
		{
			_flowFieldManager = GetComponentInParent<FlowFieldManager>();
		}

		public void RefreshMap ()
		{
			BuildLinesMesh();
			BuildSourceMesh();
			BuildBlockedMesh();
		}

		private void BuildLinesMesh ()
		{
			var size = _flowFieldManager.Size;

			var lineSize = 0.1f;

			_linesMesh = new();

			var vertices = new List<Vector3>();
			var triangles = new List<int>();

			for (var x = 0; x <= size; x += 1)
			{
				MeshUtility.MakeRectangle(ref vertices, ref triangles, new Vector3(x - lineSize / 2f, 0.01f, 0), lineSize, size);
			}

			for (var y = 0; y <= size; y += 1)
			{
				MeshUtility.MakeRectangle(ref vertices, ref triangles, new Vector3(0, 0.01f, y - lineSize / 2f), size, lineSize);
			}

			_linesMesh.SetVertices(vertices);
			_linesMesh.SetTriangles(triangles, 0);
			_linesMesh.RecalculateNormals();
			LinesMeshFilter.mesh = _linesMesh;
		}

		private void BuildSourceMesh ()
		{
			var sources = _flowFieldManager.Goal;
			var size = sources.Length;

			var vertices = new List<Vector3>();
			var triangles = new List<int>();
			_sourceMesh = new();

			for (var i = 0; i < size; i += 1) {
				MeshUtility.MakeRectangle(ref vertices, ref triangles, new Vector3(sources[i].x, 0f, sources[i].y), 1, 1);
			}

			_sourceMesh.SetVertices(vertices);
			_sourceMesh.SetTriangles(triangles, 0);
			_sourceMesh.RecalculateNormals();
			SourceMeshFilter.mesh = _sourceMesh;
		}

		private void BuildBlockedMesh ()
		{
			var size = _flowFieldManager.Size;

			var vertices = new List<Vector3>();
			var triangles = new List<int>();
			_blockedMesh = new();

			for (var x = 0; x < size; x++)
			{
				for (var y = 0; y < size; y++)
				{
					if (_flowFieldManager.Map[x, y])
					{
						MeshUtility.MakeRectangle(ref vertices, ref triangles, new Vector3(x, 0f, y), 1, 1);
					}
				}
			}

			_blockedMesh.SetVertices(vertices);
			_blockedMesh.SetTriangles(triangles, 0);
			_blockedMesh.RecalculateNormals();
			BlockedMeshFilter.mesh = _blockedMesh;
		}
	}
}