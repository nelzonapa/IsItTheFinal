using ImmersiveGraph.Core;
using ImmersiveGraph.Data;
using ImmersiveGraph.Interaction;
using ImmersiveGraph.Visual;
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
        public GraphInteractionManager interactionManager; // Arrastra aquí el script si ya existe

        public static H3GraphSpawner Instance; // Singleton para acceso global

        // --- BASE DE DATOS EN MEMORIA ---
        public Dictionary<string, NodeData> nodeDatabase = new Dictionary<string, NodeData>();

        [Header("Configuración de Archivo")]
        public string jsonFileName = "hierarchy_complete.json";

        [Header("Prefabs de Nodos")]
        public GameObject rootPrefab;      // Prefab para la Raíz
        public GameObject communityPrefab; // Prefab para Comunidades (Nivel 1)
        public GameObject filePrefab;      // Prefab para Archivos (Nivel 2)

        [Header("Prefabs UI & Feedback")]
        public GameObject nodeUIPrefab;
        public GameObject loadingBarPrefab;
        public GameObject reviewedMarkerPrefab; // Cubo/Chincheta de "Visto"

        [Header("Configuración Visual Chincheta")]
        public Vector3 markerOffset = new Vector3(0, 0.25f, 0);
        public Vector3 markerScale = new Vector3(0.2f, 0.2f, 0.2f);

        [Header("Layout (Distancias)")]
        [Tooltip("Radio de la órbita de las Comunidades alrededor de la Raíz")]
        public float communityOrbitRadius = 0.6f; // Aumentado un poco para dar espacio
        [Tooltip("Radio de la órbita de los Archivos alrededor de su Comunidad")]
        public float fileOrbitRadius = 0.25f;     // Aumentado para que no se amontonen

        [Header("Estilo de Líneas")]
        public Material lineMaterial;
        public float lineWidth = 0.002f;

        [Header("Posiciones UI (Relativo al nodo)")]
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
            // Construye la ruta al archivo JSON en StreamingAssets
            string filePath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
            string jsonContent = "";

            // --- LECTURA DEL ARCHIVO (Soporte Android/PC) ---
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
                        Debug.LogError("Error cargando JSON en Android: " + www.error);
                        yield break;
                    }
                }
            }
            else
            {
                if (File.Exists(filePath)) jsonContent = File.ReadAllText(filePath);
                else
                {
                    Debug.LogError($"No se encontró el archivo: {filePath}");
                    yield break;
                }
            }

            // --- PROCESAMIENTO ---
            if (!string.IsNullOrEmpty(jsonContent))
            {
                try
                {
                    // Deserializamos el JSON a objetos C#
                    NodeData rootNode = JsonUtility.FromJson<NodeData>(jsonContent);
                    if (rootNode != null)
                    {
                        GenerateH3Layout(rootNode);
                    }
                }
                catch (System.Exception e) { Debug.LogError("Error parseando JSON: " + e.Message); }
            }
        }

        // --- CORAZÓN DEL LAYOUT ---
        void GenerateH3Layout(NodeData rootData)
        {
            // 1. Limpieza
            nodeDatabase.Clear();
            RegisterNodeToDatabase(rootData); // Llenamos el diccionario para búsquedas rápidas

            // Borramos los objetos viejos si los hubiera
            foreach (Transform child in transform) Destroy(child.gameObject);

            // 2. Crear Nodo RAÍZ
            // Lo ponemos un poco elevado (0.2 en Y) para que flote sobre la mesa
            GameObject rootObj = CreateNodeObject(rootPrefab, transform, new Vector3(0, 0.3f, 0), rootData, "root", null, null, Color.white);

            if (rootData.children == null) return;

            // ---------------------------------------------------------
            // NIVEL 1: COMUNIDADES (Alrededor de la Raíz)
            // Usamos Esfera Completa (Fibonacci Sphere)
            // ---------------------------------------------------------
            int commCount = rootData.children.Count;

            // Llamamos a nuestra calculadora matemática
            Vector3[] commPositions = HyperbolicMath.GetFibonacciSphere(commCount, communityOrbitRadius);

            for (int i = 0; i < commCount; i++)
            {
                NodeData commData = rootData.children[i];

                // Color único por comunidad
                Color groupColor = Color.HSVToRGB((float)i / commCount, 0.7f, 0.9f);

                // Línea desde Raíz -> Comunidad
                GameObject lineToComm = CreateLine(rootObj.transform.position, rootObj.transform);

                // Crear Objeto Comunidad
                // Nota: commPositions[i] es relativo al padre (la raíz)
                GameObject commObj = CreateNodeObject(communityPrefab, rootObj.transform, commPositions[i], commData, "community", rootObj.transform, lineToComm.GetComponent<LineRenderer>(), groupColor);

                GraphNode commLogic = commObj.GetComponent<GraphNode>();

                // ---------------------------------------------------------
                // NIVEL 2: ARCHIVOS (Alrededor de la Comunidad)
                // Usamos HEMISFERIO ORIENTADO (Oriented Hemisphere)
                // ---------------------------------------------------------
                if (commData.children != null)
                {
                    int fileCount = commData.children.Count;

                    // Calculamos la dirección "Hacia afuera": Desde la Raíz hacia la Comunidad
                    // Esto asegura que los archivos broten alejándose del centro
                    Vector3 directionOut = commObj.transform.localPosition.normalized; // Como el padre es la raíz (0,0,0 local), la posición local ES el vector dirección.

                    // Llamamos a la calculadora matemática NUEVA
                    Vector3[] filePositions = HyperbolicMath.GetOrientedHemisphere(fileCount, fileOrbitRadius, directionOut);

                    for (int j = 0; j < fileCount; j++)
                    {
                        NodeData fileData = commData.children[j];

                        // Línea desde Comunidad -> Archivo
                        GameObject lineToFile = CreateLine(commObj.transform.position, commObj.transform);

                        // Crear Objeto Archivo
                        // Nota: filePositions[j] es relativo al padre (la comunidad)
                        GameObject fileObj = CreateNodeObject(filePrefab, commObj.transform, filePositions[j], fileData, "file", commObj.transform, lineToFile.GetComponent<LineRenderer>(), groupColor);

                        // Registramos en la lógica del padre para que sepa quiénes son sus hijos
                        if (commLogic != null)
                        {
                            commLogic.childNodes.Add(fileObj);
                            commLogic.childConnectionLines.Add(lineToFile);
                        }
                    }
                }

                // Inicializamos la lógica de la comunidad
                if (commLogic != null) commLogic.InitializeNode(rootObj.transform, lineToComm.GetComponent<LineRenderer>());

                // Inicializar el Manager con el nuevo Root
                if (interactionManager != null)
                {
                    interactionManager.InitializeGraph(rootObj.transform);
                }
            }
        }

        // --- REGISTRO DE DATOS ---
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

        // --- FACTORÍA DE OBJETOS (Instanciación) ---
        GameObject CreateNodeObject(GameObject prefab, Transform parent, Vector3 localPos, NodeData data, string type, Transform parentNode, LineRenderer incomingLine, Color nodeColor)
        {
            GameObject obj = Instantiate(prefab, parent);

            // Asignamos posición local
            obj.transform.localPosition = localPos;

            // Respetamos la escala del prefab
            obj.transform.localScale = prefab.transform.localScale;

            obj.name = $"{type.ToUpper()}_{data.title}";

            // Color
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = nodeColor;

            // Lógica Interactiva (GraphNode)
            GraphNode logic = obj.AddComponent<GraphNode>();
            logic.nodeType = type;
            logic.myData = data;
            logic.localZone3Manager = linkedZone3Manager;

            // --- NUEVO: ASIGNAR EL MANAGER ---
            logic.interactionManager = this.interactionManager;
            // ---------------------------------

            logic.expandSound = nodeExpandSound;

            // Chincheta
            logic.reviewedMarkerPrefab = reviewedMarkerPrefab;
            logic.markerLocalOffset = markerOffset;
            logic.markerLocalScale = markerScale;

            // Inicializar lógica base
            logic.InitializeNode(parentNode, incomingLine);

            // UI: Barra de carga
            if (loadingBarPrefab != null)
            {
                GameObject loadObj = Instantiate(loadingBarPrefab, obj.transform);
                loadObj.transform.localPosition = loaderOffset;
                loadObj.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                logic.loaderUI = loadObj.GetComponent<NodeLoaderController>();
            }

            // UI: Panel de texto flotante
            /*
            if (nodeUIPrefab != null)
            {
                GameObject uiObj = Instantiate(nodeUIPrefab, obj.transform);
                uiObj.transform.localPosition = uiOffset;
                uiObj.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
                NodeUIController uiCtrl = uiObj.GetComponent<NodeUIController>();

                string desc = string.IsNullOrEmpty(data.summary) ? "Sin descripción" : data.summary;
                if (uiCtrl != null) uiCtrl.SetupUI(data.title, desc);

                logic.infoUI = uiCtrl;
            }

            */
            return obj;
        }

        GameObject CreateLine(Vector3 start, Transform parent)
        {
            GameObject lineObj = new GameObject("Link");
            lineObj.transform.SetParent(parent);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();

            // Material por defecto si falta
            if (lineMaterial != null) lr.material = lineMaterial;
            else lr.material = new Material(Shader.Find("Sprites/Default"));

            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.positionCount = 2;

            // Puntos iniciales (se actualizarán en el Update de GraphNode)
            lr.SetPosition(0, start);
            lr.SetPosition(1, start);

            lr.useWorldSpace = true; // Importante para que las líneas sigan a los objetos
            return lineObj;
        }
    }
}