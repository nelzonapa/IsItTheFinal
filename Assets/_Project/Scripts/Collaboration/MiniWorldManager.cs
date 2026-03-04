using UnityEngine;
using System.Collections.Generic;
using ImmersiveGraph.Interaction;
using ImmersiveGraph.Network;
using ImmersiveGraph.Core;

namespace ImmersiveGraph.Collaboration
{
    public class MiniWorldManager : MonoBehaviour
    {
        [Header("Configuración del Espacio")]
        public Transform miniWorldRoot;
        public float scaleFactor = 0.05f;

        [Header("Controles del Mini-Avatar")]
        public float avatarDistanceMultiplier = 1.5f;

        [Header("Detección de Mirada (Área del Grafo)")]
        public float graphDetectionRadius = 1.5f;

        [Header("Visualización del Cono 3D")]
        public float gazeConeMaxLength = 6.0f;
        public float gazeConeBaseRadius = 1.0f;
        public bool usePlayerColorForCone = true;
        public Color customConeColor = Color.yellow;
        [Range(0f, 1f)] public float gazeConeAlpha = 0.3f;

        [Header("Estilo Visual Base")]
        public Material hologramMaterial;
        public float miniLineWidth = 0.02f;

        public Dictionary<string, GameObject> miniNodesMap = new Dictionary<string, GameObject>();
        private Dictionary<string, Color> originalColorsMap = new Dictionary<string, Color>();

        private Transform _realGraphRoot;
        private Vector3 _realGraphCenter;
        private bool _isBuilt = false;

        private class MiniAvatarData
        {
            public GameObject root;
            public Transform head;
            public MeshRenderer gazeConeRenderer;
            public Transform gazeConeTransform;

            public string currentHighlightedNodeId = "";
        }

        private Dictionary<int, MiniAvatarData> _miniAvatars = new Dictionary<int, MiniAvatarData>();

        public void BuildMiniatureFromRealGraph(Transform realRoot, List<GraphNode> realCommunities)
        {
            if (miniWorldRoot == null) return;

            _realGraphRoot = realRoot;
            _realGraphCenter = realRoot.position;

            foreach (Transform child in miniWorldRoot) Destroy(child.gameObject);
            miniNodesMap.Clear();
            originalColorsMap.Clear();
            _miniAvatars.Clear();

            GraphNode rootLogic = realRoot.GetComponent<GraphNode>();
            if (rootLogic == null) return;

            // OPTIMIZACIÓN: sharedMaterial
            Color rColor = Color.white;
            Renderer rootRenderer = realRoot.GetComponent<Renderer>();
            if (rootRenderer != null && rootRenderer.sharedMaterial != null)
                rColor = rootRenderer.sharedMaterial.color;

            GameObject miniRoot = CreateMiniNode(rootLogic.myData.id, Vector3.zero, rColor, 1.5f);

            miniNodesMap.Add(rootLogic.myData.id, miniRoot);
            originalColorsMap.Add(rootLogic.myData.id, rColor);

            foreach (GraphNode comm in realCommunities)
            {
                // OPTIMIZACIÓN: sharedMaterial
                Color cColor = Color.cyan;
                Renderer commRenderer = comm.GetComponent<Renderer>();
                if (commRenderer != null && commRenderer.sharedMaterial != null)
                    cColor = commRenderer.sharedMaterial.color;

                GameObject miniComm = CreateMiniNode(comm.myData.id, comm.transform.localPosition, cColor, 1.0f);

                miniNodesMap.Add(comm.myData.id, miniComm);
                originalColorsMap.Add(comm.myData.id, cColor);

                DrawMiniLine(miniComm.transform);
            }

            miniWorldRoot.localScale = Vector3.one * scaleFactor;
            _isBuilt = true;
        }

        private GameObject CreateMiniNode(string id, Vector3 localPos, Color color, float sizeMult)
        {
            GameObject mini = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mini.name = $"Mini_{id}";
            mini.transform.SetParent(miniWorldRoot);
            mini.transform.localPosition = localPos;
            mini.transform.localScale = Vector3.one * sizeMult;
            Destroy(mini.GetComponent<Collider>());

            Renderer r = mini.GetComponent<Renderer>();
            if (hologramMaterial != null) r.sharedMaterial = hologramMaterial; // OPTIMIZACIÓN
            r.material.color = color; // Mantenemos .material aquí porque sí necesitamos instanciar este color único

            return mini;
        }

