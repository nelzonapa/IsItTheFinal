using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace ImmersiveGraph.Collaboration
{
    public class CollabDashboardManager : MonoBehaviour
    {
        [Header("Referencias de la Interfaz")]
        public RectTransform mapCenter;
        public TextMeshProUGUI statusText;
        public Image headerPanel;

        [Header("Prefabs 2D")]
        public GameObject uiNodePrefab;
        public GameObject uiLinePrefab;

        [Header("Configuración de Proyección")]
        public float padding = 50f;
        public float maxZoomScale = 250f;
        public float nodeUISize = 40f;

        [Header("Configuración de Alertas")]
        public Color normalHeaderColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public Color alertHeaderColor = new Color(0.8f, 0.2f, 0.1f, 1f);

        private Dictionary<string, RectTransform> _activeUINodes = new Dictionary<string, RectTransform>();
        private float _canvasWidth;
        private float _canvasHeight;

        void Start()
        {
            if (mapCenter == null) return;

            RectTransform parentRect = mapCenter.parent.GetComponent<RectTransform>();
            if (parentRect != null)
            {
                _canvasWidth = parentRect.rect.width;
                _canvasHeight = parentRect.rect.height;
            }

            ResetDashboard();
        }

        void Update()
        {
            if (SharedWorkspaceTracker.Instance != null)
            {
                Render2DMap();
            }
        }

        public void SetCollabAlertState(bool isOccupied, string playerName = "")
        {
            if (isOccupied)
            {
                headerPanel.color = alertHeaderColor;
                statusText.text = $"MODIFICANDO: {playerName} está en la zona compartida";
                statusText.color = Color.white;
            }
            else
            {
                ResetDashboard();
            }
        }

        private void ResetDashboard()
        {
            if (headerPanel != null) headerPanel.color = normalHeaderColor;
            if (statusText != null)
            {
                statusText.text = "Espacio Colaborativo: Despejado";
                statusText.color = Color.green;
            }
        }

        private void Render2DMap()
        {
            var tracker = SharedWorkspaceTracker.Instance;
            var tokens = tracker.ActiveTokens;

            List<string> keysToRemove = new List<string>();
            foreach (var kvp in _activeUINodes)
            {
                if (!tokens.Exists(t => t.id == kvp.Key))
                {
                    if (kvp.Value != null) Destroy(kvp.Value.gameObject);
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove) _activeUINodes.Remove(key);

            if (tokens.Count == 0) return;

            float scaleX = (_canvasWidth - padding) / tracker.BoundingBoxSize.x;
            float scaleY = (_canvasHeight - padding) / tracker.BoundingBoxSize.y;
            float uniformScale = Mathf.Min(Mathf.Min(scaleX, scaleY), maxZoomScale);

            foreach (var token in tokens)
            {
                RectTransform uiRect;
                if (!_activeUINodes.TryGetValue(token.id, out uiRect))
                {
                    GameObject newUI = Instantiate(uiNodePrefab, mapCenter);
                    uiRect = newUI.GetComponent<RectTransform>();

                    // --- SOLUCIÓN 2: FORZAR ANCLAJES AL CENTRO ABSOLUTO ---
                    // Esto evita que el RectMask2D los oculte si el prefab estaba mal configurado
                    uiRect.anchorMin = new Vector2(0.5f, 0.5f);
                    uiRect.anchorMax = new Vector2(0.5f, 0.5f);
                    uiRect.pivot = new Vector2(0.5f, 0.5f);

                    uiRect.sizeDelta = new Vector2(nodeUISize, nodeUISize);
                    uiRect.localScale = Vector3.one;
                    uiRect.localRotation = Quaternion.identity;

                    _activeUINodes.Add(token.id, uiRect);
                }

                float relX = token.position.x - tracker.BoundingBoxCenter.x;
                float relZ = token.position.z - tracker.BoundingBoxCenter.y;

                float uiX = relX * uniformScale;
                float uiY = relZ * uniformScale;

                // --- SOLUCIÓN 3: USAR ANCHOREDPOSITION3D ---
                // Forzamos la profundidad Z explícitamente a 0 para que la máscara no lo corte
                uiRect.anchoredPosition3D = new Vector3(uiX, uiY, 0f);

                Image uiImage = uiRect.GetComponent<Image>();
                if (uiImage != null) uiImage.color = token.color;
            }
        }
    }
}