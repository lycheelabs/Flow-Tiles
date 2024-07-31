using FlowField;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace FlowTiles.FlowField {
    public class FlowCalculationViewController : MonoBehaviour {

        public MeshFilter FlowMeshFilter;

        public TextMeshPro TextPrefab;
        public TextMeshProUGUI PerformanceText;
        public Transform TextsContainer;
        public Button ToggleTextsButton;

        private IEnumerator _enumerator;
        private Mesh _flowMesh;
        private TextMeshPro[,] _gradientTexts;

        private FlowFieldManager _flowFieldManager;

        private FlowFieldTile _result;
        private int _width;
        private int _height;
        private bool _requiresDispose = false;

        private void Awake() {
            _flowFieldManager = GetComponentInParent<FlowFieldManager>();
        }

        private void OnEnable() {
            ToggleTextsButton.onClick.AddListener(ToggleTexts);
        }

        private void ToggleTexts() {
            TextsContainer.gameObject.SetActive(!TextsContainer.gameObject.activeSelf);
        }

        public void Recalculate() {
            DestroyOldTexts();
            SpawnNewTexts();

            Calculate();
            DisplayFlow();
        }

        private void DestroyOldTexts() {
            if (_gradientTexts != null) {
                for (var x = 0; x < _gradientTexts.GetLength(0); x++) {
                    for (var y = 0; y < _gradientTexts.GetLength(1); y++) {
                        Destroy(_gradientTexts[x, y]);
                    }
                }
            }
        }

        private void SpawnNewTexts() {
            _gradientTexts = new TextMeshPro[_flowFieldManager.Size, _flowFieldManager.Size];

            for (var x = 0; x < _flowFieldManager.Size; x++) {
                for (var y = 0; y < _flowFieldManager.Size; y++) {
                    _gradientTexts[x, y] = Instantiate(TextPrefab, new Vector3(x + 0.5f, 0.02f, y + 0.5f),
                        Quaternion.Euler(90, 0, 0), TextsContainer);
                }
            }
        }
        private void Calculate() {
            var size = _flowFieldManager.Size;

            var speeds = new float[size + 2, size + 2];

            for (var x = 1; x <= size; x++) {
                for (var y = 1; y <= size; y++) {
                    if (_flowFieldManager.Map[x - 1, y - 1])
                        speeds[x, y] = -1f;
                    else
                        speeds[x, y] = 1f;
                }
            }

            for (var i = 0; i <= size; i++) {
                speeds[0, i] = speeds[size + 1, i] = speeds[i, 0] = speeds[i, size + 1] = 0;
            }

            if (_requiresDispose) {
                _result.Dispose(); ;
            }

            var goal = _flowFieldManager.Goal;
            _width = size;
            _height = size;
            _result = FlowCalculationController.RequestCalculation(speeds, goal, size, size);

            PerformanceText.SetText($"Calculated in {_result.GenerationTime:mm':'ss':'fff}");
            _requiresDispose = true;
            RefreshTexts();
        }

        private void DisplayFlow() {
            var size = _flowFieldManager.Size;
            _flowMesh = new Mesh();

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (var x = 0; x < size; x++) {
                for (var y = 0; y < size; y++) {
                    var shift = _result.Directions[GetIndex(x + 1, y + 1)];

                    var from = new double2(x + 0.5, y + 0.5);
                    var to = from + shift * 0.4;
                    MeshUtility.MakeArrow(ref vertices, ref triangles, new Vector2((float)from.x, (float)from.y), new Vector2((float)to.x, (float)to.y), 0.03f);
                }
            }

            _flowMesh.SetVertices(vertices);
            _flowMesh.SetTriangles(triangles, 0);
            _flowMesh.RecalculateNormals();
            FlowMeshFilter.mesh = _flowMesh;
        }

        private int GetIndex(int x, int y) {
            return y * (_width + 2) + x;
        }
        private void RefreshTexts() {
            for (var x = 0; x < _flowFieldManager.Size; x++) {
                for (var y = 0; y < _flowFieldManager.Size; y++) {
                    _gradientTexts[x, y].SetText($"{_result.Gradients[GetIndex(x + 1, y + 1)]:0.00}");
                }
            }
        }

        private void OnDisable() {
            ToggleTextsButton.onClick.RemoveListener(ToggleTexts);

            if (_requiresDispose) {
                _result.Dispose();
            }
        }

    }
}