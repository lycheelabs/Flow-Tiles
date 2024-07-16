using System.Collections.Generic;
using UnityEngine;

namespace FlowField
{
	public static class MeshUtility
	{
		public static void MakeRectangle (ref List<Vector3> vertices, ref List<int> triangles, Vector3 position, float width, float height)
		{
			var vertexIndex = vertices.Count;
			vertices.Add(new Vector3(position.x, position.y, position.z));
			vertices.Add(new Vector3(position.x + width, position.y, position.z));
			vertices.Add(new Vector3(position.x, position.y, position.z + height));
			vertices.Add(new Vector3(position.x + width, position.y, position.z + height));

			triangles.Add(vertexIndex + 0);
			triangles.Add(vertexIndex + 2);
			triangles.Add(vertexIndex + 1);

			triangles.Add(vertexIndex + 1);
			triangles.Add(vertexIndex + 2);
			triangles.Add(vertexIndex + 3);
		}

		/// c .. d
		/// .    .
		/// .    .
		/// a .. b
		public static void MakeRectangle (ref List<Vector3> vertices, ref List<int> triangles, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
		{
			var vertexIndex = vertices.Count;
			vertices.Add(a.ToVector3());
			vertices.Add(b.ToVector3());
			vertices.Add(c.ToVector3());
			vertices.Add(d.ToVector3());

			triangles.Add(vertexIndex + 0);
			triangles.Add(vertexIndex + 2);
			triangles.Add(vertexIndex + 1);

			triangles.Add(vertexIndex + 1);
			triangles.Add(vertexIndex + 2);
			triangles.Add(vertexIndex + 3);
		}

		public static void MakeRectangle (ref List<Vector3> vertices, ref List<int> triangles, Vector2 from, Vector2 to, float width)
		{
			var temp = (Quaternion.Euler(0, 90f, 0) * (to.ToVector3() - from.ToVector3())).normalized;
			var shift = new Vector2(temp.x, temp.z) * width / 2f;

			MakeRectangle(ref vertices, ref triangles, from - shift, from + shift, to - shift, to + shift);
		}

		public static void MakeArrow (ref List<Vector3> vertices, ref List<int> triangles, Vector2 from, Vector2 to, float width)
		{
			MakeRectangle(ref vertices, ref triangles, from, to, width);

			var arrowShiftA = ((Quaternion.Euler(0, 30f, 0) * (to.ToVector3() - from.ToVector3())).normalized * width * 5).ToVector2();
			var arrowShiftB = ((Quaternion.Euler(0, -30f, 0) * (to.ToVector3() - from.ToVector3())).normalized * width * 5).ToVector2();
			MakeRectangle(ref vertices, ref triangles, to - arrowShiftA, to, width);
			MakeRectangle(ref vertices, ref triangles, to - arrowShiftB, to, width);
		}
	}
}