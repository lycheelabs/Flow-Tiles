using UnityEngine;

namespace FlowField
{
	public static class VectorExtensions
	{
		public static Vector3 ToVector3(this Vector2 vec)
		{
			var result = Vector3.zero;
			result.x = vec.x;
			result.y = 0.1f;
			result.z = vec.y;
			return result;
		}

		public static Vector2 ToVector2(this Vector3 vec)
		{
			var result = Vector2.zero;
			result.x = vec.x;
			result.y = vec.z;
			return result;
		}
	}
}