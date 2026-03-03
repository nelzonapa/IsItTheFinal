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

        // BOTÓN DE LIMPIEZA
        [Header("Controles")]
        [Tooltip("Asigna aquí el botón de la UI para limpiar los filtros")]
        public Button clearFiltersButton;

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
        }

        private Dictionary<string, UINode> _nodes = new Dictionary<string, UINode>();
        private List<UIEdge> _edges = new List<UIEdge>();

        private float _currentScale = 1.0f;
        private float _currentRepulsion;
        private float _currentSpringLength;

        void Awake()
        {
            if (graphContainer == null) graphContainer = GetComponent<RectTransform>();

            // Vincular el botón de limpieza si existe
            if (clearFiltersButton != null)
            {
                clearFiltersButton.onClick.AddListener(ResetFilters);
            }
        }

        // FUNCIÓN PARA EL BOTÓN DE LIMPIEZA
        public void ResetFilters()
        {
            if (H3GraphSpawner.Instance != null)
            {
                H3GraphSpawner.Instance.ClearAllHighlights();
            }
        }

        public void ClearGraph()
        {
            foreach (Transform child in graphContainer)
            {
                Destroy(child.gameObject);
            }
            _nodes.Clear();
            _edges.Clear();
        }

        public void BuildGraph(KGEdge[] kgData)
        {
            ClearGraph();
            if (kgData == null || kgData.Length == 0) return;

            // 1. RECOLECTAR NOMBRES ÚNICOS
            HashSet<string> uniqueEntities = new HashSet<string>();
            foreach (var edge in kgData)
            {
                uniqueEntities.Add(edge.sujeto);
                uniqueEntities.Add(edge.objeto);
            }

            int totalNodes = uniqueEntities.Count;
            if (totalNodes == 0) return;

            // 2. CÁLCULO DE ESCALADO INTELIGENTE
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

            // 3. CREAR NODOS EN DISTRIBUCIÓN CIRCULAR (Posición Inicial)
            int i = 0;
            float angleStep = (Mathf.PI * 2f) / totalNodes;
            float spawnRadius = Mathf.Min(graphContainer.rect.width, graphContainer.rect.height) * 0.25f * _currentScale;

            foreach (string entityName in uniqueEntities)
            {
                float angle = i * angleStep;
                Vector2 startPos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;

                CreateNode(entityName, startPos);
                i++;
            }

            // 4. CREAR CONEXIONES (Líneas)
            foreach (var edgeData in kgData)
            {
                if (_nodes.TryGetValue(edgeData.sujeto, out UINode sourceNode) &&
                    _nodes.TryGetValue(edgeData.objeto, out UINode targetNode))
                {
                    GameObject lineObj = Instantiate(linePrefab, graphContainer);
                    lineObj.transform.SetAsFirstSibling(); // Siempre atrás de los nodos

                    RectTransform lineRect = lineObj.GetComponent<RectTransform>();

                    // --- CORRECCIÓN CRÍTICA DE PIVOTE ---
                    // Obligamos a la línea a crecer desde su extremo izquierdo, no desde el centro
                    lineRect.pivot = new Vector2(0f, 0.5f);

                    _edges.Add(new UIEdge
                    {
                        source = sourceNode,
                        target = targetNode,
                        relation = edgeData.relacion,
                        lineRect = lineRect
                    });
                }
            }

            // 5. EJECUTAR FÍSICA INSTANTÁNEA (Sin Coroutine, pre-cálculo)
            CalculateLayoutInstantly();

            // 6. DIBUJAR RESULTADO FINAL
            UpdateVisuals();
        }

        private void CreateNode(string entityName, Vector2 startPosition)
        {
            GameObject nodeObj = Instantiate(nodePrefab, graphContainer);
            RectTransform rect = nodeObj.GetComponent<RectTransform>();

            rect.localScale = new Vector3(_currentScale, _currentScale, 1f);
            rect.anchoredPosition = startPosition; // Posición temporal

            TextMeshProUGUI textComp = nodeObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null) textComp.text = entityName;


            // FASE 3: INYECTAR BOTÓN NATIVO C#
            Button btn = nodeObj.GetComponent<Button>();
            if (btn == null) btn = nodeObj.AddComponent<Button>();

            // Le decimos al botón que al hacer clic, busque esta entidad en el Grafo 3D
            btn.onClick.AddListener(() =>
            {
                if (H3GraphSpawner.Instance != null)
                {
                    H3GraphSpawner.Instance.HighlightNodesByEntity(entityName);
                }
            });

            _nodes.Add(entityName, new UINode
            {
                id = entityName,
                rect = rect,
                position = startPosition,
                velocity = Vector2.zero
            });
        }

        private void CalculateLayoutInstantly()
        {
            float safePadding = 50f * _currentScale;
            float widthLimit = (graphContainer.rect.width / 2f) - safePadding;
            float heightLimit = (graphContainer.rect.height / 2f) - safePadding;
            float maxSpeed = 40f * _currentScale;
            float minSafeDistance = 20f * _currentScale;

            // Incrementamos a 200 pasos para garantizar que queden perfectamente acomodados.
            // Al no haber "yield return", esto toma apenas 1 milisegundo de procesador.
            for (int step = 0; step < 200; step++)
            {
                List<UINode> nodeList = new List<UINode>(_nodes.Values);

                // A. Repulsión
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

                // B. Atracción
                foreach (var edge in _edges)
                {
                    Vector2 diff = edge.target.position - edge.source.position;
                    float dist = diff.magnitude;

                    if (dist == 0) diff = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));

                    float displacement = dist - _currentSpringLength;
                    // Usamos un delta time fijo de 0.016 (60fps) porque esto ya no corre en tiempo real
                    Vector2 attraction = diff.normalized * (displacement * springForce * 0.016f);

                    edge.source.velocity += attraction;
                    edge.target.velocity -= attraction;
                }

                // C. Aplicar
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
            foreach (var node in _nodes.Values)
            {
                node.rect.anchoredPosition = node.position;
            }

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
            }
        }
    }
}