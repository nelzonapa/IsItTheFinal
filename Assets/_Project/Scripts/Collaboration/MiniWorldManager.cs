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
        [Tooltip("Multiplicador de distancia para que el avatar no esté pegado al grafo (ej. 1.5 - 2.5)")]
        public float avatarDistanceMultiplier = 1.5f;

        [Header("Detección de Mirada (Área del Grafo)")]
        [Tooltip("Radio de la 'burbuja invisible' alrededor del grafo real. Si el usuario mira hacia esta burbuja, el cono se activa.")]
        public float graphDetectionRadius = 1.5f;

        [Header("Visualización del Cono 3D")]
        [Tooltip("Largo máximo del cono en el minimundo.")]
        public float gazeConeMaxLength = 6.0f;
        [Tooltip("Anchura de la base del cono (qué tan abierto es).")]
        public float gazeConeBaseRadius = 1.0f;

        [Tooltip("¿Usar el color del jugador para el cono? Si es falso, usará el color personalizado de abajo.")]
        public bool usePlayerColorForCone = true;
        public Color customConeColor = Color.yellow;
        [Range(0f, 1f)] public float gazeConeAlpha = 0.3f; // Transparencia del cono

        [Header("Estilo Visual Base")]
        public Material hologramMaterial;
        public float miniLineWidth = 0.02f;

        // --- DICCIONARIOS DEL GRAFO ---
        public Dictionary<string, GameObject> miniNodesMap = new Dictionary<string, GameObject>();
        private Dictionary<string, Color> originalColorsMap = new Dictionary<string, Color>();
        private GameObject _highlightedNode = null;

        // --- VARIABLES DE SINCRONIZACIÓN ESPACIAL ---
        private Transform _realGraphRoot;
        private Vector3 _realGraphCenter;
        private bool _isBuilt = false;

        private class MiniAvatarData
        {
            public GameObject root;
            public Transform head;
            public MeshRenderer gazeConeRenderer;
            public Transform gazeConeTransform;
        }

        private Dictionary<int, MiniAvatarData> _miniAvatars = new Dictionary<int, MiniAvatarData>();

        // =========================================================
        // 1. CONSTRUCCIÓN DEL GRAFO
        // =========================================================
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

            Color rColor = realRoot.GetComponent<Renderer>()?.material.color ?? Color.white;
            GameObject miniRoot = CreateMiniNode(rootLogic.myData.id, Vector3.zero, rColor, 1.5f);

            miniNodesMap.Add(rootLogic.myData.id, miniRoot);
            originalColorsMap.Add(rootLogic.myData.id, rColor);

            foreach (GraphNode comm in realCommunities)
            {
                Color cColor = comm.GetComponent<Renderer>()?.material.color ?? Color.cyan;
                GameObject miniComm = CreateMiniNode(comm.myData.id, comm.transform.localPosition, cColor, 1.0f);

                miniNodesMap.Add(comm.myData.id, miniComm);
                originalColorsMap.Add(comm.myData.id, cColor);

                DrawMiniLine(miniComm.transform);
            }

            miniWorldRoot.localScale = Vector3.one * scaleFactor;

            _isBuilt = true;
            Debug.Log($"[MiniWorld] Holograma 3D creado. {_realGraphCenter} es el centro real.");
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
            if (hologramMaterial != null) r.material = hologramMaterial;
            r.material.color = color;

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
            if (hologramMaterial != null) lr.material = hologramMaterial;
            else lr.material = new Material(Shader.Find("Sprites/Default"));

            lr.startWidth = miniLineWidth;
            lr.endWidth = miniLineWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = false;

            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, childComm.localPosition);
        }

        // =========================================================
        // 2. SINCRONIZACIÓN ESPACIAL (Mini-Avatares y Cono 3D)
        // =========================================================
        void Update()
        {
            if (!_isBuilt || miniWorldRoot == null) return;

            var players = FindObjectsByType<HardwareRigSync>(FindObjectsSortMode.None);

            foreach (var p in players)
            {
                int pId = p.Object.StateAuthority.PlayerId;

                if (!_miniAvatars.ContainsKey(pId))
                {
                    CreateMiniAvatar(pId);
                }

                UpdateMiniAvatar(pId, p);
            }
        }

        private void CreateMiniAvatar(int playerId)
        {
            Color playerColor = UserColorPalette.GetColor(playerId);

            // 1. Contenedor
            GameObject avatarRoot = new GameObject($"MiniAvatar_P{playerId}");
            avatarRoot.transform.SetParent(miniWorldRoot);
            avatarRoot.transform.localScale = Vector3.one;

            // 2. Cabeza
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(avatarRoot.transform);
            head.transform.localPosition = Vector3.zero;
            head.transform.localScale = Vector3.one * 2.5f;
            Destroy(head.GetComponent<Collider>());

            Renderer headRenderer = head.GetComponent<Renderer>();
            if (hologramMaterial != null) headRenderer.material = hologramMaterial;
            headRenderer.material.color = playerColor;

            // 3. CONO 3D REAL (Generado por código)
            GameObject gazeObj = new GameObject("GazeCone3D");
            gazeObj.transform.SetParent(head.transform);
            gazeObj.transform.localPosition = Vector3.zero;
            gazeObj.transform.localRotation = Quaternion.identity;

            MeshFilter meshFilter = gazeObj.AddComponent<MeshFilter>();
            meshFilter.mesh = GenerateConeMesh(16); // 16 segmentos = un cono suave

            MeshRenderer coneRenderer = gazeObj.AddComponent<MeshRenderer>();

            // Asignar material y color personalizado o del jugador
            if (hologramMaterial != null) coneRenderer.material = new Material(hologramMaterial); // Instancia única para poder cambiarle la transparencia
            else coneRenderer.material = new Material(Shader.Find("Standard"));

            Color coneColor = usePlayerColorForCone ? playerColor : customConeColor;
            coneColor.a = gazeConeAlpha; // Aplicar transparencia
            coneRenderer.material.color = coneColor;

            // Modificamos el tamaño del cono según el inspector
            gazeObj.transform.localScale = new Vector3(gazeConeBaseRadius, gazeConeBaseRadius, gazeConeMaxLength);

            _miniAvatars.Add(playerId, new MiniAvatarData
            {
                root = avatarRoot,
                head = head.transform,
                gazeConeRenderer = coneRenderer,
                gazeConeTransform = gazeObj.transform
            });
        }

        // Generador de Malla 3D para un Cono perfecto
        private Mesh GenerateConeMesh(int segments)
        {
            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero; // Punta del cono en el ojo (Origen)
            vertices[segments + 1] = new Vector3(0, 0, 1f); // Centro de la base (Largo = 1 normalizado)

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

            // 1. POSICIÓN MULTIPLICADA (Para empujar el avatar hacia afuera)
            Vector3 relativePos = syncData.HeadPos - _realGraphCenter;
            avatar.root.transform.localPosition = relativePos * avatarDistanceMultiplier;
            avatar.head.localRotation = syncData.HeadRot;

            // 2. DETECCIÓN DE ÁREA MATEMÁTICA (No depende de físicas ni colliders)
            bool isLookingAtGraphArea = false;

            // Vector desde la cabeza del usuario hasta el centro del grafo real
            Vector3 toGraphCenter = _realGraphCenter - syncData.HeadPos;
            Vector3 lookDirection = syncData.HeadRot * Vector3.forward;

            // Verificamos si está mirando "hacia adelante" en dirección al grafo (Producto Punto)
            if (Vector3.Dot(toGraphCenter.normalized, lookDirection) > 0)
            {
                // Calculamos la distancia de separación entre la línea de visión y el centro del grafo (Producto Cruz)
                float distanceToRay = Vector3.Cross(lookDirection, toGraphCenter).magnitude;

                // Si la línea de visión pasa a una distancia menor al radio de la "burbuja", lo está mirando
                if (distanceToRay <= graphDetectionRadius)
                {
                    isLookingAtGraphArea = true;
                }
            }

            // Apagamos o prendemos el cono 3D
            avatar.gazeConeRenderer.enabled = isLookingAtGraphArea;

            // Actualizamos en tiempo real las dimensiones del cono por si las cambiaste en el Inspector
            if (isLookingAtGraphArea)
            {
                avatar.gazeConeTransform.localScale = new Vector3(gazeConeBaseRadius, gazeConeBaseRadius, gazeConeMaxLength);

                // Actualizar color/transparencia dinámicamente
                Color coneColor = usePlayerColorForCone ? UserColorPalette.GetColor(playerId) : customConeColor;
                coneColor.a = gazeConeAlpha;
                avatar.gazeConeRenderer.material.color = coneColor;
            }
        }

        // =========================================================
        // 3. ATENCIÓN COLABORATIVA 
        // =========================================================
        public void HighlightNode(string nodeId, Color highlightColor)
        {
            if (miniNodesMap.TryGetValue(nodeId, out GameObject node))
            {
                ResetCurrentHighlight();

                _highlightedNode = node;
                _highlightedNode.GetComponent<Renderer>().material.color = highlightColor;
                _highlightedNode.transform.localScale *= 2.0f;
            }
        }

        public void ResetHighlight(string nodeId)
        {
            if (_highlightedNode != null && _highlightedNode.name == $"Mini_{nodeId}")
            {
                ResetCurrentHighlight();
            }
        }

        private void ResetCurrentHighlight()
        {
            if (_highlightedNode != null)
            {
                string id = _highlightedNode.name.Replace("Mini_", "");
                if (originalColorsMap.TryGetValue(id, out Color ogColor))
                {
                    _highlightedNode.GetComponent<Renderer>().material.color = ogColor;
                }
                _highlightedNode.transform.localScale /= 2.0f;
                _highlightedNode = null;
            }
        }
    }
}