using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ImmersiveGraph.Data;
using ImmersiveGraph.Visualization;
using TMPro;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Necesario

namespace ImmersiveGraph.Core
{
    public class GraphManager : MonoBehaviour
    {
        [Header("Referencias")]
        public BoxCollider containerVolume;
        public GraphInteractionManager interactionManager; // <-- REFERENCIA NUEVA

        [Header("Prefabs")]
        public GameObject spherePrefab;
        public GameObject cubePrefab;
        public GameObject capsulePrefab;
        public GameObject linkLabelPrefab;
        public Material lineMaterial;

        [Header("Ajustes")]
        public float nodeSizeAdjuster = 1.0f;

        // Diccionarios
        private Dictionary<string, GameObject> nodeMap = new Dictionary<string, GameObject>();
        private Dictionary<string, NodeData> dataMap = new Dictionary<string, NodeData>(); // Para saber quién es hijo de quién
        private Dictionary<string, VisualSetting> visualConfigMap = new Dictionary<string, VisualSetting>();

        private AppConfigContainer appConfig;
        private GraphDataContainer graphData;
        private Vector3 graphCenterOffset;

        void Start()
        {
            nodeMap.Clear();
            dataMap.Clear();
            visualConfigMap.Clear();
            StartCoroutine(LoadFilesAndBuild());
        }

        IEnumerator LoadFilesAndBuild()
        {
            // (Carga de archivos igual que antes...)
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
            if (File.Exists(dataPath))
            {
                graphData = JsonUtility.FromJson<GraphDataContainer>(File.ReadAllText(dataPath));
            }

            CalculateAndApplyScale();
            BuildNodes();
            yield return null;
            BuildConnections();
        }

        void CalculateAndApplyScale()
        {
            if (containerVolume == null || graphData.nodes.Count == 0) return;

            float minX = graphData.nodes.Min(n => n.position.x);
            float maxX = graphData.nodes.Max(n => n.position.x);
            float minY = graphData.nodes.Min(n => n.position.y);
            float maxY = graphData.nodes.Max(n => n.position.y);
            float minZ = graphData.nodes.Min(n => n.position.z);
            float maxZ = graphData.nodes.Max(n => n.position.z);

            Vector3 graphSize = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
            if (graphSize.x == 0) graphSize.x = 1; if (graphSize.y == 0) graphSize.y = 1; if (graphSize.z == 0) graphSize.z = 1;

            Vector3 containerSize = containerVolume.size;
            float scaleX = containerSize.x / graphSize.x;
            float scaleY = containerSize.y / graphSize.y;
            float scaleZ = containerSize.z / graphSize.z;

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

                // Color
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

                // --- INTERACTIVIDAD ---
                // Agregamos XRSimpleInteractable si no lo tiene el prefab
                if (newNode.GetComponent<XRSimpleInteractable>() == null)
                    newNode.AddComponent<XRSimpleInteractable>();

                // Agregamos nuestro script de interacción
                NodeInteraction interaction = newNode.AddComponent<NodeInteraction>();
                interaction.Initialize(interactionManager, node.type, node.id);


                if (!nodeMap.ContainsKey(node.id)) nodeMap.Add(node.id, newNode);
                if (!dataMap.ContainsKey(node.id)) dataMap.Add(node.id, node);

                // --- ESTADO INICIAL: MESA (Solo Nivel 1 y 2 visibles si son padres) ---
                // Ocultamos DOCUMENT y ENTITY al inicio
                if (node.type == "DOCUMENT" || node.type == "ENTITY")
                {
                    newNode.SetActive(false);
                }
                // Ocultamos MICRO_TOPIC también inicialmente para que el usuario haga "Drill down"?
                // Tú dijiste "La zona 2 solo debería soportar hasta el nivel de tema MICRO_TOPIC".
                // Así que MICRO_TOPIC sí debe verse, pero quizás colapsado.
                // Por ahora mostremos ROOT, MACRO y MICRO en la caja.
            }
        }

