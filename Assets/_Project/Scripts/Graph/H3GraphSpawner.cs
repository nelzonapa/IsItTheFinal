using UnityEngine;
using System.IO;
using ImmersiveGraph.Data;
using ImmersiveGraph.Interaction;
using ImmersiveGraph.Visual;
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
                try
                {
                    NodeData rootNode = JsonUtility.FromJson<NodeData>(File.ReadAllText(filePath));
                    if (rootNode != null) GenerateH3Layout(rootNode);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Error JSON: " + e.Message);
                }
            }
        }

        void GenerateH3Layout(NodeData rootData)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);

            // 1. ROOT (Le pasamos color blanco por defecto)
            GameObject rootObj = CreateNodeObject(rootPrefab, transform, new Vector3(0, 0.2f, 0), rootData, "root", null, null, Color.white);

            if (rootData.children == null) return;

            // 2. COMUNIDADES
            int commCount = rootData.children.Count;
            Vector3[] commPositions = CalculateFibonacciSphere(commCount, communityOrbitRadius);

            for (int i = 0; i < commCount; i++)
            {
                NodeData commData = rootData.children[i];

                // Calcular el color de este grupo
                Color groupColor = Color.HSVToRGB((float)i / commCount, 0.7f, 0.9f);

                GameObject lineToComm = CreateLine(rootObj.transform.position, rootObj.transform);

                // PASAMOS EL COLOR AQUÍ
                GameObject commObj = CreateNodeObject(communityPrefab, rootObj.transform, commPositions[i], commData, "community", rootObj.transform, lineToComm.GetComponent<LineRenderer>(), groupColor);

                GraphNode commLogic = commObj.GetComponent<GraphNode>();

                // 3. ARCHIVOS
                if (commData.children != null)
                {
                    int fileCount = commData.children.Count;
                    Vector3[] filePositions = CalculateFibonacciSphere(fileCount, fileOrbitRadius);

                    for (int j = 0; j < fileCount; j++)
                    {
                        NodeData fileData = commData.children[j];

                        GameObject lineToFile = CreateLine(commObj.transform.position, commObj.transform);

                        // PASAMOS EL MISMO COLOR DE GRUPO AQUÍ
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

        // --- FUNCIÓN MODIFICADA: Ahora recibe 'Color nodeColor' ---
        GameObject CreateNodeObject(GameObject prefab, Transform parent, Vector3 localPos, NodeData data, string type, Transform parentNode, LineRenderer incomingLine, Color nodeColor)
        {
            GameObject obj = Instantiate(prefab, parent);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = prefab.transform.localScale;
            obj.name = $"{type.ToUpper()}_{data.title}";

            // 1. APLICAR COLOR INMEDIATAMENTE (Antes de agregar scripts)
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = nodeColor;
            }

            // 2. AGREGAR LÓGICA (Ahora InitializeNode leerá el color correcto)
            GraphNode logic = obj.AddComponent<GraphNode>();
            logic.nodeType = type;
            logic.myData = data;

            logic.InitializeNode(parentNode, incomingLine);

            // UI
            if (nodeUIPrefab != null)
            {
                GameObject uiObj = Instantiate(nodeUIPrefab, obj.transform);
                uiObj.transform.localPosition = uiOffset;
                uiObj.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);

                NodeUIController uiCtrl = uiObj.GetComponent<NodeUIController>();
                if (uiCtrl != null)
                {
                    string summary = string.IsNullOrEmpty(data.summary) ? "Sin descripción" : data.summary;
                    uiCtrl.SetupUI(data.title, summary);
                    logic.uiController = uiCtrl;
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
    }
}