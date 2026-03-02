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

        // NUEVO: Diccionario veloz para buscar entidades en todo el grafo
        public Dictionary<string, GlobalEntityData> globalEntityDatabase = new Dictionary<string, GlobalEntityData>();

        [Header("Configuración de Archivos JSON")]
        [Tooltip("El archivo principal con el Knowledge Graph incrustado")]
        public string jsonFileName = "hierarchy_complete_KG.json";
        [Tooltip("El archivo del índice de búsqueda rápida")]
        public string entityIndexFileName = "global_entity_index.json";

        [Header("Prefabs de Nodos")]
        public GameObject rootPrefab;
        public GameObject communityPrefab;
        public GameObject filePrefab;

        [Header("Prefabs UI & Feedback")]
        public GameObject nodeUIPrefab;
        public GameObject loadingBarPrefab;
        public GameObject reviewedMarkerPrefab;

        [Header("Configuración Visual Chincheta")]
        public Vector3 markerOffset = new Vector3(0, 0.25f, 0);
        public Vector3 markerScale = new Vector3(0.2f, 0.2f, 0.2f);

        [Header("Layout (Distancias)")]
        public float communityOrbitRadius = 0.6f;
        public float fileOrbitRadius = 0.25f;

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
            // 1. CARGAR EL ÍNDICE GLOBAL DE ENTIDADES (Motor de Búsqueda)
            string indexFilePath = Path.Combine(Application.streamingAssetsPath, entityIndexFileName);
            string indexJsonContent = "";
            yield return ReadFileRoutine(indexFilePath, result => indexJsonContent = result);

            if (!string.IsNullOrEmpty(indexJsonContent))
            {
                ParseGlobalIndexNative(indexJsonContent);
                Debug.Log($"[KG System] Índice global cargado con {globalEntityDatabase.Count} entidades.");
            }

            // 2. CARGAR LA JERARQUÍA PRINCIPAL (El Grafo Visual)
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

        // --- RUTINA AUXILIAR PARA LEER ARCHIVOS (PC o Android/Quest) ---
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

        // =========================================================================
        // PARSER NATIVO EN C# PARA BURLAR LA LIMITACIÓN DE DICCIONARIOS EN UNITY
        // =========================================================================
        void ParseGlobalIndexNative(string jsonText)
        {
            globalEntityDatabase.Clear();

            // Buscar la apertura del diccionario JSON global
            int startIndex = jsonText.IndexOf('{');
            if (startIndex == -1) return;

            int i = startIndex + 1;
            while (i < jsonText.Length)
            {
                // Buscar la siguiente llave ("nombre_entidad")
                int keyStart = jsonText.IndexOf('"', i);
                if (keyStart == -1) break;

                int keyEnd = jsonText.IndexOf('"', keyStart + 1);
                if (keyEnd == -1) break;

                string key = jsonText.Substring(keyStart + 1, keyEnd - keyStart - 1);

                // Buscar el inicio del objeto asociado '{'
                int objStart = jsonText.IndexOf('{', keyEnd + 1);
                if (objStart == -1) break;

                // Filtro de seguridad: Si hay comillas entre la llave y la llave de apertura, no era una entidad raíz
                string inBetween = jsonText.Substring(keyEnd + 1, objStart - keyEnd - 1);
                if (inBetween.Contains("\""))
                {
                    i = keyEnd + 1;
                    continue;
                }

                // Algoritmo de "Bracket Counting" para extraer el objeto exacto anidado
                int braceCount = 1;
                int objEnd = objStart + 1;
                while (objEnd < jsonText.Length && braceCount > 0)
                {
                    if (jsonText[objEnd] == '{') braceCount++;
                    else if (jsonText[objEnd] == '}') braceCount--;
                    objEnd++;
                }

                string objJson = jsonText.Substring(objStart, objEnd - objStart);

                // Usamos JsonUtility solo para el "pedacito" estructurado que sí entiende
                GlobalEntityData data = JsonUtility.FromJson<GlobalEntityData>(objJson);
                if (data != null && !string.IsNullOrEmpty(data.nombre_original))
                {
                    globalEntityDatabase[key] = data; // Almacenado rápido
                }

                i = objEnd; // Avanzar el escáner
            }
        }

        void GenerateH3Layout(NodeData rootData)
        {
            nodeDatabase.Clear();
            RegisterNodeToDatabase(rootData);

            foreach (Transform child in transform) Destroy(child.gameObject);

            GameObject rootObj = CreateNodeObject(rootPrefab, transform, new Vector3(0, 0.3f, 0), rootData, "root", null, null, Color.white);

            if (rootData.children == null) return;

            int commCount = rootData.children.Count;
            Vector3[] commPositions = HyperbolicMath.GetFibonacciSphere(commCount, communityOrbitRadius);
            List<GraphNode> createdCommunities = new List<GraphNode>();

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
                    Vector3[] filePositions = HyperbolicMath.GetOrientedHemisphere(fileCount, fileOrbitRadius, directionOut);

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

            if (interactionManager != null) interactionManager.InitializeGraph(rootObj.transform);
            if (miniWorldManager != null) miniWorldManager.BuildMiniatureFromRealGraph(rootObj.transform, createdCommunities);
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
    }
}