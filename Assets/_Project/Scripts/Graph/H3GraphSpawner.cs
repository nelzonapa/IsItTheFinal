using ImmersiveGraph.Core;
using ImmersiveGraph.Data;
using ImmersiveGraph.Interaction;
using ImmersiveGraph.Collaboration;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ImmersiveGraph.Visual
{
    public class H3GraphSpawner : MonoBehaviour
    {
        [Header("Interacción")]
        public GraphInteractionManager interactionManager;

        [Header("Colaboración (WIM)")]
        public MiniWorldManager miniWorldManager;

        public static H3GraphSpawner Instance;

        // --- BASES DE DATOS EN MEMORIA ---
        public Dictionary<string, NodeData> nodeDatabase = new Dictionary<string, NodeData>();
        public Dictionary<string, GlobalEntityData> globalEntityDatabase = new Dictionary<string, GlobalEntityData>();

        [Header("Configuración de Archivos JSON")]
        public string jsonFileName = "hierarchy_complete_KG.json";
        public string entityIndexFileName = "global_entity_index.json";

        [Header("Prefabs de Nodos")]
        public GameObject rootPrefab;
        public GameObject communityPrefab;
        public GameObject filePrefab;

        [Header("Prefabs UI & Feedback")]
        public GameObject nodeUIPrefab;
        public GameObject loadingBarPrefab;
        public GameObject reviewedMarkerPrefab;

        [Header("Escala Inicial (Mesa)")]
        [Tooltip("Escala del planeta miniatura sobre la mesa. 0.3 = 30% del tamaño real.")]
        public float initialMiniatureScale = 0.3f;

        [Header("Indicador Grafo de Conocimiento")]
        public GameObject kgIndicatorPrefab;
        public Vector3 kgIndicatorOffset = new Vector3(0, 0.35f, 0);
        public Vector3 kgIndicatorScale = new Vector3(0.1f, 0.1f, 0.1f);

        [Header("Configuración Visual Chincheta")]
        public Vector3 markerOffset = new Vector3(0, 0.25f, 0);
        public Vector3 markerScale = new Vector3(0.2f, 0.2f, 0.2f);

        // --- VARIABLES DE RADIO ---
        [Header("Layout Orgánico (Force-Directed)")]
        public float communityMiniatureRadius = 0.4f;
        public float communityExpandedRadius = 1.2f;
        public float fileOrbitRadius = 0.35f;

        [Header("Límites de Ergonomía VR (Techo y Suelo)")]
        [Tooltip("Altura máxima en el eje Y. Evita que el usuario fuerce el cuello hacia arriba.")]
        public float maxHeightLimit = 0.6f;
        [Tooltip("Altura mínima en el eje Y. Evita que el usuario tenga que mirar hacia sus pies.")]
        public float minHeightLimit = -0.1f;

        [Header("Dinámica de Acomodo Espacial")]
        public bool aplicarFisicaContinua = true;
        [Tooltip("Radio virtual del nodo cuando NO tiene archivos visibles")]
        public float radioBaseNodo = 0.15f;
        [Tooltip("Radio virtual del nodo cuando tiene archivos fantasmas visibles")]
        public float radioExpandidoNodo = 0.35f;
        public float fuerzaDeEmpuje = 2.0f;

        [Header("Estilo de Líneas")]
        public Material lineMaterial;
        public float lineWidth = 0.002f;

        [Header("Posiciones UI")]
        public Vector3 loaderOffset = new Vector3(0, -0.25f, 0);
        public Vector3 uiOffset = new Vector3(0, -0.6f, 0);

        [Header("Referencias de Escena")]
        public Zone3Manager linkedZone3Manager;

        [Header("Feedback de Audio")]
        public AudioClip nodeExpandSound;

        [HideInInspector]
        public List<GraphNode> allSpawnedNodes = new List<GraphNode>();

        [HideInInspector]
        public Dictionary<string, GraphNode> spawnedNodesMap = new Dictionary<string, GraphNode>();

        private List<GraphNode> _activeCommunities = new List<GraphNode>();

        // --- DICCIONARIO DINÁMICO DE COLORES POR FUENTE ---
        private Dictionary<string, Color> sourceColorMap = new Dictionary<string, Color>();

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        IEnumerator Start()
        {
            yield return LoadGraphRoutine();
        }

        void Update()
        {
            if (!aplicarFisicaContinua || _activeCommunities.Count == 0 || transform.childCount == 0 || interactionManager == null) return;

            Transform rootObjTransform = transform.GetChild(0);

            for (int i = 0; i < _activeCommunities.Count; i++)
            {
                GraphNode nodeA = _activeCommunities[i];

                if (nodeA.transform.parent != rootObjTransform) continue;

                bool aHasFiles = nodeA.childNodes != null && nodeA.childNodes.Count > 0 && nodeA.childNodes[0].activeSelf;
                float radA = aHasFiles ? radioExpandidoNodo : radioBaseNodo;

                Vector3 push = Vector3.zero;

                for (int j = 0; j < _activeCommunities.Count; j++)
                {
                    if (i == j) continue;
                    GraphNode nodeB = _activeCommunities[j];
                    if (nodeB.transform.parent != rootObjTransform) continue;

                    bool bHasFiles = nodeB.childNodes != null && nodeB.childNodes.Count > 0 && nodeB.childNodes[0].activeSelf;
                    float radB = bHasFiles ? radioExpandidoNodo : radioBaseNodo;

                    Vector3 diff = nodeA.transform.localPosition - nodeB.transform.localPosition;
                    float dist = diff.magnitude;
                    float minSafeDist = radA + radB;

                    if (dist < minSafeDist && dist > 0.001f)
                    {
                        if (!interactionManager.IsNodeAnimating(nodeA))
                        {
                            push += diff.normalized * (minSafeDist - dist) * fuerzaDeEmpuje;
                        }
                    }
                }

                if (push != Vector3.zero)
                {
                    Vector3 newPos = nodeA.transform.localPosition + push * Time.deltaTime;

                    // 1. Mantenerse sobre el radio de la esfera
                    newPos = newPos.normalized * communityMiniatureRadius;

                    // 2. APLICAR EL TECHO Y EL SUELO
                    newPos.y = Mathf.Clamp(newPos.y, minHeightLimit, maxHeightLimit);

                    nodeA.transform.localPosition = newPos;
                    nodeA.originalLocalPosition = nodeA.transform.localPosition;
                }
            }
        }

        IEnumerator LoadGraphRoutine()
        {
            string indexFilePath = Path.Combine(Application.streamingAssetsPath, entityIndexFileName);
            string indexJsonContent = "";
            yield return ReadFileRoutine(indexFilePath, result => indexJsonContent = result);

            if (!string.IsNullOrEmpty(indexJsonContent)) ParseGlobalIndexNative(indexJsonContent);

            string graphFilePath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
            string graphJsonContent = "";
            yield return ReadFileRoutine(graphFilePath, result => graphJsonContent = result);

            if (!string.IsNullOrEmpty(graphJsonContent))
            {
                try
                {
                    NodeData rootNode = JsonUtility.FromJson<NodeData>(graphJsonContent);
                    if (rootNode != null) GenerateH3Layout(rootNode);
                }
                catch (System.Exception e) { Debug.LogError("[KG System] Error parseando Jerarquía: " + e.Message); }
            }
        }

        IEnumerator ReadFileRoutine(string path, System.Action<string> onCompleted)
        {
            if (path.Contains("://") || path.Contains("jar:"))
            {
                using (UnityWebRequest www = UnityWebRequest.Get(path))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success) onCompleted?.Invoke(www.downloadHandler.text);
                }
            }
            else
            {
                if (File.Exists(path)) onCompleted?.Invoke(File.ReadAllText(path));
            }
        }

        void ParseGlobalIndexNative(string jsonText)
        {
            globalEntityDatabase.Clear();
            int startIndex = jsonText.IndexOf('{');
            if (startIndex == -1) return;

            int i = startIndex + 1;
            while (i < jsonText.Length)
            {
                int keyStart = jsonText.IndexOf('"', i);
                if (keyStart == -1) break;
                int keyEnd = jsonText.IndexOf('"', keyStart + 1);
                if (keyEnd == -1) break;
                string key = jsonText.Substring(keyStart + 1, keyEnd - keyStart - 1);
                int objStart = jsonText.IndexOf('{', keyEnd + 1);
                if (objStart == -1) break;

                string inBetween = jsonText.Substring(keyEnd + 1, objStart - keyEnd - 1);
                if (inBetween.Contains("\"")) { i = keyEnd + 1; continue; }

                int braceCount = 1;
                int objEnd = objStart + 1;
                while (objEnd < jsonText.Length && braceCount > 0)
                {
                    if (jsonText[objEnd] == '{') braceCount++;
                    else if (jsonText[objEnd] == '}') braceCount--;
                    objEnd++;
                }

                string objJson = jsonText.Substring(objStart, objEnd - objStart);
                GlobalEntityData data = JsonUtility.FromJson<GlobalEntityData>(objJson);
                if (data != null && !string.IsNullOrEmpty(data.nombre_original)) globalEntityDatabase[key] = data;
                i = objEnd;
            }
        }

        private Color GetColorForSource(string source)
        {
            if (string.IsNullOrEmpty(source)) source = "Desconocido";

            if (!sourceColorMap.ContainsKey(source))
            {
                float hue = (sourceColorMap.Count * 0.618033988749895f) % 1f;
                sourceColorMap[source] = Color.HSVToRGB(hue, 0.7f, 0.9f);
            }
            return sourceColorMap[source];
        }

        void GenerateH3Layout(NodeData rootData)
        {
            nodeDatabase.Clear();
            allSpawnedNodes.Clear();
            spawnedNodesMap.Clear();
            _activeCommunities.Clear();
            sourceColorMap.Clear();

            RegisterNodeToDatabase(rootData);

            foreach (Transform child in transform) Destroy(child.gameObject);

            GameObject rootObj = CreateNodeObject(rootPrefab, transform, new Vector3(0, 0.3f, 0), rootData, "root", null, null, Color.white);

            if (rootData.children == null) return;

            Vector3[] commPositions = SimulateOrganicForceDirectedLayout(rootData.children, communityMiniatureRadius);
            int commCount = rootData.children.Count;

            for (int i = 0; i < commCount; i++)
            {
                NodeData commData = rootData.children[i];

                string dominantSource = "Desconocido";
                if (commData.children != null && commData.children.Count > 0)
                {
                    Dictionary<string, int> sourceCounts = new Dictionary<string, int>();
                    foreach (var file in commData.children)
                    {
                        string src = (file.data != null && !string.IsNullOrEmpty(file.data.source)) ? file.data.source : "Desconocido";
                        if (!sourceCounts.ContainsKey(src)) sourceCounts[src] = 0;
                        sourceCounts[src]++;
                    }

                    int max = 0;
                    foreach (var kvp in sourceCounts)
                    {
                        if (kvp.Value > max) { max = kvp.Value; dominantSource = kvp.Key; }
                    }
                }

                Color groupColor = GetColorForSource(dominantSource);

                GameObject lineToComm = CreateLine(rootObj.transform.position, rootObj.transform);
                GameObject commObj = CreateNodeObject(communityPrefab, rootObj.transform, commPositions[i], commData, "community", rootObj.transform, lineToComm.GetComponent<LineRenderer>(), groupColor);

                GraphNode commLogic = commObj.GetComponent<GraphNode>();
                if (commLogic != null) _activeCommunities.Add(commLogic);

                if (commData.children != null)
                {
                    int fileCount = commData.children.Count;
                    Vector3 directionOut = commObj.transform.localPosition.normalized;

                    // Llama al NUEVO algoritmo anti-solapamiento
                    Vector3[] filePositions = SimulateOrganicFileCloud(fileCount, fileOrbitRadius, directionOut);

                    for (int j = 0; j < fileCount; j++)
                    {
                        NodeData fileData = commData.children[j];

                        string fileSrc = (fileData.data != null && !string.IsNullOrEmpty(fileData.data.source)) ? fileData.data.source : "Desconocido";
                        Color fileColor = GetColorForSource(fileSrc);

                        GameObject lineToFile = CreateLine(commObj.transform.position, commObj.transform);
                        GameObject fileObj = CreateNodeObject(filePrefab, commObj.transform, filePositions[j], fileData, "file", commObj.transform, lineToFile.GetComponent<LineRenderer>(), fileColor);

                        if (commLogic != null)
                        {
                            commLogic.childNodes.Add(fileObj);
                            commLogic.childConnectionLines.Add(lineToFile);
                        }
                    }
                }
                if (commLogic != null) commLogic.InitializeNode(rootObj.transform, lineToComm.GetComponent<LineRenderer>());
            }

            rootObj.transform.localScale = new Vector3(initialMiniatureScale, initialMiniatureScale, initialMiniatureScale);

            if (interactionManager != null) interactionManager.InitializeGraph(rootObj.transform, communityMiniatureRadius, communityExpandedRadius);
            if (miniWorldManager != null) miniWorldManager.BuildMiniatureFromRealGraph(rootObj.transform, _activeCommunities);
        }

        private Vector3[] SimulateOrganicForceDirectedLayout(List<NodeData> communities, float baseRadius)
        {
            int count = communities.Count;
            Vector3[] pos = new Vector3[count];
            float[] masses = new float[count];

            for (int i = 0; i < count; i++)
            {
                masses[i] = (communities[i].children != null && communities[i].children.Count > 0) ? communities[i].children.Count : 2f;
                Vector3 randomDir = Random.onUnitSphere;
                if (randomDir.z < 0.2f) randomDir.z = Mathf.Abs(randomDir.z) + 0.2f;
                pos[i] = randomDir.normalized * baseRadius * Random.Range(0.7f, 1.3f);
            }

            float k = baseRadius * 0.4f;
            for (int iter = 0; iter < 100; iter++)
            {
                Vector3[] disp = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    for (int j = 0; j < count; j++)
                    {
                        if (i == j) continue;
                        Vector3 delta = pos[i] - pos[j];
                        float dist = delta.magnitude;
                        if (dist < 0.01f) dist = 0.01f;
                        float force = (k * k) / dist * (masses[i] + masses[j]) * 0.05f;
                        disp[i] += (delta / dist) * force;
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    Vector3 delta = pos[i];
                    float dist = delta.magnitude;
                    float force = (dist * dist) / k;
                    disp[i] -= (delta / dist) * force * 0.1f;
                }
                for (int i = 0; i < count; i++)
                {
                    pos[i] += disp[i] * 0.05f;
                    if (pos[i].z < 0.1f) pos[i].z = 0.1f;

                    pos[i].y = Mathf.Clamp(pos[i].y, minHeightLimit, maxHeightLimit);
                }
            }
            return pos;
        }

        // --- NUEVO ALGORITMO ANTI-SOLAPAMIENTO PARA ARCHIVOS ---
        private Vector3[] SimulateOrganicFileCloud(int fileCount, float radius, Vector3 communityDirection)
        {
            Vector3[] pos = new Vector3[fileCount];
            Vector3 centerOffset = communityDirection * (radius * 0.5f);

            // 1. Asignación inicial aleatoria
            for (int i = 0; i < fileCount; i++)
            {
                Vector3 randomPoint = Random.insideUnitSphere;
                if (Vector3.Dot(randomPoint, communityDirection) < 0) randomPoint = -randomPoint;
                pos[i] = centerOffset + randomPoint * (radius * Random.Range(0.3f, 1.2f));
            }

            // 2. Relajación por Fuerza Dirigida (se empujan entre sí para dejar espacio)
            float k = radius * 0.45f; // Radio ideal de separación entre archivos
            for (int iter = 0; iter < 50; iter++) // 50 iteraciones es suficiente
            {
                Vector3[] disp = new Vector3[fileCount];

                // Repulsión mutua
                for (int i = 0; i < fileCount; i++)
                {
                    for (int j = 0; j < fileCount; j++)
                    {
                        if (i == j) continue;
                        Vector3 delta = pos[i] - pos[j];
                        float dist = delta.magnitude;
                        if (dist < 0.01f) dist = 0.01f;

                        if (dist < k * 1.5f) // Solo se empujan si están muy cerca
                        {
                            float force = (k * k) / dist;
                            disp[i] += (delta / dist) * force * 0.1f;
                        }
                    }
                }

                // Atracción ligera al centro de la comunidad
                for (int i = 0; i < fileCount; i++)
                {
                    Vector3 delta = pos[i] - centerOffset;
                    float dist = delta.magnitude;
                    float force = (dist * dist) / (radius * 2.0f);
                    disp[i] -= (delta / dist) * force * 0.1f;
                }

                // Aplicar el movimiento y forzar los límites ergonómicos
                for (int i = 0; i < fileCount; i++)
                {
                    pos[i] += disp[i];
                    pos[i].y = Mathf.Clamp(pos[i].y, minHeightLimit, maxHeightLimit);
                }
            }

            return pos;
        }

        void RegisterNodeToDatabase(NodeData node)
        {
            if (node == null) return;
            if (!string.IsNullOrEmpty(node.id) && !nodeDatabase.ContainsKey(node.id)) nodeDatabase.Add(node.id, node);
            if (node.children != null) foreach (var child in node.children) RegisterNodeToDatabase(child);
        }

        public NodeData GetNodeDataByID(string id)
        {
            return nodeDatabase.ContainsKey(id) ? nodeDatabase[id] : null;
        }

        GameObject CreateNodeObject(GameObject prefab, Transform parent, Vector3 localPos, NodeData data, string type, Transform parentNode, LineRenderer incomingLine, Color nodeColor)
        {
            GameObject obj = Instantiate(prefab, parent);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = prefab.transform.localScale;
            obj.name = $"{type.ToUpper()}_{data.title}";

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = nodeColor;

            GraphNode logic = obj.AddComponent<GraphNode>();
            logic.nodeType = type;
            logic.myData = data;
            logic.localZone3Manager = linkedZone3Manager;
            logic.interactionManager = this.interactionManager;
            logic.miniWorldManager = this.miniWorldManager;
            logic.expandSound = nodeExpandSound;
            logic.reviewedMarkerPrefab = reviewedMarkerPrefab;
            logic.markerLocalOffset = markerOffset;
            logic.markerLocalScale = markerScale;

            logic.InitializeNode(parentNode, incomingLine);

            allSpawnedNodes.Add(logic);
            spawnedNodesMap[data.id] = logic;

            if (loadingBarPrefab != null)
            {
                GameObject loadObj = Instantiate(loadingBarPrefab, obj.transform);
                loadObj.transform.localPosition = loaderOffset;
                loadObj.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                logic.loaderUI = loadObj.GetComponent<NodeLoaderController>();
            }

            if (nodeUIPrefab != null)
            {
                GameObject uiObj = Instantiate(nodeUIPrefab, obj.transform);
                uiObj.transform.localPosition = uiOffset;
                uiObj.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                NodeUIController uiController = uiObj.GetComponent<NodeUIController>();
                if (uiController != null) uiController.SetupUI(data.title, "");
            }

            if (type == "file" && data.knowledge_graph != null && data.knowledge_graph.Length > 0 && kgIndicatorPrefab != null)
            {
                GameObject kgIcon = Instantiate(kgIndicatorPrefab, obj.transform);
                kgIcon.transform.localPosition = kgIndicatorOffset;
                kgIcon.transform.localScale = kgIndicatorScale;
            }

            return obj;
        }

        GameObject CreateLine(Vector3 start, Transform parent)
        {
            GameObject lineObj = new GameObject("Link");
            lineObj.transform.SetParent(parent);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, start);
            lr.useWorldSpace = true;
            return lineObj;
        }

        public int GetFileCountForEntity(string entityName)
        {
            string key = entityName.ToLower();
            if (!globalEntityDatabase.ContainsKey(key)) return 0;
            int fileCount = 0;
            string[] allNodes = globalEntityDatabase[key].nodos;
            foreach (string nodeId in allNodes)
            {
                if (!nodeId.ToUpper().Contains("COMUNIDAD") && !nodeId.ToUpper().Contains("ROOT")) fileCount++;
            }
            return fileCount;
        }

        public void HighlightNodesByEntity(string entityName)
        {
            string key = entityName.ToLower();
            if (!globalEntityDatabase.ContainsKey(key)) return;
            string[] targetIDs = globalEntityDatabase[key].nodos;
            HashSet<string> targetSet = new HashSet<string>(targetIDs);
            foreach (GraphNode node in allSpawnedNodes)
            {
                if (node == null || node.myData == null || node.nodeType == "root") continue;
                node.SetVisualState(targetSet.Contains(node.myData.id) ? 1 : 2);
            }
        }

        public void ClearAllHighlights()
        {
            foreach (GraphNode node in allSpawnedNodes)
            {
                if (node != null) node.SetVisualState(0);
            }
        }
    }
}