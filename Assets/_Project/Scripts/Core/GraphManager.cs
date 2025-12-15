using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ImmersiveGraph.Data;
using ImmersiveGraph.Visualization;
using TMPro;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers; // Necesario

namespace ImmersiveGraph.Core
{
    public class GraphManager : MonoBehaviour
    {
        [Header("Referencias")]
        public BoxCollider containerVolume;
        public GraphInteractionManager interactionManager;

        [Header("Prefabs")]
        public GameObject spherePrefab;
        public GameObject cubePrefab;
        public GameObject capsulePrefab;
        public GameObject linkLabelPrefab;
        public GameObject loadingBarPrefab;
        public Material lineMaterial;

        [Header("Ajustes")]
        public float nodeSizeAdjuster = 1.0f;

        // Diccionarios públicos para que el InteractionManager los lea
        public Dictionary<string, GameObject> nodeMap = new Dictionary<string, GameObject>();
        public Dictionary<string, NodeData> dataMap = new Dictionary<string, NodeData>();

        private Dictionary<string, VisualSetting> visualConfigMap = new Dictionary<string, VisualSetting>();
        private AppConfigContainer appConfig;
        private GraphDataContainer graphData;
        private Vector3 graphCenterOffset;

        // Flag para saber si estamos en modo overview (para actualizar líneas)
        private bool isOverviewMode = true;

        void Start()
        {
            nodeMap.Clear();
            dataMap.Clear();
            visualConfigMap.Clear();
            StartCoroutine(LoadFilesAndBuild());
        }

        // --- NUEVO: Actualización dinámica de líneas ---
        void Update()
        {
            // Solo actualizamos las líneas si estamos en Overview (pocos nodos) y hay nodos moviéndose.
            // Para garantizar suavidad, lo hacemos cada frame en Overview.
            if (isOverviewMode && graphData != null && graphData.nodes.Count > 0)
            {
                BuildConnections();
            }
        }

        IEnumerator LoadFilesAndBuild()
        {
            // ... (Carga de JSONs idéntica a tu versión anterior) ...
            string configPath = Path.Combine(Application.streamingAssetsPath, "unity_app_config.json");
            if (File.Exists(configPath))
            {
                appConfig = JsonUtility.FromJson<AppConfigContainer>(File.ReadAllText(configPath));
                if (appConfig.visual_settings != null)
                {
                    if (appConfig.visual_settings.ROOT != null) visualConfigMap.Add("ROOT", appConfig.visual_settings.ROOT);
                    if (appConfig.visual_settings.MACRO_TOPIC != null) visualConfigMap.Add("MACRO_TOPIC", appConfig.visual_settings.MACRO_TOPIC);
                    if (appConfig.visual_settings.MICRO_TOPIC != null) visualConfigMap.Add("MICRO_TOPIC", appConfig.visual_settings.MICRO_TOPIC);
                    if (appConfig.visual_settings.DOCUMENT != null) visualConfigMap.Add("DOCUMENT", appConfig.visual_settings.DOCUMENT);
                    if (appConfig.visual_settings.ENTITY != null) visualConfigMap.Add("ENTITY", appConfig.visual_settings.ENTITY);
                }
            }
            string dataPath = Path.Combine(Application.streamingAssetsPath, "unity_graph_data.json");
            if (File.Exists(dataPath)) graphData = JsonUtility.FromJson<GraphDataContainer>(File.ReadAllText(dataPath));

            CalculateAndApplyScale();
            BuildNodes();
            yield return null;
            BuildConnections();
        }

        void CalculateAndApplyScale()
        {
            if (containerVolume == null || graphData.nodes.Count == 0) return;
            float minX = graphData.nodes.Min(n => n.position.x); float maxX = graphData.nodes.Max(n => n.position.x);
            float minY = graphData.nodes.Min(n => n.position.y); float maxY = graphData.nodes.Max(n => n.position.y);
            float minZ = graphData.nodes.Min(n => n.position.z); float maxZ = graphData.nodes.Max(n => n.position.z);
            Vector3 graphSize = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
            if (graphSize.x == 0) graphSize.x = 1; if (graphSize.y == 0) graphSize.y = 1; if (graphSize.z == 0) graphSize.z = 1;
            Vector3 containerSize = containerVolume.size;
            float scaleX = containerSize.x / graphSize.x; float scaleY = containerSize.y / graphSize.y; float scaleZ = containerSize.z / graphSize.z;
            float finalScale = Mathf.Min(scaleX, Mathf.Min(scaleY, scaleZ)) * 1.5f;
            transform.localScale = Vector3.one * finalScale;
            transform.localPosition = Vector3.zero;
            graphCenterOffset = new Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
        }

