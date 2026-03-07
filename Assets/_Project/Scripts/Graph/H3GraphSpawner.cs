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

        // --- NUEVAS VARIABLES DE RADIO ---
        [Header("Layout Orgánico (Force-Directed)")]
        [Tooltip("Radio de separación cuando las comunidades están en la MESA (miniatura).")]
        public float communityMiniatureRadius = 0.4f;
        [Tooltip("Radio de separación cuando las comunidades forman la BÓVEDA en el fondo.")]
        public float communityExpandedRadius = 1.2f;
        [Tooltip("Qué tan dispersos están los archivos alrededor de su comunidad.")]
        public float fileOrbitRadius = 0.35f;

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

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        IEnumerator Start()
        {
            yield return LoadGraphRoutine();
        }

        IEnumerator LoadGraphRoutine()
        {
            string indexFilePath = Path.Combine(Application.streamingAssetsPath, entityIndexFileName);
            string indexJsonContent = "";
            yield return ReadFileRoutine(indexFilePath, result => indexJsonContent = result);

            if (!string.IsNullOrEmpty(indexJsonContent))
            {
                ParseGlobalIndexNative(indexJsonContent);
            }

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
                if (inBetween.Contains("\""))
                {
                    i = keyEnd + 1;
                    continue;
                }

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
                if (data != null && !string.IsNullOrEmpty(data.nombre_original))
                {
                    globalEntityDatabase[key] = data;
                }
                i = objEnd;
            }
        }

        void GenerateH3Layout(NodeData rootData)
        {
            nodeDatabase.Clear();
            allSpawnedNodes.Clear();
            spawnedNodesMap.Clear();
            RegisterNodeToDatabase(rootData);

            foreach (Transform child in transform) Destroy(child.gameObject);

            GameObject rootObj = CreateNodeObject(rootPrefab, transform, new Vector3(0, 0.3f, 0), rootData, "root", null, null, Color.white);

            if (rootData.children == null) return;

            // --- FASE 3: APLICAMOS EL ALGORITMO CON EL RADIO DE LA MESA (Miniatura) ---
            Vector3[] commPositions = SimulateOrganicForceDirectedLayout(rootData.children, communityMiniatureRadius);

            List<GraphNode> createdCommunities = new List<GraphNode>();
            int commCount = rootData.children.Count;

            for (int i = 0; i < commCount; i++)
            {
                NodeData commData = rootData.children[i];
                Color groupColor = Color.HSVToRGB((float)i / commCount, 0.7f, 0.9f);

                GameObject lineToComm = CreateLine(rootObj.transform.position, rootObj.transform);
                GameObject commObj = CreateNodeObject(communityPrefab, rootObj.transform, commPositions[i], commData, "community", rootObj.transform, lineToComm.GetComponent<LineRenderer>(), groupColor);

                GraphNode commLogic = commObj.GetComponent<GraphNode>();
                if (commLogic != null) createdCommunities.Add(commLogic);

                if (commData.children != null)
                {
                    int fileCount = commData.children.Count;
                    Vector3 directionOut = commObj.transform.localPosition.normalized;

                    Vector3[] filePositions = SimulateOrganicFileCloud(fileCount, fileOrbitRadius, directionOut);

                    for (int j = 0; j < fileCount; j++)
                    {
                        NodeData fileData = commData.children[j];
                        GameObject lineToFile = CreateLine(commObj.transform.position, commObj.transform);
                        GameObject fileObj = CreateNodeObject(filePrefab, commObj.transform, filePositions[j], fileData, "file", commObj.transform, lineToFile.GetComponent<LineRenderer>(), groupColor);

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

            // PASAMOS AMBOS RADIOS AL MANAGER PARA LA ANIMACIÓN
            if (interactionManager != null) interactionManager.InitializeGraph(rootObj.transform, communityMiniatureRadius, communityExpandedRadius);

            if (miniWorldManager != null) miniWorldManager.BuildMiniatureFromRealGraph(rootObj.transform, createdCommunities);
        }

        // ==========================================
        // MOTORES FÍSICOS FORCE-DIRECTED (ESTÁTICOS)
        // ==========================================

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
                }
            }

            return pos;
        }

        private Vector3[] SimulateOrganicFileCloud(int fileCount, float radius, Vector3 communityDirection)
        {
            Vector3[] pos = new Vector3[fileCount];
            for (int i = 0; i < fileCount; i++)
            {
                Vector3 randomPoint = Random.insideUnitSphere;
                if (Vector3.Dot(randomPoint, communityDirection) < 0)
                {
                    randomPoint = -randomPoint;
                }
                pos[i] = communityDirection * (radius * 0.5f) + randomPoint * (radius * Random.Range(0.5f, 1.5f));
            }
            return pos;
        }

        void RegisterNodeToDatabase(NodeData node)
        {
            if (node == null) return;
            if (!string.IsNullOrEmpty(node.id) && !nodeDatabase.ContainsKey(node.id))
            {
                nodeDatabase.Add(node.id, node);
            }
            if (node.children != null)
            {
                foreach (var child in node.children) RegisterNodeToDatabase(child);
            }
        }

        public NodeData GetNodeDataByID(string id)
        {
            if (nodeDatabase.ContainsKey(id)) return nodeDatabase[id];
            return null;
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

            if (type == "file" && data.knowledge_graph != null && data.knowledge_graph.Length > 0)
            {
                if (kgIndicatorPrefab != null)
                {
                    GameObject kgIcon = Instantiate(kgIndicatorPrefab, obj.transform);
                    kgIcon.transform.localPosition = kgIndicatorOffset;
                    kgIcon.transform.localScale = kgIndicatorScale;
                }
            }

            return obj;
        }

        GameObject CreateLine(Vector3 start, Transform parent)
        {
            GameObject lineObj = new GameObject("Link");
            lineObj.transform.SetParent(parent);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();

            if (lineMaterial != null) lr.material = lineMaterial;
            else lr.material = new Material(Shader.Find("Sprites/Default"));

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
                if (!nodeId.ToUpper().Contains("COMUNIDAD") && !nodeId.ToUpper().Contains("ROOT"))
                {
                    fileCount++;
                }
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
                if (node == null || node.myData == null) continue;
                if (node.nodeType == "root") continue;

                if (targetSet.Contains(node.myData.id))
                {
                    node.SetVisualState(1);
                }
                else
                {
                    node.SetVisualState(2);
                }
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