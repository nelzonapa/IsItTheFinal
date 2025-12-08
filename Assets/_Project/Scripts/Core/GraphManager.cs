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
            // Esperar un frame para asegurar que las posiciones locales se asienten antes de dibujar líneas
            yield return null;
            BuildConnections();
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

                // --- CORRECCIÓN 1: Instanciar relativo al padre ---
                // Instanciamos el nodo como hijo de ESTE objeto (GraphManager)
                GameObject newNode = Instantiate(prefabToUse, this.transform);

                // Asignamos la posición LOCAL usando los datos del JSON.
                // Así, si mueves la mesa, los nodos se mueven con ella.
                newNode.transform.localPosition = node.position.ToVector3();
                newNode.transform.localRotation = Quaternion.identity;

                newNode.name = node.id;

                // Aplicar escala
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

                if (!nodeMap.ContainsKey(node.id)) nodeMap.Add(node.id, newNode);

                // --- FILTRO DE VISTA GENERAL ---
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

            // --- CORRECCIÓN 2: Resetear transformaciones del contenedor de líneas ---
            // Esto asegura que las coordenadas locales coincidan con las de los nodos hermanos
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
            labelsContainer.transform.localScale = Vector3.one; // Importante para que el texto no se deforme

            foreach (var node in graphData.nodes)
            {
                if (!string.IsNullOrEmpty(node.parent_id) && nodeMap.ContainsKey(node.parent_id) && nodeMap.ContainsKey(node.id))
                {
                    GameObject childObj = nodeMap[node.id];
                    GameObject parentObj = nodeMap[node.parent_id];

                    if (!childObj.activeSelf || !parentObj.activeSelf) continue;

                    // --- CORRECCIÓN 3: USAR LOCAL POSITION ---
                    // Usamos localPosition porque ambos (nodos y líneas) son hijos del mismo padre (GraphSystem)
                    Vector3 startPos = childObj.transform.localPosition;
                    Vector3 endPos = parentObj.transform.localPosition;

                    int startIndex = vertices.Count;
                    vertices.Add(startPos);
                    vertices.Add(endPos);
                    indices.Add(startIndex);
                    indices.Add(startIndex + 1);

                    // ETIQUETAS
                    if (!string.IsNullOrEmpty(node.relation_label) && linkLabelPrefab != null)
                    {
                        Vector3 midPoint = (startPos + endPos) / 2f;

                        // Instanciamos y luego asignamos localPosition para que esté correcto
                        GameObject labelObj = Instantiate(linkLabelPrefab, labelsContainer.transform);
                        labelObj.transform.localPosition = midPoint;
                        labelObj.transform.localRotation = Quaternion.identity;

                        TextMeshPro tmp = labelObj.GetComponentInChildren<TextMeshPro>();
                        if (tmp != null) tmp.text = node.relation_label;

                        NodeLOD lod = labelObj.GetComponent<NodeLOD>();
                        if (lod != null) lod.cullingDistance = 10.0f;
                    }
                }
            }

            Mesh mesh = new Mesh();
            // Truco para que el frustum culling no oculte la malla antes de tiempo si es muy grande
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices.ToArray();
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);

            // Recalcular límites es importante cuando manipulamos vértices manualmente
            mesh.RecalculateBounds();
            mf.mesh = mesh;
        }
    }
}