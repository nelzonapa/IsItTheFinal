using UnityEngine;
using System.IO;
using ImmersiveGraph.Data;
using ImmersiveGraph.Interaction;
using System.Collections.Generic;

namespace ImmersiveGraph.Visual
{
    public class H3GraphSpawner : MonoBehaviour
    {
        [Header("Configuración")]
        public string jsonFileName = "hierarchy_complete.json";
        public GameObject rootPrefab;
        public GameObject communityPrefab;
        public GameObject filePrefab;
        public GameObject nodeUIPrefab;

        [Header("Layout")]
        public float communityOrbitRadius = 0.4f;
        public float fileOrbitRadius = 0.15f;
        public Material lineMaterial;
        public float lineWidth = 0.002f;

        public Vector3 uiOffset = new Vector3(0, -0.5f, 0);

        void Start()
        {
            LoadGraph();
        }

        void LoadGraph()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
            if (File.Exists(filePath))
            {
                // Usamos un try-catch por si el JSON está mal formado
                try
                {
                    NodeData rootNode = JsonUtility.FromJson<NodeData>(File.ReadAllText(filePath));
                    if (rootNode != null) GenerateH3Layout(rootNode);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Error al leer JSON: " + e.Message);
                }
            }
        }

        void GenerateH3Layout(NodeData rootData)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);

            // 1. ROOT
            GameObject rootObj = CreateNodeObject(rootPrefab, transform, new Vector3(0, 0.2f, 0), rootData, "root");

            if (rootData.children == null) return;

            // 2. COMUNIDADES
            int commCount = rootData.children.Count;
            Vector3[] commPositions = CalculateFibonacciSphere(commCount, communityOrbitRadius);

            for (int i = 0; i < commCount; i++)
            {
                NodeData commData = rootData.children[i];
                GameObject commObj = CreateNodeObject(communityPrefab, rootObj.transform, commPositions[i], commData, "community");

                DrawConnection(rootObj.transform.position, commObj.transform.position, commObj.transform);

                Color groupColor = Color.HSVToRGB((float)i / commCount, 0.7f, 0.9f);
                commObj.GetComponent<Renderer>().material.color = groupColor;

                GraphNode commLogic = commObj.GetComponent<GraphNode>();

                // 3. ARCHIVOS
                if (commData.children != null)
                {
                    int fileCount = commData.children.Count;
                    Vector3[] filePositions = CalculateFibonacciSphere(fileCount, fileOrbitRadius);

                    for (int j = 0; j < fileCount; j++)
                    {
                        NodeData fileData = commData.children[j];
                        GameObject fileObj = CreateNodeObject(filePrefab, commObj.transform, filePositions[j], fileData, "file");

                        fileObj.GetComponent<Renderer>().material.color = groupColor;

                        GameObject line = DrawConnection(commObj.transform.position, fileObj.transform.position, fileObj.transform);

                        // Añadir a la lista del padre
                        if (commLogic != null)
                        {
                            commLogic.childNodes.Add(fileObj);
                            commLogic.connectionLines.Add(line);
                        }
                    }
                }

                // --- IMPORTANTE: Inicializar la comunidad AHORA que ya tiene los hijos ---
                if (commLogic != null)
                {
                    commLogic.InitializeNode();
                }
            }
        }

        GameObject CreateNodeObject(GameObject prefab, Transform parent, Vector3 localPos, NodeData data, string type)
        {
            GameObject obj = Instantiate(prefab, parent);
            obj.transform.localPosition = localPos;
            obj.name = $"{type.ToUpper()}_{data.title}";

            GraphNode logic = obj.AddComponent<GraphNode>();
            logic.nodeType = type;
            logic.myData = data;

            // B. Instanciar UI Flotante con CORRECCIÓN DE ESCALA
            if (nodeUIPrefab != null)
            {
                GameObject uiObj = Instantiate(nodeUIPrefab, obj.transform);
                uiObj.transform.localPosition = uiOffset;

                // CORRECCIÓN DE ESCALA:
                // Ignoramos la escala del padre y ponemos una fija pequeña para que se vea bien.
                // 0.003 es un valor estándar para Canvas WorldSpace legibles a 1 metro.
                // Como es hijo de una esfera (que quizás mide 0.1), hay que compensar.
                // Probamos con 0.02f local (que multiplicado por 0.1 del padre da 0.002 mundial).
                uiObj.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);

                NodeUIController uiCtrl = uiObj.GetComponent<NodeUIController>();
                if (uiCtrl != null)
                {
                    string summary = string.IsNullOrEmpty(data.summary) ? "Sin descripción" : data.summary;
                    uiCtrl.SetupUI(data.title, summary);
                }
            }

            return obj;
        }

        Vector3[] CalculateFibonacciSphere(int samples, float radius)
        {
            if (samples <= 0) return new Vector3[0];
            if (samples == 1) return new Vector3[] { Vector3.up * radius };

            Vector3[] points = new Vector3[samples];
            float phi = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (int i = 0; i < samples; i++)
            {
                float div = (float)(samples - 1);
                float y = 1 - (i / div) * 2;
                float radiusAtY = Mathf.Sqrt(1 - y * y);
                float theta = phi * i;
                float x = Mathf.Cos(theta) * radiusAtY;
                float z = Mathf.Sin(theta) * radiusAtY;
                points[i] = new Vector3(x * radius, y * radius, z * radius);
            }
            return points;
        }

        GameObject DrawConnection(Vector3 start, Vector3 end, Transform parent)
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
            lr.SetPosition(1, end);
            lr.useWorldSpace = true;
            return lineObj;
        }
    }
}