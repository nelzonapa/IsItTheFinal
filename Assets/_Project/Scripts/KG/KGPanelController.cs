using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ImmersiveGraph.Data;

namespace ImmersiveGraph.Visual
{
    public class KGPanelController : MonoBehaviour
    {
        [Header("Referencias Visuales")]
        public RectTransform graphContainer;
        public GameObject nodePrefab;
        public GameObject linePrefab;

        [Header("Controles y Mensajes")]
        [Tooltip("Asigna aquí el botón de la UI para limpiar los filtros")]
        public Button clearFiltersButton;

        [Tooltip("Asigna un TextMeshPro para mostrar mensajes flotantes de advertencia")]
        public TextMeshProUGUI warningText;

        [Tooltip("Asigna un TextMeshPro para mostrar qué filtro está activo permanentemente")]
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

            // --- Referencia al texto del verbo ---
            public RectTransform labelRect;
        }

        private Dictionary<string, UINode> _nodes = new Dictionary<string, UINode>();
        private List<UIEdge> _edges = new List<UIEdge>();

        private float _currentScale = 5.0f;
        private float _currentRepulsion;
        private float _currentSpringLength;
        private Coroutine _warningCoroutine;

        private string _currentActiveFilter = "";

        void Awake()
        {
            if (graphContainer == null) graphContainer = GetComponent<RectTransform>();

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

            if (H3GraphSpawner.Instance != null) H3GraphSpawner.Instance.ClearAllHighlights();
            if (clearFiltersButton != null) clearFiltersButton.gameObject.SetActive(false);
            if (activeFilterText != null) activeFilterText.gameObject.SetActive(false);
        }

        public void ClearGraph()
        {
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
                    if (!string.IsNullOrEmpty(ent))
                    {
                        uniqueEntities.Add(ent);
                    }
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

                int fileCount = H3GraphSpawner.Instance != null ? H3GraphSpawner.Instance.GetFileCountForEntity(entityName) : 0;
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

                        // ==========================================
                        // --- CREACIÓN DINÁMICA DEL TEXTO (VERBO) ---
                        // ==========================================
                        GameObject labelObj = new GameObject("Verb_" + edgeData.relacion);
                        labelObj.transform.SetParent(graphContainer, false);

                        labelObj.transform.SetSiblingIndex(lineObj.transform.GetSiblingIndex() + 1);

                        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
                        labelText.text = edgeData.relacion;

                        // --- CORRECCIÓN: ESCALADO DE FUENTE ---
                        // Ajustamos el tamaño base de la letra multiplicándolo por la escala actual
                        labelText.fontSize = 18f * _currentScale;

                        labelText.color = new Color(1f, 0.9f, 0.5f, 1f);
                        labelText.alignment = TextAlignmentOptions.Center;
                        //labelText.enableWordWrapping = false;

                        RectTransform labelRect = labelObj.GetComponent<RectTransform>();

                        // --- CORRECCIÓN: ESCALADO DEL CONTENEDOR DE TEXTO ---
                        labelRect.sizeDelta = new Vector2(200f * _currentScale, 30f * _currentScale);
                        labelRect.pivot = new Vector2(0.5f, 0.5f);

                        // Nos aseguramos que inicie horizontal y con la escala general
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

            CalculateLayoutInstantly();
            UpdateVisuals();
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
                if (isGlobalEntity)
                {
                    bgImage.color = new Color(0.2f, 0.8f, 0.8f, 1f);
                }
                else
                {
                    bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                }
            }

            Button btn = nodeObj.GetComponent<Button>();
            if (btn == null) btn = nodeObj.AddComponent<Button>();

            btn.onClick.AddListener(() =>
            {
                if (isGlobalEntity)
                {
                    _currentActiveFilter = entityName;

                    if (H3GraphSpawner.Instance != null) H3GraphSpawner.Instance.HighlightNodesByEntity(entityName);

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
            });

            _nodes.Add(entityName, new UINode { id = entityName, rect = rect, position = startPosition, velocity = Vector2.zero });
        }

        private void ShowWarningMessage(string msg)
        {
            if (warningText != null)
            {
                if (_warningCoroutine != null) StopCoroutine(_warningCoroutine);
                _warningCoroutine = StartCoroutine(AnimateWarning(msg));
            }
            else
            {
                Debug.LogWarning("[KG Info] " + msg);
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

        private void CalculateLayoutInstantly()
        {
            float safePadding = 50f * _currentScale;
            float widthLimit = (graphContainer.rect.width / 2f) - safePadding;
            float heightLimit = (graphContainer.rect.height / 2f) - safePadding;
            float maxSpeed = 40f * _currentScale;
            float minSafeDistance = 20f * _currentScale;

            for (int step = 0; step < 200; step++)
            {
                List<UINode> nodeList = new List<UINode>(_nodes.Values);
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
            }
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

                // Actualizar la línea
                edge.lineRect.anchoredPosition = startPos;
                edge.lineRect.sizeDelta = new Vector2(dist, baseNodeThickness * _currentScale);
                edge.lineRect.localRotation = Quaternion.Euler(0, 0, angle);

                // ==========================================
                // --- POSICIONAR EL VERBO (HORIZONTAL SIEMPRE) ---
                // ==========================================
                if (edge.labelRect != null)
                {
                    // Lo ubicamos exactamente en el punto medio de la línea
                    Vector2 midPoint = startPos + (dir / 2f);
                    edge.labelRect.anchoredPosition = midPoint;

                    // --- CORRECCIÓN: FORZAR HORIZONTAL ---
                    // Mantenemos la rotación siempre en 0, 0, 0 para que no gire con la línea.
                    edge.labelRect.localRotation = Quaternion.identity;
                }
            }
        }
    }
}