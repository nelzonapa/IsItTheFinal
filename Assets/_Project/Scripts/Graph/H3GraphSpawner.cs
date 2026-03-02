using ImmersiveGraph.Core;
using ImmersiveGraph.Data;
using ImmersiveGraph.Interaction;
using ImmersiveGraph.Visual;
using ImmersiveGraph.Collaboration; // <--- Para el miniworld
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
        [Tooltip("Arrastra aquí el objeto Zone7_MiniWorld_Base que contiene el MiniWorldManager")]
        public MiniWorldManager miniWorldManager; // <--- NUEVO

        public static H3GraphSpawner Instance;

        public Dictionary<string, NodeData> nodeDatabase = new Dictionary<string, NodeData>();

        [Header("Configuración de Archivo")]
        public string jsonFileName = "hierarchy_complete.json";

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
            string filePath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
            string jsonContent = "";

            if (filePath.Contains("://") || filePath.Contains("jar:"))
            {
                using (UnityWebRequest www = UnityWebRequest.Get(filePath))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success) jsonContent = www.downloadHandler.text;
                    else yield break;
                }
            }
            else
            {
                if (File.Exists(filePath)) jsonContent = File.ReadAllText(filePath);
                else yield break;
            }

            if (!string.IsNullOrEmpty(jsonContent))
            {
                try
                {
                    NodeData rootNode = JsonUtility.FromJson<NodeData>(jsonContent);
                    if (rootNode != null) GenerateH3Layout(rootNode);
                }
                catch (System.Exception e) { Debug.LogError("Error parseando JSON: " + e.Message); }
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

            // Lista temporal para recolectar comunidades para el minimundo
            List<GraphNode> createdCommunities = new List<GraphNode>();

            for (int i = 0; i < commCount; i++)
            {
                NodeData commData = rootData.children[i];
                Color groupColor = Color.HSVToRGB((float)i / commCount, 0.7f, 0.9f);

                GameObject lineToComm = CreateLine(rootObj.transform.position, rootObj.transform);
                GameObject commObj = CreateNodeObject(communityPrefab, rootObj.transform, commPositions[i], commData, "community", rootObj.transform, lineToComm.GetComponent<LineRenderer>(), groupColor);

                GraphNode commLogic = commObj.GetComponent<GraphNode>();
                if (commLogic != null) createdCommunities.Add(commLogic); // Guardamos la referencia

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

            if (interactionManager != null)
            {
                interactionManager.InitializeGraph(rootObj.transform);
            }

            // --- NUEVO: EJECUTAR FASE 2 (CONSTRUIR MINIMUNDO) ---
            if (miniWorldManager != null)
            {
                miniWorldManager.BuildMiniatureFromRealGraph(rootObj.transform, createdCommunities);
            }
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

            // --- NUEVA LÍNEA ---
            logic.miniWorldManager = this.miniWorldManager;
            // -------------------

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

            // --- NUEVO: INSTANCIAR PANEL UI (TÍTULO) ---
            // ==========================================
            if (nodeUIPrefab != null)
            {
                GameObject uiObj = Instantiate(nodeUIPrefab, obj.transform);
                uiObj.transform.localPosition = uiOffset;

                // Aplicamos la posición debajo del nodo
                uiObj.transform.localPosition = uiOffset;
                uiObj.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

                // Inyectar la información
                NodeUIController uiController = uiObj.GetComponent<NodeUIController>();
                if (uiController != null)
                {
                    // Solo pasamos el título. El resumen va vac o porque el script lo apagar .
                    uiController.SetupUI(data.title, "");
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
    }
}