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

        [Header("Configuración de Proyección")]
        public float padding = 50f;
        public float maxZoomScale = 250f;

        [Header("Configuración de Escalado Dinámico")]
        public float nodeSizeInMeters = 0.8f;
        public float minNodePixelSize = 40f;
        public float maxNodePixelSize = 300f;

        public float lineThicknessInMeters = 0.08f;
        public float minLinePixelThickness = 4f;
        public float maxLinePixelThickness = 25f;

        [Header("Algoritmo Anti-Solapamiento (Nuevo)")]
        public bool enableAntiOverlap = true;
        [Tooltip("Espacio extra en píxeles que tratarán de mantener entre sí.")]
        public float repulsionPadding = 15f;
        [Tooltip("Precisión del algoritmo. 3 o 5 es ideal.")]
        public int relaxationSteps = 3;

        [Header("Configuración de Alertas")]
        public Color normalHeaderColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public Color alertHeaderColor = new Color(0.8f, 0.2f, 0.1f, 1f);

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

            // 1. FACTOR DE ESCALA MATEMÁTICO
            float scaleX = (_canvasWidth - padding) / tracker.BoundingBoxSize.x;
            float scaleY = (_canvasHeight - padding) / tracker.BoundingBoxSize.y;
            float uniformScale = Mathf.Min(Mathf.Min(scaleX, scaleY), maxZoomScale);

            float dynamicNodeSize = Mathf.Clamp(nodeSizeInMeters * uniformScale, minNodePixelSize, maxNodePixelSize);
            float dynamicLineThickness = Mathf.Clamp(lineThicknessInMeters * uniformScale, minLinePixelThickness, maxLinePixelThickness);

            var nodes = tracker.ActiveNodes;

            // ==========================================
            // NUEVO: FASE DE CÁLCULO DE POSICIONES Y RELAJACIÓN
            // ==========================================
            Dictionary<string, Vector2> targetPositions = new Dictionary<string, Vector2>();

            // Mapeo inicial
            foreach (var node in nodes)
            {
                float relX = node.position.x - tracker.BoundingBoxCenter.x;
                float relZ = node.position.z - tracker.BoundingBoxCenter.y;
                targetPositions[node.id] = new Vector2(relX * uniformScale, relZ * uniformScale);
            }

            // Algoritmo de Repulsión (Anti-Overlap)
            if (enableAntiOverlap && nodes.Count > 1)
            {
                float minDistance = dynamicNodeSize + repulsionPadding;

                for (int step = 0; step < relaxationSteps; step++)
                {
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        for (int j = i + 1; j < nodes.Count; j++)
                        {
                            string idA = nodes[i].id;
                            string idB = nodes[j].id;

                            Vector2 posA = targetPositions[idA];
                            Vector2 posB = targetPositions[idB];

                            Vector2 diff = posA - posB;
                            float dist = diff.magnitude;

                            if (dist < minDistance)
                            {
                                // Evitar división por cero si están exactamente en el mismo pixel
                                if (dist == 0)
                                {
                                    diff = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                                    dist = diff.magnitude;
                                }

                                float pushForce = (minDistance - dist) / 2f;
                                Vector2 pushVector = (diff / dist) * pushForce;

                                targetPositions[idA] += pushVector;
                                targetPositions[idB] -= pushVector;
                            }
                        }
                    }
                }
            }

            // Límites de la pantalla para que no se salgan al ser empujados
            float limitX = (_canvasWidth / 2f) - (dynamicNodeSize / 2f);
            float limitY = (_canvasHeight / 2f) - (dynamicNodeSize / 2f);

            // ==========================================
            // DIBUJAR NODOS
            // ==========================================
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

                    uiElement.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    uiElement.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    uiElement.rectTransform.pivot = new Vector2(0.5f, 0.5f);

                    uiElement.rectTransform.anchoredPosition3D = Vector3.zero;
                    uiElement.rectTransform.localScale = Vector3.one;
                    uiElement.rectTransform.localRotation = Quaternion.identity;

                    _activeUINodes.Add(node.id, uiElement);
                }

                uiElement.Setup(node.id, node.color, node.textContent, node.originDocumentId);
                uiElement.rectTransform.sizeDelta = new Vector2(dynamicNodeSize, dynamicNodeSize);

                // Obtener la posición calculada y relajarla dentro de los bordes
                Vector2 finalPos = targetPositions[node.id];
                finalPos.x = Mathf.Clamp(finalPos.x, -limitX, limitX);
                finalPos.y = Mathf.Clamp(finalPos.y, -limitY, limitY);

                uiElement.rectTransform.anchoredPosition = Vector2.Lerp(uiElement.rectTransform.anchoredPosition, finalPos, Time.deltaTime * 10f);
            }

            // ==========================================
            // DIBUJAR LÍNEAS DE CONEXIÓN
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

                // Las líneas usan las posiciones finales reales de la UI
                Vector2 startPos = _activeUINodes[line.startNodeId].rectTransform.anchoredPosition;
                Vector2 endPos = _activeUINodes[line.endNodeId].rectTransform.anchoredPosition;

                Vector2 direction = endPos - startPos;
                float distance = direction.magnitude;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                uiLine.rectTransform.anchoredPosition = startPos;
                uiLine.rectTransform.sizeDelta = new Vector2(distance, dynamicLineThickness);
                uiLine.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }
}