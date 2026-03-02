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

        [Header("Configuración Física (Fuerzas)")]
        public float repulsionForce = 2000f;
        public float springLength = 100f;
        public float springForce = 5f;
        public float damping = 0.85f;
        public float nodeThickness = 3f;

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

        private Coroutine _physicsCoroutine;

        void Awake()
        {
            if (graphContainer == null) graphContainer = GetComponent<RectTransform>();
        }

        public void ClearGraph()
        {
            if (_physicsCoroutine != null) StopCoroutine(_physicsCoroutine);

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

            Debug.Log($"[KG Panel UI] Recibida orden de construcción. Procesando {kgData.Length} tripletas...");

            // 1. Crear Nodos Únicos
            foreach (var edge in kgData)
            {
                CreateNodeIfNotExists(edge.sujeto);
                CreateNodeIfNotExists(edge.objeto);
            }

            Debug.Log($"[KG Panel UI] Generación completada: Se crearon {_nodes.Count} nodos visuales únicos de las tripletas. Iniciando motor de físicas 2D...");

            // 2. Crear Conexiones (Líneas)
            foreach (var edgeData in kgData)
            {
                UINode sourceNode = _nodes[edgeData.sujeto];
                UINode targetNode = _nodes[edgeData.objeto];

                GameObject lineObj = Instantiate(linePrefab, graphContainer);
                lineObj.transform.SetAsFirstSibling();

                _edges.Add(new UIEdge
                {
                    source = sourceNode,
                    target = targetNode,
                    relation = edgeData.relacion,
                    lineRect = lineObj.GetComponent<RectTransform>()
                });
            }

            // 3. Iniciar Simulación Física (Animación suave)
            _physicsCoroutine = StartCoroutine(RunForceDirectedLayout());
        }

        private void CreateNodeIfNotExists(string entityName)
        {
            if (!_nodes.ContainsKey(entityName))
            {
                GameObject nodeObj = Instantiate(nodePrefab, graphContainer);
                RectTransform rect = nodeObj.GetComponent<RectTransform>();

                Vector2 randomStart = new Vector2(Random.Range(-10f, 10f), Random.Range(-10f, 10f));
                rect.anchoredPosition = randomStart;

                TextMeshProUGUI textComp = nodeObj.GetComponentInChildren<TextMeshProUGUI>();
                if (textComp != null) textComp.text = entityName;

                _nodes.Add(entityName, new UINode
                {
                    id = entityName,
                    rect = rect,
                    position = randomStart,
                    velocity = Vector2.zero
                });
            }
        }

        private IEnumerator RunForceDirectedLayout()
        {
            float widthLimit = graphContainer.rect.width / 2f - 60f;
            float heightLimit = graphContainer.rect.height / 2f - 30f;

            for (int step = 0; step < 100; step++)
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
                        if (dist == 0) dist = 0.1f;

                        Vector2 repulsion = (diff.normalized * repulsionForce) / (dist * dist);
                        n1.velocity += repulsion;
                        n2.velocity -= repulsion;
                    }
                }

                foreach (var edge in _edges)
                {
                    Vector2 diff = edge.target.position - edge.source.position;
                    float dist = diff.magnitude;

                    float displacement = dist - springLength;
                    Vector2 attraction = diff.normalized * (displacement * springForce * Time.deltaTime);

                    edge.source.velocity += attraction;
                    edge.target.velocity -= attraction;
                }

                foreach (var node in nodeList)
                {
                    node.velocity = Vector2.ClampMagnitude(node.velocity, 50f);

                    node.position += node.velocity * Time.deltaTime;
                    node.velocity *= damping;

                    node.position.x = Mathf.Clamp(node.position.x, -widthLimit, widthLimit);
                    node.position.y = Mathf.Clamp(node.position.y, -heightLimit, heightLimit);

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
                    edge.lineRect.sizeDelta = new Vector2(dist, nodeThickness);
                    edge.lineRect.localRotation = Quaternion.Euler(0, 0, angle);
                }

                yield return null;
            }
        }
    }
}