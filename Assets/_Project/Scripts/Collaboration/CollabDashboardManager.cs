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

        [Header("Prefabs 2D (Con script UIDashboardElement)")]
        public GameObject uiTokenPrefab;
        public GameObject uiPostItPrefab;
        public GameObject uiLinePrefab;

        [Header("Configuración de Proyección y Tamaños")]
        public float padding = 50f;
        public float maxZoomScale = 250f;
        [Tooltip("Tamaño de los círculos/cuadrados en pantalla (Auméntalo si se ven pequeños)")]
        public float nodeUISize = 120f; // <--- AUMENTADO A 120
        [Tooltip("Grosor de las líneas de conexión")]
        public float lineThickness = 12f; // <--- NUEVA VARIABLE PARA LÍNEAS GRUESAS

        [Header("Configuración de Alertas")]
        public Color normalHeaderColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public Color alertHeaderColor = new Color(0.8f, 0.2f, 0.1f, 1f);

        // Diccionarios separados para Nodos y Líneas
        private Dictionary<string, UIDashboardElement> _activeUINodes = new Dictionary<string, UIDashboardElement>();
        private Dictionary<string, UIDashboardElement> _activeUILines = new Dictionary<string, UIDashboardElement>();

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

            // 1. FACTOR DE ESCALA MATEMÁTICO (Para las posiciones)
            float scaleX = (_canvasWidth - padding) / tracker.BoundingBoxSize.x;
            float scaleY = (_canvasHeight - padding) / tracker.BoundingBoxSize.y;
            float uniformScale = Mathf.Min(Mathf.Min(scaleX, scaleY), maxZoomScale);

            // ==========================================
            // 2. DIBUJAR NODOS (TOKENS Y POSTITS)
            // ==========================================
            var nodes = tracker.ActiveNodes;
            List<string> nodeKeysToRemove = new List<string>();

            foreach (var kvp in _activeUINodes)
            {
                if (!nodes.Exists(n => n.id == kvp.Key))
                {
                    if (kvp.Value != null) Destroy(kvp.Value.gameObject);
                    nodeKeysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in nodeKeysToRemove) _activeUINodes.Remove(key);

            foreach (var node in nodes)
            {
                UIDashboardElement uiElement;
                if (!_activeUINodes.TryGetValue(node.id, out uiElement))
                {
                    GameObject prefabToUse = node.type == UIDashboardElement.ElementType.Token ? uiTokenPrefab : uiPostItPrefab;
                    GameObject newUI = Instantiate(prefabToUse, mapCenter);

                    uiElement = newUI.GetComponent<UIDashboardElement>();

                    // Forzar anclajes al centro absoluto
                    uiElement.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    uiElement.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    uiElement.rectTransform.pivot = new Vector2(0.5f, 0.5f);

                    // APLICAMOS EL TAMAÑO CORRECTO (Width x Height)
                    uiElement.rectTransform.sizeDelta = new Vector2(nodeUISize, nodeUISize);

                    uiElement.rectTransform.anchoredPosition3D = Vector3.zero;
                    uiElement.rectTransform.localScale = Vector3.one;
                    uiElement.rectTransform.localRotation = Quaternion.identity;

                    _activeUINodes.Add(node.id, uiElement);
                }

                // --- ACTUALIZAR DATOS CONSTANTEMENTE (Por si cambian de color o de texto) ---
                uiElement.Setup(node.id, node.color, node.textContent, node.originDocumentId);

                // Calcular posición proyectada
                float relX = node.position.x - tracker.BoundingBoxCenter.x;
                float relZ = node.position.z - tracker.BoundingBoxCenter.y;

                float uiX = relX * uniformScale;
                float uiY = relZ * uniformScale;

                // Mover suavemente el UI
                Vector2 targetPos = new Vector2(uiX, uiY);
                uiElement.rectTransform.anchoredPosition = Vector2.Lerp(uiElement.rectTransform.anchoredPosition, targetPos, Time.deltaTime * 15f);
            }

            // ==========================================
            // 3. DIBUJAR LÍNEAS DE CONEXIÓN
            // ==========================================
            var lines = tracker.ActiveLines;
            List<string> lineKeysToRemove = new List<string>();

            foreach (var kvp in _activeUILines)
            {
                if (!lines.Exists(l => l.id == kvp.Key))
                {
                    if (kvp.Value != null) Destroy(kvp.Value.gameObject);
                    lineKeysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in lineKeysToRemove) _activeUILines.Remove(key);

            foreach (var kvp in _activeUILines) kvp.Value.rectTransform.SetAsFirstSibling();

            foreach (var line in lines)
            {
                if (!_activeUINodes.ContainsKey(line.startNodeId) || !_activeUINodes.ContainsKey(line.endNodeId)) continue;

                UIDashboardElement uiLine;
                if (!_activeUILines.TryGetValue(line.id, out uiLine))
                {
                    GameObject newUI = Instantiate(uiLinePrefab, mapCenter);
                    uiLine = newUI.GetComponent<UIDashboardElement>();
                    uiLine.Setup(line.id, Color.white);

                    uiLine.rectTransform.anchoredPosition3D = Vector3.zero;
                    uiLine.rectTransform.localScale = Vector3.one;

                    _activeUILines.Add(line.id, uiLine);
                }

                Vector2 startPos = _activeUINodes[line.startNodeId].rectTransform.anchoredPosition;
                Vector2 endPos = _activeUINodes[line.endNodeId].rectTransform.anchoredPosition;

                Vector2 direction = endPos - startPos;
                float distance = direction.magnitude;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                uiLine.rectTransform.anchoredPosition = startPos;
                // APLICAMOS LA DISTANCIA Y EL GROSOR DE LÍNEA CONFIGURABLE
                uiLine.rectTransform.sizeDelta = new Vector2(distance, lineThickness);
                uiLine.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }
}