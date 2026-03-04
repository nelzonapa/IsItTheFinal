using UnityEngine;
using System.Collections.Generic;
using ImmersiveGraph.Network;

namespace ImmersiveGraph.Collaboration
{
    public class SharedWorkspaceTracker : MonoBehaviour
    {
        public static SharedWorkspaceTracker Instance { get; private set; }

        [Header("Configuración de Detección")]
        public float collaborativeRadius = 15f;
        public float updateRateHz = 5f;

        public struct TrackedNode
        {
            public string id;
            public UIDashboardElement.ElementType type;
            public Vector3 position;
            public Color color;
            public string originDocumentId;
            public string textContent;
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

        public bool IsOccupied { get; private set; }
        public string OccupantsNames { get; private set; }

        private float _timer = 0f;

        // ==============================================================
        // --- OPTIMIZACIÓN: REGISTRO ESTÁTICO DE ENTIDADES EN RED ---
        // ==============================================================
        public static readonly HashSet<NetworkTokenSync> RegisteredTokens = new HashSet<NetworkTokenSync>();
        public static readonly HashSet<NetworkPostItSync> RegisteredPostIts = new HashSet<NetworkPostItSync>();
        public static readonly HashSet<NetworkConnectionLine> RegisteredLines = new HashSet<NetworkConnectionLine>();
        public static readonly HashSet<HardwareRigSync> RegisteredAvatars = new HashSet<HardwareRigSync>();

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

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            bool hasObjects = false;

            // 1. ESCANEO DE TOKENS (Cero GetComponent, Cero FindObjectsByType)
            foreach (var token in RegisteredTokens)
            {
                if (token == null) continue;
                if (Vector3.Distance(token.transform.position, transform.position) > collaborativeRadius) continue;

                hasObjects = true;
                UpdateBounds(token.transform.position, ref minX, ref maxX, ref minZ, ref maxZ);

                Color objColor = ExtractSafeColor(token.GetComponent<Renderer>());

                ActiveNodes.Add(new TrackedNode
                {
                    id = token.Id.ToString(),
                    type = UIDashboardElement.ElementType.Token,
                    position = token.transform.position,
                    color = objColor,
                    originDocumentId = token.SourceNodeID.ToString(),
                    textContent = token.TokenLabel.ToString()
                });
            }

            // 2. ESCANEO DE POST-ITS
            foreach (var postIt in RegisteredPostIts)
            {
                if (postIt == null) continue;
                if (Vector3.Distance(postIt.transform.position, transform.position) > collaborativeRadius) continue;

                hasObjects = true;
                UpdateBounds(postIt.transform.position, ref minX, ref maxX, ref minZ, ref maxZ);

                Color objColor = ExtractSafeColor(postIt.GetComponent<Renderer>());

                ActiveNodes.Add(new TrackedNode
                {
                    id = postIt.Id.ToString(),
                    type = UIDashboardElement.ElementType.PostIt,
                    position = postIt.transform.position,
                    color = objColor,
                    originDocumentId = "",
                    textContent = postIt.NetworkContent.ToString()
                });
            }

            // 3. ESCANEO DE LÍNEAS
            foreach (var line in RegisteredLines)
            {
                if (line == null) continue;
                if (line.StartNodeID.IsValid && line.EndNodeID.IsValid)
                {
                    ActiveLines.Add(new TrackedLine
                    {
                        id = line.Id.ToString(),
                        startNodeId = line.StartNodeID.ToString(),
                        endNodeId = line.EndNodeID.ToString()
                    });
                }
            }

            // 4. CALCULAR BOUNDING BOX
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

            // 5. ESCANEO DE PRESENCIA
            IsOccupied = false;
            List<string> occupantList = new List<string>();

            foreach (var avatar in RegisteredAvatars)
            {
                if (avatar == null) continue;

                if (Vector3.Distance(avatar.HeadPos, transform.position) <= collaborativeRadius)
                {
                    IsOccupied = true;
                    int pId = avatar.Object != null && avatar.Object.IsValid ? avatar.Object.InputAuthority.PlayerId : -1;
                    occupantList.Add("Usuario " + pId);
                }
            }
            OccupantsNames = string.Join(", ", occupantList);
        }

        // --- Funciones Auxiliares de Optimización ---
        private void UpdateBounds(Vector3 pos, ref float minX, ref float maxX, ref float minZ, ref float maxZ)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.z < minZ) minZ = pos.z;
            if (pos.z > maxZ) maxZ = pos.z;
        }

        private Color ExtractSafeColor(Renderer r)
        {
            // Usar sharedMaterial evita crear instancias de memoria basura (Garbage Collection)
            if (r != null && r.sharedMaterial != null) return r.sharedMaterial.color;
            return Color.white;
        }
    }
}