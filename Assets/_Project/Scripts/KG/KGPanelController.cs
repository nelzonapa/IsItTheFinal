using ImmersiveGraph.Core;
using ImmersiveGraph.Data;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ImmersiveGraph.Visual
{
    public class KGPanelController : MonoBehaviour
    {
        [Header("Referencia Local (¡Importante!)")]
        [Tooltip("Referencia al grafo de ESTE escritorio. Evita usar el Instance global.")]
        public H3GraphSpawner localGraphSpawner;

        [Header("Referencias Visuales")]
        public RectTransform graphContainer;
        public GameObject nodePrefab;
        public GameObject linePrefab;

        [Header("Generador de Tokens 3D")]
        [Tooltip("Asigna aquí tu prefab del Token 3D que se instanciará al agarrar el nodo")]
        public GameObject token3DPrefab;

        [Header("Controles y Mensajes")]
        public Button clearFiltersButton;
        public TextMeshProUGUI warningText;
        public TextMeshProUGUI activeFilterText;

        [Header("Física Base (Para tamaño 1.0)")]
        public float baseRepulsionForce = 2500f;
        public float baseSpringLength = 150f;
        public float springForce = 4f;
        public float damping = 0.85f;
        public float baseNodeThickness = 3f;

        [Header("Configuración de Escalado Automático")]
        public int nodeThresholdForScaling = 8;
        public float minScaleLimit = 0.3f;

        private class UINode
        {
            public string id;
            public RectTransform rect;
            public Vector2 position;
            public Vector2 velocity;
        }

        private class UIEdge
        {
            public UINode source;
            public UINode target;
            public string relation;
            public RectTransform lineRect;
            public RectTransform labelRect;
        }

        private Dictionary<string, UINode> _nodes = new Dictionary<string, UINode>();
        private List<UIEdge> _edges = new List<UIEdge>();

        private float _currentScale = 5.0f;
        private float _currentRepulsion;
        private float _currentSpringLength;
        private Coroutine _warningCoroutine;
        private Coroutine _layoutCoroutine;

        private string _currentActiveFilter = "";

        void Awake()
        {
            if (graphContainer == null) graphContainer = GetComponent<RectTransform>();

            // Auto-búsqueda del Spawner local en caso de que no se haya asignado en el Inspector
            if (localGraphSpawner == null) localGraphSpawner = GetComponentInParent<H3GraphSpawner>();

            if (clearFiltersButton != null)
            {
                clearFiltersButton.onClick.AddListener(ResetFilters);
                clearFiltersButton.gameObject.SetActive(false);
            }

            if (warningText != null) warningText.gameObject.SetActive(false);
            if (activeFilterText != null) activeFilterText.gameObject.SetActive(false);
        }

        public void ResetFilters()
        {
            _currentActiveFilter = "";

            if (localGraphSpawner != null) localGraphSpawner.ClearAllHighlights();

            if (clearFiltersButton != null) clearFiltersButton.gameObject.SetActive(false);
            if (activeFilterText != null) activeFilterText.gameObject.SetActive(false);
        }

        public void ClearGraph()
        {
            if (_layoutCoroutine != null) StopCoroutine(_layoutCoroutine);

            foreach (Transform child in graphContainer)
            {
                if (clearFiltersButton != null && child == clearFiltersButton.transform) continue;
                if (warningText != null && child == warningText.transform) continue;
                if (activeFilterText != null && child == activeFilterText.transform) continue;

                Destroy(child.gameObject);
            }
            _nodes.Clear();
            _edges.Clear();

            if (string.IsNullOrEmpty(_currentActiveFilter))
            {
                if (clearFiltersButton != null) clearFiltersButton.gameObject.SetActive(false);
                if (activeFilterText != null) activeFilterText.gameObject.SetActive(false);
            }

            if (warningText != null) warningText.gameObject.SetActive(false);
        }

        public void BuildGraph(KGEdge[] kgData, string[] extraEntities)
        {
            ClearGraph();

            HashSet<string> uniqueEntities = new HashSet<string>();

            if (kgData != null)
            {
                foreach (var edge in kgData)
                {
                    uniqueEntities.Add(edge.sujeto);
                    uniqueEntities.Add(edge.objeto);
                }
            }

            if (extraEntities != null)
            {
                foreach (string ent in extraEntities)
                {
                    if (!string.IsNullOrEmpty(ent)) uniqueEntities.Add(ent);
                }
            }

            int totalNodes = uniqueEntities.Count;
            if (totalNodes == 0) return;

            if (totalNodes > nodeThresholdForScaling)
            {
                _currentScale = (float)nodeThresholdForScaling / (float)totalNodes;
                _currentScale = Mathf.Clamp(_currentScale, minScaleLimit, 1.0f);
            }
            else
            {
                _currentScale = 1.0f;
            }

            _currentRepulsion = baseRepulsionForce * _currentScale;
            _currentSpringLength = baseSpringLength * _currentScale;

            int i = 0;
            float angleStep = (Mathf.PI * 2f) / totalNodes;
            float spawnRadius = Mathf.Min(graphContainer.rect.width, graphContainer.rect.height) * 0.25f * _currentScale;

            foreach (string entityName in uniqueEntities)
            {
                float angle = i * angleStep;
                Vector2 startPos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;

                int fileCount = localGraphSpawner != null ? localGraphSpawner.GetFileCountForEntity(entityName) : 0;
                CreateNode(entityName, startPos, fileCount);
                i++;
            }

            if (kgData != null)
            {
                foreach (var edgeData in kgData)
                {
                    if (_nodes.TryGetValue(edgeData.sujeto, out UINode sourceNode) &&
                        _nodes.TryGetValue(edgeData.objeto, out UINode targetNode))
                    {
                        GameObject lineObj = Instantiate(linePrefab, graphContainer);
                        lineObj.transform.SetAsFirstSibling();
                        RectTransform lineRect = lineObj.GetComponent<RectTransform>();
                        lineRect.pivot = new Vector2(0f, 0.5f);

                        GameObject labelObj = new GameObject("Verb_" + edgeData.relacion);
                        labelObj.transform.SetParent(graphContainer, false);
                        labelObj.transform.SetSiblingIndex(lineObj.transform.GetSiblingIndex() + 1);

                        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
                        labelText.text = edgeData.relacion;
                        labelText.fontSize = 18f * _currentScale;
                        labelText.color = new Color(1f, 0.9f, 0.5f, 1f);
                        labelText.alignment = TextAlignmentOptions.Center;

                        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
                        labelRect.sizeDelta = new Vector2(200f * _currentScale, 30f * _currentScale);
                        labelRect.pivot = new Vector2(0.5f, 0.5f);
                        labelRect.localRotation = Quaternion.identity;
                        labelRect.localScale = Vector3.one;

                        _edges.Add(new UIEdge
                        {
                            source = sourceNode,
                            target = targetNode,
                            relation = edgeData.relacion,
                            lineRect = lineRect,
                            labelRect = labelRect
                        });
                    }
                }
            }

            // Distribuimos el costo físico en el tiempo
            _layoutCoroutine = StartCoroutine(AnimateLayoutRoutine());
        }

        private void CreateNode(string entityName, Vector2 startPosition, int fileCount)
        {
            GameObject nodeObj = Instantiate(nodePrefab, graphContainer);
            RectTransform rect = nodeObj.GetComponent<RectTransform>();

            rect.localScale = new Vector3(_currentScale, _currentScale, 1f);
            rect.anchoredPosition = startPosition;

            TextMeshProUGUI textComp = nodeObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null) textComp.text = entityName;

            bool isGlobalEntity = (fileCount >= 2);
            Image bgImage = nodeObj.GetComponent<Image>();

            if (bgImage != null)
            {
                if (isGlobalEntity) bgImage.color = new Color(0.2f, 0.8f, 0.8f, 1f);
                else bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            }

            // --- ACCIÓN: EXTRAER TOKEN FÍSICO ---
            System.Action spawnTokenAction = () =>
            {
                if (token3DPrefab != null)
                {
                    Vector3 spawnPos = nodeObj.transform.position - (nodeObj.transform.forward * 0.15f);
                    GameObject newToken = Instantiate(token3DPrefab, spawnPos, nodeObj.transform.rotation);
                    newToken.name = "Token3D_" + entityName;

                    var renderer = newToken.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = UserColorPalette.GetLocalPlayerColor();
                    }

                    TMP_Text[] textComponents = newToken.GetComponentsInChildren<TMP_Text>();
                    foreach (var txt in textComponents)
                    {
                        txt.text = entityName;
                    }
                }
                else
                {
                    Debug.LogWarning("[KGPanel] No se asignó el token3DPrefab en el inspector.");
                }
            };

            // --- ACCIÓN: FILTRAR ---
            System.Action onClickAction = () =>
            {
                if (isGlobalEntity)
                {
                    _currentActiveFilter = entityName;

                    if (localGraphSpawner != null) localGraphSpawner.HighlightNodesByEntity(entityName);

                    if (clearFiltersButton != null)
                    {
                        clearFiltersButton.gameObject.SetActive(true);
                        clearFiltersButton.transform.SetAsLastSibling();
                    }

                    if (activeFilterText != null)
                    {
                        activeFilterText.text = $"Filtro Activo: {entityName}";
                        activeFilterText.gameObject.SetActive(true);
                        activeFilterText.transform.SetAsLastSibling();
                    }
                }
                else
                {
                    ShowWarningMessage($"La entidad '{entityName}' solo existe en este archivo.");
                }
            };

            UIDraggableNode smartNodeHandler = nodeObj.AddComponent<UIDraggableNode>();
            float safePadding = 50f * _currentScale;
            float widthLimit = (graphContainer.rect.width / 2f) - safePadding;
            float heightLimit = (graphContainer.rect.height / 2f) - safePadding;

            System.Action<Vector2> onDragUpdate = (newPosition) =>
            {
                _nodes[entityName].position = newPosition;
                UpdateVisuals();
            };

            smartNodeHandler.Initialize(rect, graphContainer, widthLimit, heightLimit, onDragUpdate, onClickAction, spawnTokenAction);

            // =========================================================
            // AISLAMIENTO DE INTERACTABLES VR
            // =========================================================
            bool isVRActive = true;
            if (PlatformManager.Instance != null && PlatformManager.Instance.pcRig != null)
            {
                if (PlatformManager.Instance.ActiveRig == PlatformManager.Instance.pcRig)
                {
                    isVRActive = false;
                }
            }
            else
            {
                var xrSettings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
                isVRActive = xrSettings != null && xrSettings.Manager != null && xrSettings.Manager.isInitializationComplete;
            }

            if (isVRActive)
            {
                BoxCollider col3D = nodeObj.AddComponent<BoxCollider>();
                col3D.size = new Vector3(rect.rect.width, rect.rect.height, 10f);
                var xrInteractable = nodeObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
                xrInteractable.selectEntered.AddListener((args) =>
                {
                    spawnTokenAction();
                });
            }

            _nodes.Add(entityName, new UINode { id = entityName, rect = rect, position = startPosition, velocity = Vector2.zero });
        }

        private void ShowWarningMessage(string msg)
        {
            if (warningText != null)
            {
                if (_warningCoroutine != null) StopCoroutine(_warningCoroutine);
                _warningCoroutine = StartCoroutine(AnimateWarning(msg));
            }
        }

        private IEnumerator AnimateWarning(string msg)
        {
            warningText.text = msg;
            warningText.gameObject.SetActive(true);
            warningText.transform.SetAsLastSibling();

            warningText.color = new Color(warningText.color.r, warningText.color.g, warningText.color.b, 1f);
            yield return new WaitForSeconds(2.0f);

            float alpha = 1f;
            while (alpha > 0f)
            {
                alpha -= Time.deltaTime * 1.5f;
                warningText.color = new Color(warningText.color.r, warningText.color.g, warningText.color.b, alpha);
                yield return null;
            }

            warningText.gameObject.SetActive(false);
        }

        // =========================================================================
        // REEMPLAZO DE CalculateLayoutInstantly POR CORRUTINA PARA EVITAR CONGELAMIENTO
        // =========================================================================
        private IEnumerator AnimateLayoutRoutine()
        {
            float safePadding = 50f * _currentScale;
            float widthLimit = (graphContainer.rect.width / 2f) - safePadding;
            float heightLimit = (graphContainer.rect.height / 2f) - safePadding;
            float maxSpeed = 40f * _currentScale;
            float minSafeDistance = 20f * _currentScale;

            List<UINode> nodeList = new List<UINode>(_nodes.Values);

            for (int step = 0; step < 200; step++)
            {
                for (int i = 0; i < nodeList.Count; i++)
                {
                    for (int j = i + 1; j < nodeList.Count; j++)
                    {
                        UINode n1 = nodeList[i];
                        UINode n2 = nodeList[j];
                        Vector2 diff = n1.position - n2.position;
                        float dist = diff.magnitude;
                        if (dist < minSafeDistance) dist = minSafeDistance;
                        Vector2 repulsion = (diff.normalized * _currentRepulsion) / (dist * dist);
                        n1.velocity += repulsion;
                        n2.velocity -= repulsion;
                    }
                }
                foreach (var edge in _edges)
                {
                    Vector2 diff = edge.target.position - edge.source.position;
                    float dist = diff.magnitude;
                    if (dist == 0) diff = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                    float displacement = dist - _currentSpringLength;
                    Vector2 attraction = diff.normalized * (displacement * springForce * 0.016f);
                    edge.source.velocity += attraction;
                    edge.target.velocity -= attraction;
                }
                foreach (var node in nodeList)
                {
                    node.velocity = Vector2.ClampMagnitude(node.velocity, maxSpeed);
                    node.position += node.velocity * 0.016f * 60f;
                    node.velocity *= damping;
                    node.position.x = Mathf.Clamp(node.position.x, -widthLimit, widthLimit);
                    node.position.y = Mathf.Clamp(node.position.y, -heightLimit, heightLimit);
                }

                // Liberar el hilo principal de Unity para procesar gráficos, inputs, etc. cada 5 iteraciones
                if (step % 5 == 0)
                {
                    UpdateVisuals();
                    yield return null;
                }
            }

            UpdateVisuals();
            _layoutCoroutine = null;
        }

        private void UpdateVisuals()
        {
            foreach (var node in _nodes.Values) node.rect.anchoredPosition = node.position;
            foreach (var edge in _edges)
            {
                Vector2 startPos = edge.source.position;
                Vector2 endPos = edge.target.position;
                Vector2 dir = endPos - startPos;
                float dist = dir.magnitude;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                edge.lineRect.anchoredPosition = startPos;
                edge.lineRect.sizeDelta = new Vector2(dist, baseNodeThickness * _currentScale);
                edge.lineRect.localRotation = Quaternion.Euler(0, 0, angle);

                if (edge.labelRect != null)
                {
                    Vector2 midPoint = startPos + (dir / 2f);
                    edge.labelRect.anchoredPosition = midPoint;
                    edge.labelRect.localRotation = Quaternion.identity;
                }
            }
        }
    }

    // ==========================================
    // --- LÓGICA INTELIGENTE (ARRASTRE VS CLIC) ---
    // ==========================================
    public class UIDraggableNode : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerDownHandler, IPointerClickHandler
    {
        private RectTransform _nodeRect;
        private RectTransform _canvasRect;
        private System.Action<Vector2> _onDragUpdate;
        private System.Action _onClickAction;
        private System.Action _onRightClickAction;

        private float _limitX;
        private float _limitY;
        private bool _wasDragged = false;

        public void Initialize(RectTransform nodeRect, RectTransform canvasRect, float limitX, float limitY, System.Action<Vector2> onDragUpdate, System.Action onClickAction, System.Action onRightClickAction)
        {
            _nodeRect = nodeRect;
            _canvasRect = canvasRect;
            _limitX = limitX;
            _limitY = limitY;
            _onDragUpdate = onDragUpdate;
            _onClickAction = onClickAction;
            _onRightClickAction = onRightClickAction;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) return;
            _wasDragged = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) return;
            _wasDragged = true;
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPointerPosition))
            {
                localPointerPosition.x = Mathf.Clamp(localPointerPosition.x, -_limitX, _limitX);
                localPointerPosition.y = Mathf.Clamp(localPointerPosition.y, -_limitY, _limitY);

                _nodeRect.anchoredPosition = localPointerPosition;
                _onDragUpdate?.Invoke(localPointerPosition);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                _onRightClickAction?.Invoke();
            }
            else
            {
                if (!_wasDragged)
                {
                    _onClickAction?.Invoke();
                }
            }
        }
    }
}