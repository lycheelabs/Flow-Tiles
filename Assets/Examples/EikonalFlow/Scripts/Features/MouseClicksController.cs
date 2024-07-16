using UnityEngine;
using UnityEngine.InputSystem;

namespace FlowField
{
	public class MouseClicksController : MonoBehaviour
	{
		private CameraController _cameraController;
		private FlowFieldManager _flowFieldManager;

		private Vector2Int? _lastProcessedRight;
		private Vector2Int? _lastProcessedLeft;

		private void Awake()
		{
			_flowFieldManager = GetComponentInParent<FlowFieldManager>();
			_cameraController = _flowFieldManager.CameraController;
		}

		private void Update()
		{
			if (Mouse.current.leftButton.isPressed && TryGetGridPoint(out var p1))
			{
				if (_lastProcessedLeft.HasValue == false || p1 != _lastProcessedLeft.Value)
				{
					_flowFieldManager.SetSource(p1);
					_lastProcessedLeft = p1;
				}
			}
			else if (Mouse.current.rightButton.isPressed && TryGetGridPoint(out var p2))
			{
				if (_lastProcessedRight.HasValue == false || p2 != _lastProcessedRight.Value)
				{
					_flowFieldManager.SetBlockade(p2);
					_lastProcessedRight = p2;
				}
			}

			if (Mouse.current.rightButton.isPressed == false)
			{
				_lastProcessedRight = null;
			}

			if (Mouse.current.leftButton.isPressed == false)
			{
				_lastProcessedLeft = null;
			}
		}

		private bool TryGetGridPoint (out Vector2Int point)
		{
			point = default;

			var ray = _cameraController.Camera.ScreenPointToRay(Input.mousePosition);
			if (Physics.Raycast(ray, out var hit) && TryTransformToGrid(hit.point, out var gridPoint))
			{
				point = gridPoint;
				return true;
			}

			return false;
		}

		private bool TryTransformToGrid (Vector3 point, out Vector2Int gridPoint)
		{
			var size = _flowFieldManager.Size;
			gridPoint = new Vector2Int((int)point.x, (int)point.z);
			return 0 <= gridPoint.x && gridPoint.x < size && 0 <= gridPoint.y && gridPoint.y < size;
		}
	}
}