        // ... (BuildConnections es igual, no hace falta pegarlo de nuevo si ya lo tienes bien) ...
        void BuildConnections()
        {
            // (Usa el mismo código corregido de la respuesta anterior)
            // Solo asegúrate de copiarlo aquí si vas a reemplazar todo el archivo.
            // ... [Código de BuildConnections anterior] ...

            // REPETIR CODIGO DE BUILDCONNECTIONS PARA QUE COMPILE DIRECTO:
            GameObject linesObj = new GameObject("GraphConnections");
            linesObj.transform.parent = this.transform;
            linesObj.transform.localPosition = Vector3.zero;
            linesObj.transform.localRotation = Quaternion.identity;
            linesObj.transform.localScale = Vector3.one;

            MeshFilter mf = linesObj.AddComponent<MeshFilter>();
            MeshRenderer mr = linesObj.AddComponent<MeshRenderer>();
            mr.material = lineMaterial;

            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();

            GameObject labelsContainer = new GameObject("RelationLabels");
            labelsContainer.transform.parent = this.transform;
            labelsContainer.transform.localPosition = Vector3.zero;
            labelsContainer.transform.localRotation = Quaternion.identity;
            labelsContainer.transform.localScale = Vector3.one;

            foreach (var node in graphData.nodes)
            {
                if (!string.IsNullOrEmpty(node.parent_id) && nodeMap.ContainsKey(node.parent_id) && nodeMap.ContainsKey(node.id))
                {
                    GameObject childObj = nodeMap[node.id];
                    GameObject parentObj = nodeMap[node.parent_id];

                    if (!childObj.activeSelf || !parentObj.activeSelf) continue;

                    Vector3 startPos = childObj.transform.localPosition;
                    Vector3 endPos = parentObj.transform.localPosition;

                    int startIndex = vertices.Count;
                    vertices.Add(startPos);
                    vertices.Add(endPos);
                    indices.Add(startIndex);
                    indices.Add(startIndex + 1);

                    if (!string.IsNullOrEmpty(node.relation_label) && linkLabelPrefab != null)
                    {
                        Vector3 midPoint = (startPos + endPos) / 2f;
                        GameObject labelObj = Instantiate(linkLabelPrefab, labelsContainer.transform);
                        labelObj.transform.localPosition = midPoint;
                        labelObj.transform.localScale = Vector3.one * nodeSizeAdjuster;

                        TextMeshPro tmp = labelObj.GetComponentInChildren<TextMeshPro>();
                        if (tmp != null) tmp.text = node.relation_label;

                        NodeLOD lod = labelObj.GetComponent<NodeLOD>();
                        if (lod != null) lod.cullingDistance = 10.0f;
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices.ToArray();
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            mf.mesh = mesh;
        }

        // --- MÉTODOS DE FILTRADO PARA INTERACCIÓN ---

        public void ShowChildrenOf(string parentId)
        {
            // Busca todos los nodos cuyo parent_id sea parentId y actívalos
            foreach (var kvp in dataMap)
            {
                if (kvp.Value.parent_id == parentId)
                {
                    if (nodeMap.TryGetValue(kvp.Key, out GameObject nodeObj))
                    {
                        nodeObj.SetActive(true);
                    }
                }
            }
            // Reconstruir líneas porque ahora hay nuevos nodos visibles
            // (Es costoso hacer esto en tiempo real, pero para fase 2 es aceptable)
            Destroy(transform.Find("GraphConnections")?.gameObject);
            Destroy(transform.Find("RelationLabels")?.gameObject);
            BuildConnections();
        }

        public void ShowDeepLevels()
        {
            // Activar DOCUMENT y ENTITY
            foreach (var kvp in dataMap)
            {
                if (kvp.Value.type == "DOCUMENT" || kvp.Value.type == "ENTITY")
                {
                    if (nodeMap.TryGetValue(kvp.Key, out GameObject nodeObj))
                    {
                        nodeObj.SetActive(true);
                    }
                }
            }
            // Reconstruir visualización completa
            Destroy(transform.Find("GraphConnections")?.gameObject);
            Destroy(transform.Find("RelationLabels")?.gameObject);
            BuildConnections();
        }

        public void HideDeepLevels()
        {
            foreach (var kvp in dataMap)
            {
                if (kvp.Value.type == "DOCUMENT" || kvp.Value.type == "ENTITY")
                {
                    if (nodeMap.TryGetValue(kvp.Key, out GameObject nodeObj))
                    {
                        nodeObj.SetActive(false);
                    }
                }
            }
            Destroy(transform.Find("GraphConnections")?.gameObject);
            Destroy(transform.Find("RelationLabels")?.gameObject);
            BuildConnections();
        }
    }
}