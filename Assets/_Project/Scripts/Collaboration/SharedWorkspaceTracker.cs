using UnityEngine;
using System.Collections.Generic;
using ImmersiveGraph.Network;
using Fusion; // Necesario para NetworkObject

namespace ImmersiveGraph.Collaboration
{
    public class SharedWorkspaceTracker : MonoBehaviour
    {
        public static SharedWorkspaceTracker Instance { get; private set; }

        [Header("Configuración de Detección")]
        public float collaborativeRadius = 15f;
        public float updateRateHz = 5f;

        // Estructuras de datos relacionales
        public struct TrackedNode
        {
            public string id;
            public UIDashboardElement.ElementType type;
            public Vector3 position;
            public Color color;
            public string originDocumentId;
            public string textContent; // texto
        }

        public struct TrackedLine
        {
            public string id;
            public string startNodeId;
            public string endNodeId;
        }

        public List<TrackedNode> ActiveNodes { get; private set; } = new List<TrackedNode>();
        public List<TrackedLine> ActiveLines { get; private set; } = new List<TrackedLine>();

        public Vector2 BoundingBoxCenter { get; private set; }
        public Vector2 BoundingBoxSize { get; private set; }

        private float _timer = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= (1f / updateRateHz))
            {
                _timer = 0f;
                CalculateWorkspaceData();
            }
        }

        private void CalculateWorkspaceData()
        {
            ActiveNodes.Clear();
            ActiveLines.Clear();

            // 1. ESCANEO DE NODOS (Tokens y PostIts)
            var allNetworkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            bool hasObjects = false;

            foreach (var netObj in allNetworkObjects)
            {
                // Ignorar objetos fuera del radio
                if (Vector3.Distance(netObj.transform.position, transform.position) > collaborativeRadius) continue;

                // Determinar el tipo de objeto
                var tokenSync = netObj.GetComponent<NetworkTokenSync>();
                var postItSync = netObj.GetComponent<NetworkPostItSync>();

                if (tokenSync != null || postItSync != null)
                {
                    hasObjects = true;
                    Vector3 pos = netObj.transform.position;

                    // Expandir Bounding Box
                    if (pos.x < minX) minX = pos.x;
                    if (pos.x > maxX) maxX = pos.x;
                    if (pos.z < minZ) minZ = pos.z;
                    if (pos.z > maxZ) maxZ = pos.z;

                    // Extraer Color
                    Color objColor = Color.white;
                    var r = netObj.GetComponent<Renderer>();
                    if (r != null && r.material != null) objColor = r.material.color;

                    // Extraer Texto
                    string extractedText = "";
                    if (tokenSync != null) extractedText = tokenSync.TokenLabel.ToString();
                    else if (postItSync != null) extractedText = postItSync.NetworkContent.ToString();

                    // Construir el Nodo
                    ActiveNodes.Add(new TrackedNode
                    {
                        id = netObj.Id.ToString(),
                        type = tokenSync != null ? UIDashboardElement.ElementType.Token : UIDashboardElement.ElementType.PostIt,
                        position = pos,
                        color = objColor,
                        originDocumentId = tokenSync != null ? tokenSync.SourceNodeID.ToString() : "",
                        textContent = extractedText // <--- ¡AQUÍ ESTABA EL ERROR! Faltaba esta línea para guardar el texto
                    });
                }
            }

            // 2. ESCANEO DE LÍNEAS
            var allLines = FindObjectsByType<NetworkConnectionLine>(FindObjectsSortMode.None);
            foreach (var line in allLines)
            {
                if (line.StartNodeID.IsValid && line.EndNodeID.IsValid)
                {
                    ActiveLines.Add(new TrackedLine
                    {
                        id = line.GetComponent<NetworkObject>().Id.ToString(),
                        startNodeId = line.StartNodeID.ToString(),
                        endNodeId = line.EndNodeID.ToString()
                    });
                }
            }

            // 3. CALCULAR BOUNDING BOX
            if (hasObjects)
            {
                BoundingBoxCenter = new Vector2((minX + maxX) / 2f, (minZ + maxZ) / 2f);
                BoundingBoxSize = new Vector2(Mathf.Max(maxX - minX, 2f), Mathf.Max(maxZ - minZ, 2f));
            }
            else
            {
                BoundingBoxCenter = new Vector2(transform.position.x, transform.position.z);
                BoundingBoxSize = new Vector2(2f, 2f);
            }
        }
    }
}