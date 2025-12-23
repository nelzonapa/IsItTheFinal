using ImmersiveGraph.Core;
using ImmersiveGraph.Data;
using ImmersiveGraph.Interaction;
using ImmersiveGraph.Visual;
using System.Collections;
using System.Collections.Generic; // Necesario para Dictionary
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ImmersiveGraph.Visual
{
    public class H3GraphSpawner : MonoBehaviour
    {

        public static H3GraphSpawner Instance; // Singleton para acceso global

        // --- NUEVO: BASE DE DATOS EN MEMORIA ---
        public Dictionary<string, NodeData> nodeDatabase = new Dictionary<string, NodeData>();
        // ---------------------------------------}

        [Header("Configuración")]
        public string jsonFileName = "hierarchy_complete.json";
        public GameObject rootPrefab;
        public GameObject communityPrefab;
        public GameObject filePrefab;

        [Header("Prefabs UI")]
        public GameObject nodeUIPrefab;
        public GameObject loadingBarPrefab;

        // --- CHINCHETA CONFIG ---
        [Header("Configuración Visual Chincheta")]
        public GameObject reviewedMarkerPrefab; // Arrastra tu cubo aquí
        public Vector3 markerOffset = new Vector3(0, 0.25f, 0); // Altura sobre el nodo
        public Vector3 markerScale = new Vector3(0.2f, 0.2f, 0.2f); // Tamaño del cubo
        // ------------------------

        [Header("Layout")]
        public float communityOrbitRadius = 0.4f;
        public float fileOrbitRadius = 0.15f;
        public Material lineMaterial;
        public float lineWidth = 0.002f;

        [Header("Posiciones UI")]
        public Vector3 loaderOffset = new Vector3(0, -0.25f, 0);
        public Vector3 uiOffset = new Vector3(0, -0.6f, 0);

        [Header("Referencias de Escena")]
        public Zone3Manager linkedZone3Manager;

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
                Debug.Log($"[ANDROID LOAD] Intentando leer: {filePath}");
                using (UnityWebRequest www = UnityWebRequest.Get(filePath))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        jsonContent = www.downloadHandler.text;
                    }
                    else
                    {
                        Debug.LogError("Error JSON Android: " + www.error);
                        yield break;
                    }
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
                catch (System.Exception e) { Debug.LogError("Error JSON: " + e.Message); }
            }
        }

        void GenerateH3Layout(NodeData rootData)
        {
            // Limpiamos la base de datos antes de regenerar
            nodeDatabase.Clear();
            // Función recursiva auxiliar para registrar todos los nodos (padres e hijos)
            RegisterNodeToDatabase(rootData);

            foreach (Transform child in transform) Destroy(child.gameObject);

            GameObject rootObj = CreateNodeObject(rootPrefab, transform, new Vector3(0, 0.2f, 0), rootData, "root", null, null, Color.white);

            if (rootData.children == null) return;

            int commCount = rootData.children.Count;
            Vector3[] commPositions = CalculateFibonacciSphere(commCount, communityOrbitRadius);

            for (int i = 0; i < commCount; i++)
            {
                NodeData commData = rootData.children[i];
                Color groupColor = Color.HSVToRGB((float)i / commCount, 0.7f, 0.9f);
                GameObject lineToComm = CreateLine(rootObj.transform.position, rootObj.transform);
                GameObject commObj = CreateNodeObject(communityPrefab, rootObj.transform, commPositions[i], commData, "community", rootObj.transform, lineToComm.GetComponent<LineRenderer>(), groupColor);
                GraphNode commLogic = commObj.GetComponent<GraphNode>();

                if (commData.children != null)
                {
                    int fileCount = commData.children.Count;
                    Vector3[] filePositions = CalculateFibonacciSphere(fileCount, fileOrbitRadius);
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
        }

        // --- NUEVA FUNCIÓN RECURSIVA ---
        void RegisterNodeToDatabase(NodeData node)
        {
            if (node == null) return;

            // Registramos este nodo por su ID
            if (!string.IsNullOrEmpty(node.id) && !nodeDatabase.ContainsKey(node.id))
            {
                nodeDatabase.Add(node.id, node);
            }

            // Buscamos en sus hijos
            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    RegisterNodeToDatabase(child);
                }
            }
        }

        // --- NUEVA FUNCIÓN PÚBLICA PARA CONSULTAR ---
        public NodeData GetNodeDataByID(string id)
        {
            if (nodeDatabase.ContainsKey(id))
            {
                return nodeDatabase[id];
            }
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

            // --- PASAR CONFIGURACIÓN DE CHINCHETA AL NODO ---
            logic.reviewedMarkerPrefab = reviewedMarkerPrefab;
            logic.markerLocalOffset = markerOffset;
            logic.markerLocalScale = markerScale;
            // ------------------------------------------------

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
                uiObj.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
                NodeUIController uiCtrl = uiObj.GetComponent<NodeUIController>();
                if (uiCtrl != null) uiCtrl.SetupUI(data.title, string.IsNullOrEmpty(data.summary) ? "Sin descripción" : data.summary);
                logic.infoUI = uiCtrl;
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
            lr.startWidth = lineWidth; lr.endWidth = lineWidth; lr.positionCount = 2;
            lr.SetPosition(0, start); lr.SetPosition(1, start); lr.useWorldSpace = true;
            return lineObj;
        }

        Vector3[] CalculateFibonacciSphere(int samples, float radius)
        {
            if (samples <= 0) return new Vector3[0];
            if (samples == 1) return new Vector3[] { Vector3.up * radius };
            Vector3[] points = new Vector3[samples];
            float phi = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < samples; i++)
            {
                float div = (float)(samples - 1); float y = 1 - (i / div) * 2;
                float radiusAtY = Mathf.Sqrt(1 - y * y); float theta = phi * i;
                float x = Mathf.Cos(theta) * radiusAtY; float z = Mathf.Sin(theta) * radiusAtY;
                points[i] = new Vector3(x * radius, y * radius, z * radius);
            }
            return points;
        }
    }
}