        private void DrawMiniLine(Transform childComm)
        {
            GameObject lineObj = new GameObject("MiniLink");
            lineObj.transform.SetParent(miniWorldRoot);
            lineObj.transform.localPosition = Vector3.zero;
            lineObj.transform.localRotation = Quaternion.identity;
            lineObj.transform.localScale = Vector3.one;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            if (hologramMaterial != null) lr.sharedMaterial = hologramMaterial; // OPTIMIZACIÓN
            else lr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            lr.startWidth = miniLineWidth;
            lr.endWidth = miniLineWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = false;

            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, childComm.localPosition);
        }

        void Update()
        {
            if (!_isBuilt || miniWorldRoot == null || HardwareRigSync.Local == null) return;

            // =========================================================
            // OPTIMIZACIÓN MÁXIMA: Usar el Registro en vez de FindObjects
            // =========================================================
            foreach (var p in SharedWorkspaceTracker.RegisteredAvatars)
            {
                if (p == null || p.Object == null || !p.Object.IsValid) continue;

                int pId = p.Object.StateAuthority.PlayerId;

                if (!_miniAvatars.ContainsKey(pId)) CreateMiniAvatar(pId);

                UpdateMiniAvatar(pId, p);
            }
        }

        private void CreateMiniAvatar(int playerId)
        {
            Color playerColor = UserColorPalette.GetColor(playerId);

            GameObject avatarRoot = new GameObject($"MiniAvatar_P{playerId}");
            avatarRoot.transform.SetParent(miniWorldRoot);
            avatarRoot.transform.localScale = Vector3.one;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(avatarRoot.transform);
            head.transform.localPosition = Vector3.zero;
            head.transform.localScale = Vector3.one * 2.5f;
            Destroy(head.GetComponent<Collider>());

            Renderer headRenderer = head.GetComponent<Renderer>();
            if (hologramMaterial != null) headRenderer.sharedMaterial = hologramMaterial; // OPTIMIZACIÓN
            headRenderer.material.color = playerColor;

            GameObject gazeObj = new GameObject("GazeCone3D");
            gazeObj.transform.SetParent(head.transform);
            gazeObj.transform.localPosition = Vector3.zero;
            gazeObj.transform.localRotation = Quaternion.identity;

            MeshFilter meshFilter = gazeObj.AddComponent<MeshFilter>();
            meshFilter.mesh = GenerateConeMesh(16);

            MeshRenderer coneRenderer = gazeObj.AddComponent<MeshRenderer>();
            if (hologramMaterial != null) coneRenderer.material = new Material(hologramMaterial);
            else coneRenderer.material = new Material(Shader.Find("Standard"));

            Color coneColor = usePlayerColorForCone ? playerColor : customConeColor;
            coneColor.a = gazeConeAlpha;
            coneRenderer.material.color = coneColor;

            gazeObj.transform.localScale = new Vector3(gazeConeBaseRadius, gazeConeBaseRadius, gazeConeMaxLength);

            _miniAvatars.Add(playerId, new MiniAvatarData
            {
                root = avatarRoot,
                head = head.transform,
                gazeConeRenderer = coneRenderer,
                gazeConeTransform = gazeObj.transform,
                currentHighlightedNodeId = ""
            });
        }

