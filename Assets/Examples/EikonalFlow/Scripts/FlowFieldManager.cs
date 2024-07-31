using FlowTiles.FlowField;
using System.Linq;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace FlowField {
    public class FlowFieldManager : MonoBehaviour {

        public int Size = 10;
        public bool[,] Map;
        public Vector2Int[] Goal;

        public TMP_InputField SizeField;
        public Button ClearButton;
        public Button RandomizeButton;

        [SerializeField] public MapDisplayController MapDisplayController;
        [SerializeField] public CameraController CameraController;
        public FlowCalculationViewController FlowCalculationViewController;

        private void OnEnable() {
            ClearButton.onClick.AddListener(FullRefresh);
            RandomizeButton.onClick.AddListener(Randomize);

            SizeField.text = Size.ToString();
        }

        private void Randomize() {
            if (int.TryParse(SizeField.text, out var size) && size != Size) {
                Size = size;
                Map = new bool[Size, Size];
            }

            for (var i = 0; i < Size; i++) {
                for (var j = 0; j < Size; j++) {
                    if (Map[i, j]) {
                        SetBlockade(new Vector2Int(i, j), false);
                    }
                }
            }
            for (var i = 0; i < Size * Size * 0.2f; i++) {
                SetBlockade(new Vector2Int(Random.Range(0, Size), Random.Range(0, Size)), false);
            }

            SetEdgeAsSource();
            //SetSource(new Vector2Int(Random.Range(0, Size), Random.Range(0, Size)));
        }

        private void Start() {
            FullRefresh();
        }

        public void FullRefresh() {
            if (int.TryParse(SizeField.text, out var size)) {
                Size = size;
            }
            else {
                SizeField.text = Size.ToString();
            }
            Map = new bool[Size, Size];
            ViewRefresh();
        }

        public void SetSource(Vector2Int point, bool withRefresh = true) {
            Goal = new Vector2Int[] { point };
            foreach (var goal in Goal) {
                Map[goal.x, goal.y] = false;
            }
            if (withRefresh) {
                ViewRefresh();
            }
        }

        public void SetSource(Vector2Int[] points, bool withRefresh = true) {
            Goal = points;
            foreach (var goal in Goal) {
                Map[goal.x, goal.y] = false;
            }
            if (withRefresh) {
                ViewRefresh();
            }
        }

        public void SetEdgeAsSource (bool withRefresh = true) {
            var edge = new Vector2Int[Size];
            for (int i = 0; i < Size; i++) {
                edge[i] = new Vector2Int(i, 0);
                //break;
            }
            SetSource(edge, withRefresh);
        }

        public void SetBlockade(Vector2Int point, bool withRefresh = true) {
            if (!Goal.Contains(point)) {
                Map[point.x, point.y] = !Map[point.x, point.y];
                if (withRefresh) {
                    ViewRefresh();
                }
            }
        }

        private void ViewRefresh() {
            MapDisplayController.RefreshMap();
            CameraController.RefreshCamera();
            FlowCalculationViewController.Recalculate();
        }

        private void OnDisable() {
            ClearButton.onClick.RemoveListener(FullRefresh);
            RandomizeButton.onClick.RemoveListener(Randomize);
        }
    }
}