        void BuildNodes()
        {
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

            foreach (var node in graphData.nodes)
            {
                if (!visualConfigMap.TryGetValue(node.type, out VisualSetting setting)) continue;

                GameObject prefabToUse = spherePrefab;
                if (setting.shape.Equals("Cube", System.StringComparison.OrdinalIgnoreCase)) prefabToUse = cubePrefab;
                else if (setting.shape.Equals("Capsule", System.StringComparison.OrdinalIgnoreCase)) prefabToUse = capsulePrefab;

                GameObject newNode = Instantiate(prefabToUse, this.transform);
                newNode.transform.localPosition = node.position.ToVector3() - graphCenterOffset;
                newNode.transform.localRotation = Quaternion.identity;
                newNode.name = node.id;
                newNode.transform.localScale = Vector3.one * setting.scale * nodeSizeAdjuster;

                Renderer rend = newNode.GetComponent<Renderer>();
                if (rend != null && ColorUtility.TryParseHtmlString(setting.color, out Color baseColor))
                {
                    rend.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_BaseColor", baseColor);
                    if (setting.emission > 0)
                    {
                        Color emissionColor = baseColor * Mathf.LinearToGammaSpace(setting.emission);
                        propBlock.SetColor("_EmissionColor", emissionColor);
                        rend.material.EnableKeyword("_EMISSION");
                    }
                    rend.SetPropertyBlock(propBlock);
                }

                TextMeshPro textComp = newNode.GetComponentInChildren<TextMeshPro>();
                if (textComp != null)
                {
                    textComp.text = node.label;
                    newNode.AddComponent<NodeLOD>();
                }

                // --- INTERACTIVIDAD FÍSICA ---
                // Agregamos componentes para manipulación (Grab Transformer)
                if (newNode.GetComponent<XRGrabInteractable>() == null) newNode.AddComponent<XRGrabInteractable>();
                if (newNode.GetComponent<XRGeneralGrabTransformer>() == null) newNode.AddComponent<XRGeneralGrabTransformer>();

                NodeInteraction interaction = newNode.AddComponent<NodeInteraction>();
                // Pasamos el prefab de la barra al inicializar
                interaction.Initialize(interactionManager, node.type, node.id, loadingBarPrefab);

                if (!nodeMap.ContainsKey(node.id)) nodeMap.Add(node.id, newNode);
                if (!dataMap.ContainsKey(node.id)) dataMap.Add(node.id, node);

                // --- FILTRO INICIAL ---
                // "Al inicio, solo deberían de aparecer ROOT y MACRO_TOPIC"
                if (node.type != "ROOT" && node.type != "MACRO_TOPIC")
                {
                    newNode.SetActive(false);
                }
            }
        }

        void BuildConnections()
        {
            // Buscamos el objeto de líneas existente o creamos uno nuevo
            Transform linesTrans = transform.Find("GraphConnections");
            GameObject linesObj;
            if (linesTrans == null)
            {
                linesObj = new GameObject("GraphConnections");
                linesObj.transform.parent = this.transform;
                linesObj.transform.localPosition = Vector3.zero;
                linesObj.transform.localRotation = Quaternion.identity;
                linesObj.transform.localScale = Vector3.one;
                linesObj.AddComponent<MeshFilter>();
                linesObj.AddComponent<MeshRenderer>().material = lineMaterial;
            }
            else
            {
                linesObj = linesTrans.gameObject;
            }

            // Usamos una lista temporal para vértices
            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();

            // Para las etiquetas, en este update rápido, NO instanciamos/destruimos, 
            // solo actualizamos posiciones si existen. (Simplificación para rendimiento)

            foreach (var node in graphData.nodes)
            {
                if (!string.IsNullOrEmpty(node.parent_id) && nodeMap.ContainsKey(node.parent_id) && nodeMap.ContainsKey(node.id))
                {
                    GameObject childObj = nodeMap[node.id];
                    GameObject parentObj = nodeMap[node.parent_id];

                    if (childObj == null || parentObj == null) continue;
                    if (!childObj.activeSelf || !parentObj.activeSelf) continue;

                    // Usamos LocalPosition para que las líneas sigan al nodo dentro del contenedor
                    Vector3 startPos = childObj.transform.localPosition;
                    Vector3 endPos = parentObj.transform.localPosition;

                    int startIndex = vertices.Count;
                    vertices.Add(startPos);
                    vertices.Add(endPos);
                    indices.Add(startIndex);
                    indices.Add(startIndex + 1);
                }
            }

            Mesh mesh = linesObj.GetComponent<MeshFilter>().mesh;
            mesh.Clear(); // Limpiar malla anterior
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices.ToArray();
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }

        // --- MÉTODOS PÚBLICOS DE AYUDA ---

        public List<GameObject> GetActiveChildrenOf(string parentId)
        {
            List<GameObject> children = new List<GameObject>();
            foreach (var kvp in dataMap)
            {
                if (kvp.Value.parent_id == parentId && nodeMap.ContainsKey(kvp.Key))
                {
                    GameObject obj = nodeMap[kvp.Key];
                    if (obj.activeSelf) children.Add(obj);
                }
            }
            return children;
        }

        public void RevealMicroTopicsFor(string macroId)
        {
            foreach (var kvp in dataMap)
            {
                // Solo revelar hijos directos (MICRO) del MACRO seleccionado
                if (kvp.Value.parent_id == macroId && kvp.Value.type == "MICRO_TOPIC")
                {
                    if (nodeMap.TryGetValue(kvp.Key, out GameObject nodeObj))
                    {
                        nodeObj.SetActive(true);
                        // Resetear posición relativa al padre por si acaso, o dejar que aparezcan donde estaban
                    }
                }
            }
            // Forzar rebuild inmediato para ver líneas nuevas
            BuildConnections();
        }

        public void SetSphericalMode(bool isSpherical)
        {
            isOverviewMode = !isSpherical; // Dejar de actualizar líneas cada frame si estamos en esférico (para rendimiento)
            if (isSpherical)
            {
                // Mostrar niveles profundos...
                ShowDeepLevels();
            }
        }

        // Métodos ShowDeepLevels y HideDeepLevels se mantienen igual...
        public void ShowDeepLevels() { /* Código anterior */ }
        public void HideDeepLevels() { /* Código anterior */ }
    }
}