        private Mesh GenerateConeMesh(int segments)
        {
            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            vertices[segments + 1] = new Vector3(0, 0, 1f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Sin(angle);
                float y = Mathf.Cos(angle);
                vertices[i + 1] = new Vector3(x, y, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1 == segments) ? 1 : i + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        private void UpdateMiniAvatar(int playerId, HardwareRigSync syncData)
        {
            if (!_miniAvatars.TryGetValue(playerId, out MiniAvatarData avatar)) return;

            // OPTIMIZACIÓN: Evitar llamadas a InverseTransformPoint usando matemática vectorial directa donde sea posible.
            Transform myLocalRig = HardwareRigSync.Local.transform;

            Vector3 myGraphLocalOffset = myLocalRig.InverseTransformPoint(_realGraphCenter);

            // Calculamos dónde está la cabeza del OTRO jugador respecto a SU mesa
            Vector3 theirHeadLocalPos = syncData.transform.InverseTransformPoint(syncData.HeadPos);

            // Posición relativa final para el minimundo
            Vector3 relativePos = theirHeadLocalPos - myGraphLocalOffset;
            avatar.root.transform.localPosition = relativePos * avatarDistanceMultiplier;

            // Rotación relativa a su propia mesa
            Quaternion theirHeadLocalRot = Quaternion.Inverse(syncData.transform.rotation) * syncData.HeadRot;
            avatar.head.localRotation = theirHeadLocalRot;

            // --- CORRECCIÓN DEL LÁSER: DETECCIÓN SOBRE EL GRAFO DEL OPONENTE ---
            Vector3 theirGraphCenterGlobal = syncData.transform.TransformPoint(myGraphLocalOffset);

            bool isLookingAtGraphArea = false;
            Vector3 toTheirGraphCenter = theirGraphCenterGlobal - syncData.HeadPos;
            Vector3 theirLookDirection = syncData.HeadRot * Vector3.forward;

            if (Vector3.Dot(toTheirGraphCenter.normalized, theirLookDirection) > 0)
            {
                float distanceToRay = Vector3.Cross(theirLookDirection, toTheirGraphCenter).magnitude;
                if (distanceToRay <= graphDetectionRadius) isLookingAtGraphArea = true;
            }

            // OPTIMIZACIÓN: Solo acceder al material si hay un cambio de estado en el renderer (evitar setear booleanos redundantes)
            if (avatar.gazeConeRenderer.enabled != isLookingAtGraphArea)
            {
                avatar.gazeConeRenderer.enabled = isLookingAtGraphArea;
                if (isLookingAtGraphArea)
                {
                    avatar.gazeConeTransform.localScale = new Vector3(gazeConeBaseRadius, gazeConeBaseRadius, gazeConeMaxLength);
                }
            }

            // --- FASE 4: SINCRONIZACIÓN DE SELECCIÓN (EL RESALTADO) ---
            string networkNodeId = syncData.SelectedNodeId.ToString();

            if (avatar.currentHighlightedNodeId != networkNodeId)
            {
                if (!string.IsNullOrEmpty(avatar.currentHighlightedNodeId))
                {
                    ResetNodeVisuals(avatar.currentHighlightedNodeId);
                }

                avatar.currentHighlightedNodeId = networkNodeId;

                if (!string.IsNullOrEmpty(networkNodeId))
                {
                    HighlightNodeVisuals(networkNodeId, UserColorPalette.GetColor(playerId));
                }
            }
        }

        private void HighlightNodeVisuals(string nodeId, Color highlightColor)
        {
            if (miniNodesMap.TryGetValue(nodeId, out GameObject node))
            {
                node.GetComponent<Renderer>().material.color = highlightColor;
                node.transform.localScale = Vector3.one * 2.5f;
            }
        }

        private void ResetNodeVisuals(string nodeId)
        {
            if (miniNodesMap.TryGetValue(nodeId, out GameObject node))
            {
                if (originalColorsMap.TryGetValue(nodeId, out Color ogColor))
                {
                    node.GetComponent<Renderer>().material.color = ogColor;
                }

                float resetSize = (node.name.Contains("ROOT")) ? 1.5f : 1.0f;
                node.transform.localScale = Vector3.one * resetSize;
            }
        }

        private string _offlineHighlightedNode = "";

        public void HighlightNodeLocalFallback(string nodeId, Color highlightColor)
        {
            if (!string.IsNullOrEmpty(_offlineHighlightedNode)) ResetNodeVisuals(_offlineHighlightedNode);

            _offlineHighlightedNode = nodeId;
            HighlightNodeVisuals(nodeId, highlightColor);
        }
    }
}