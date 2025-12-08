using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ImmersiveGraph.Data;
using ImmersiveGraph.Visualization;
using TMPro;
using System.Linq;

namespace ImmersiveGraph.Core
{
    public class GraphManager : MonoBehaviour
    {
        [Header("Referencias de Escenario")]
        public BoxCollider containerVolume;

        [Header("Prefabs de Nodos")]
        public GameObject spherePrefab;
        public GameObject cubePrefab;
        public GameObject capsulePrefab;

        [Header("Prefab de Etiquetas de Enlace")]
        public GameObject linkLabelPrefab;

        [Header("Configuración Visual")]
        public Material lineMaterial;

        [Header("Ajustes de Escala")]
        [Tooltip("Multiplicador manual para el tamaño de las bolitas (nodos). Úsalo si se ven muy chicas o grandes.")]
        public float nodeSizeAdjuster = 1.0f;

        // Diccionarios
        private Dictionary<string, GameObject> nodeMap = new Dictionary<string, GameObject>();
        private Dictionary<string, VisualSetting> visualConfigMap = new Dictionary<string, VisualSetting>();

        private AppConfigContainer appConfig;
        private GraphDataContainer graphData;
        private Vector3 graphCenterOffset;

        void Start()
        {
            nodeMap.Clear();
            visualConfigMap.Clear();
            StartCoroutine(LoadFilesAndBuild());
        }

        IEnumerator LoadFilesAndBuild()
        {
            // 1. CARGAR CONFIG
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
            else { Debug.LogError("Falta unity_app_config.json"); yield break; }

            // 2. CARGAR DATOS
            string dataPath = Path.Combine(Application.streamingAssetsPath, "unity_graph_data.json");
            if (File.Exists(dataPath))
            {
                graphData = JsonUtility.FromJson<GraphDataContainer>(File.ReadAllText(dataPath));
            }
            else { Debug.LogError("Falta unity_graph_data.json"); yield break; }

            // CALCULAR LA ESCALA GLOBAL
            CalculateAndApplyScale();

            // 3. CONSTRUIR
            BuildNodes();
            yield return null;
            BuildConnections();
        }

        void CalculateAndApplyScale()
        {
            if (containerVolume == null || graphData.nodes.Count == 0) return;

            // Encontrar límites
            float minX = graphData.nodes.Min(n => n.position.x);
            float maxX = graphData.nodes.Max(n => n.position.x);
            float minY = graphData.nodes.Min(n => n.position.y);
            float maxY = graphData.nodes.Max(n => n.position.y);
            float minZ = graphData.nodes.Min(n => n.position.z);
            float maxZ = graphData.nodes.Max(n => n.position.z);

            Vector3 graphSize = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
            if (graphSize.x == 0) graphSize.x = 1; if (graphSize.y == 0) graphSize.y = 1; if (graphSize.z == 0) graphSize.z = 1;

            Vector3 containerSize = containerVolume.size;

            // Escalar para que quepa en la caja
            float scaleX = containerSize.x / graphSize.x;
            float scaleY = containerSize.y / graphSize.y;
            float scaleZ = containerSize.z / graphSize.z;

            // Factor 0.8 para dejar margen
            float finalScale = Mathf.Min(scaleX, Mathf.Min(scaleY, scaleZ)) * 0.8f;

            transform.localScale = Vector3.one * finalScale;
            transform.localPosition = Vector3.zero;

            // Calcular offset para centrar
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

                // Posición corregida con Offset
                newNode.transform.localPosition = node.position.ToVector3() - graphCenterOffset;
                newNode.transform.localRotation = Quaternion.identity;
                newNode.name = node.id;

                // --- CORRECCIÓN DE TAMAÑO ---
                // Ya no dividimos por la escala del padre.
                // Multiplicamos por el ajustador manual para control total.
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

                // Texto
                TextMeshPro textComp = newNode.GetComponentInChildren<TextMeshPro>();
                if (textComp != null)
                {
                    textComp.text = node.label;
                    newNode.AddComponent<NodeLOD>();
                }

                if (!nodeMap.ContainsKey(node.id)) nodeMap.Add(node.id, newNode);

                // Filtro Vista General
                if (node.type != "ROOT" && node.type != "MACRO_TOPIC") newNode.SetActive(false);
            }
        }

        void BuildConnections()
        {
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

                        // Escala ajustada también para los textos
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
    }
}