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
        [Header("Prefabs de Formas")]
        public GameObject spherePrefab;
        public GameObject cubePrefab;
        public GameObject capsulePrefab;

        [Header("Configuración Visual")]
        public Material lineMaterial;

        // Diccionario de Nodos (GameObject)
        private Dictionary<string, GameObject> nodeMap = new Dictionary<string, GameObject>();

        // Diccionario de Configuración Visual (Colores, Shapes)
        private Dictionary<string, VisualSetting> visualConfigMap = new Dictionary<string, VisualSetting>();

        // Datos crudos
        private AppConfigContainer appConfig;
        private GraphDataContainer graphData;

        void Start()
        {
            // Limpieza inicial por si acaso
            nodeMap.Clear();
            visualConfigMap.Clear();

            StartCoroutine(LoadFilesAndBuild());
        }

        IEnumerator LoadFilesAndBuild()
        {
            // 1. CARGAR CONFIGURACIÓN (App Config)
            string configPath = Path.Combine(Application.streamingAssetsPath, "unity_app_config.json");
            if (File.Exists(configPath))
            {
                string jsonContent = File.ReadAllText(configPath);
                appConfig = JsonUtility.FromJson<AppConfigContainer>(jsonContent);

                // TRUCO: Convertir el mapa plano a un Diccionario real para usarlo fácil luego
                if (appConfig.visual_settings != null)
                {
                    if (appConfig.visual_settings.ROOT != null) visualConfigMap.Add("ROOT", appConfig.visual_settings.ROOT);
                    if (appConfig.visual_settings.MACRO_TOPIC != null) visualConfigMap.Add("MACRO_TOPIC", appConfig.visual_settings.MACRO_TOPIC);
                    if (appConfig.visual_settings.MICRO_TOPIC != null) visualConfigMap.Add("MICRO_TOPIC", appConfig.visual_settings.MICRO_TOPIC);
                    if (appConfig.visual_settings.DOCUMENT != null) visualConfigMap.Add("DOCUMENT", appConfig.visual_settings.DOCUMENT);
                    if (appConfig.visual_settings.ENTITY != null) visualConfigMap.Add("ENTITY", appConfig.visual_settings.ENTITY);
                }

                Debug.Log("Configuración cargada y procesada.");
            }
            else { Debug.LogError("Falta unity_app_config.json"); yield break; }

            // 2. CARGAR DATOS DEL GRAFO
            string dataPath = Path.Combine(Application.streamingAssetsPath, "unity_graph_data.json");
            if (File.Exists(dataPath))
            {
                string dataContent = File.ReadAllText(dataPath);
                graphData = JsonUtility.FromJson<GraphDataContainer>(dataContent);
                Debug.Log($"Grafo cargado. Nodos a procesar: {graphData.nodes.Count}");
            }
            else { Debug.LogError("Falta unity_graph_data.json"); yield break; }

            // 3. CONSTRUIR
            BuildNodes();
            BuildLines();

            yield return null;
        }

        void BuildNodes()
        {
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

            foreach (var node in graphData.nodes)
            {
                // A. Buscar configuración en el DICCIONARIO que creamos (Ya no falla)
                if (!visualConfigMap.TryGetValue(node.type, out VisualSetting setting))
                {
                    // Si falla, es porque el JSON trae un tipo nuevo no mapeado.
                    // Usamos un fallback para no romper el loop
                    Debug.LogWarning($"Tipo desconocido: {node.type}. Usando defaults.");
                    continue;
                }

                // B. Elegir Prefab
                GameObject prefabToUse = spherePrefab;
                if (setting.shape.Equals("Cube", System.StringComparison.OrdinalIgnoreCase)) prefabToUse = cubePrefab;
                else if (setting.shape.Equals("Capsule", System.StringComparison.OrdinalIgnoreCase)) prefabToUse = capsulePrefab;
                else if (setting.shape.Equals("Diamond", System.StringComparison.OrdinalIgnoreCase)) prefabToUse = spherePrefab; // Fallback para Diamond

                // C. Instanciar
                Vector3 pos = node.position.ToVector3();
                GameObject newNode = Instantiate(prefabToUse, pos, Quaternion.identity, this.transform);
                newNode.name = node.id;

                // D. Escala
                newNode.transform.localScale = Vector3.one * setting.scale;

                // E. Color y Emisión
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

                // F. Texto
                TextMeshPro textComp = newNode.GetComponentInChildren<TextMeshPro>();
                if (textComp != null)
                {
                    textComp.text = node.label;
                    newNode.AddComponent<NodeLOD>();
                }

                // G. Registrar
                if (!nodeMap.ContainsKey(node.id))
                {
                    nodeMap.Add(node.id, newNode);
                }
            }
        }

        void BuildLines()
        {
            GameObject linesObj = new GameObject("GraphConnections");
            linesObj.transform.parent = this.transform;

            MeshFilter mf = linesObj.AddComponent<MeshFilter>();
            MeshRenderer mr = linesObj.AddComponent<MeshRenderer>();
            mr.material = lineMaterial;

            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();

            foreach (var node in graphData.nodes)
            {
                if (!string.IsNullOrEmpty(node.parent_id) && nodeMap.ContainsKey(node.parent_id) && nodeMap.ContainsKey(node.id))
                {
                    Vector3 startPos = nodeMap[node.id].transform.position;
                    Vector3 endPos = nodeMap[node.parent_id].transform.position;

                    int startIndex = vertices.Count;
                    vertices.Add(startPos);
                    vertices.Add(endPos);

                    indices.Add(startIndex);
                    indices.Add(startIndex + 1);
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            mf.mesh = mesh;

            Debug.Log($"¡Éxito! Conexiones generadas: {indices.Count / 2}");
        }
    }
}