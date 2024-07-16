using UnityEngine;

namespace FlowField
{
	public class CameraController : MonoBehaviour
	{
		[SerializeField] public Camera Camera;

		private FlowFieldManager _flowFieldManager;

		private void Awake()
		{
			_flowFieldManager = GetComponentInParent<FlowFieldManager>();
		}

		private void OnEnable()
		{
			RefreshCamera();
		}

		public void RefreshCamera ()
		{
			var size = _flowFieldManager.Size;
			Camera.transform.position = new Vector3(size / 2f, size * Mathf.Sqrt(2) * 0.7f, size / 2f);
		}
	}
}