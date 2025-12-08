using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ImmersiveGraph.Data;
using ImmersiveGraph.Visualization;
using TMPro;

namespace ImmersiveGraph.Core
{
    public class GraphManager : MonoBehaviour
    {
        [Header("Prefabs de Nodos")]
        public GameObject spherePrefab;
        public GameObject cubePrefab;
        public GameObject capsulePrefab;

        [Header("Prefab de Etiquetas de Enlace")]
        public GameObject linkLabelPrefab;

        [Header("Configuración Visual")]
        public Material lineMaterial;

        // Diccionarios
        private Dictionary<string, GameObject> nodeMap = new Dictionary<string, GameObject>();
        private Dictionary<string, VisualSetting> visualConfigMap = new Dictionary<string, VisualSetting>();

        private AppConfigContainer appConfig;
        private GraphDataContainer graphData;

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
                string jsonContent = File.ReadAllText(configPath);
                appConfig = JsonUtility.FromJson<AppConfigContainer>(jsonContent);

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
                string dataContent = File.ReadAllText(dataPath);
                graphData = JsonUtility.FromJson<GraphDataContainer>(dataContent);
            }
            else { Debug.LogError("Falta unity_graph_data.json"); yield break; }

            // 3. CONSTRUIR
            BuildNodes();
            BuildConnections(); // He renombrado esto para que sea más claro

            yield return null;
        }

        void BuildNodes()
        {
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

            foreach (var node in graphData.nodes)
            {
                if (!visualConfigMap.TryGetValue(node.type, out VisualSetting setting)) continue;

                // --- LOGICA DE PREFAB ---
                GameObject prefabToUse = spherePrefab;
                if (setting.shape.Equals("Cube", System.StringComparison.OrdinalIgnoreCase)) prefabToUse = cubePrefab;
                else if (setting.shape.Equals("Capsule", System.StringComparison.OrdinalIgnoreCase)) prefabToUse = capsulePrefab;

                // Instanciar como hijo de este GraphManager (para que herede la escala pequeña)
                Vector3 pos = node.position.ToVector3();
                GameObject newNode = Instantiate(prefabToUse, pos, Quaternion.identity, this.transform);
                newNode.name = node.id;

                // Aplicar escala del nodo (relativa al padre)
                newNode.transform.localScale = Vector3.one * setting.scale;

                // --- COLOR Y VISUALES ---
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

                // Guardar en mapa
                if (!nodeMap.ContainsKey(node.id)) nodeMap.Add(node.id, newNode);

                // --- NUEVO: FILTRO DE VISTA GENERAL (OVERVIEW) ---
                // Solo mostramos ROOT y MACRO_TOPIC al inicio.
                // Ocultamos el resto (SetActive false).
                if (node.type != "ROOT" && node.type != "MACRO_TOPIC")
                {
                    newNode.SetActive(false);
                }
            }
        }

        void BuildConnections()
        {
            GameObject linesObj = new GameObject("GraphConnections");
            linesObj.transform.parent = this.transform;

            MeshFilter mf = linesObj.AddComponent<MeshFilter>();
            MeshRenderer mr = linesObj.AddComponent<MeshRenderer>();
            mr.material = lineMaterial;

            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();

            // Contenedor para organizar las etiquetas en la jerarquía
            GameObject labelsContainer = new GameObject("RelationLabels");
            labelsContainer.transform.parent = this.transform;

            foreach (var node in graphData.nodes)
            {
                // Verificar si tiene padre válido
                if (!string.IsNullOrEmpty(node.parent_id) && nodeMap.ContainsKey(node.parent_id) && nodeMap.ContainsKey(node.id))
                {
                    Vector3 startPos = nodeMap[node.id].transform.position; // Hijo (Entity)
                    Vector3 endPos = nodeMap[node.parent_id].transform.position; // Padre (Document)

                    // 1. DIBUJAR LÍNEA (Geometry)
                    int startIndex = vertices.Count;
                    vertices.Add(startPos);
                    vertices.Add(endPos);
                    indices.Add(startIndex);
                    indices.Add(startIndex + 1);

                    // 2. DIBUJAR ETIQUETA (Solo si existe relation_label)
                    if (!string.IsNullOrEmpty(node.relation_label) && linkLabelPrefab != null)
                    {
                        // Calculamos el punto medio
                        Vector3 midPoint = (startPos + endPos) / 2f;

                        GameObject labelObj = Instantiate(linkLabelPrefab, midPoint, Quaternion.identity, labelsContainer.transform);

                        TextMeshPro tmp = labelObj.GetComponentInChildren<TextMeshPro>();
                        if (tmp != null) tmp.text = node.relation_label;

                        // Opcional: Ajustar LOD para que desaparezcan antes que los nodos grandes
                        NodeLOD lod = labelObj.GetComponent<NodeLOD>();
                        if (lod != null) lod.cullingDistance = 10.0f; // Distancia más corta para textos pequeños
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            mf.mesh = mesh;
        }
    }
}