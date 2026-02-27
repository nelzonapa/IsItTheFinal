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
        [Tooltip("Aumenta esto (ej. 1.5 o 2.0) si el avatar sale muy pegado o dentro del grafo.")]
        public float avatarDistanceMultiplier = 1.5f;

        [Tooltip("Largo máximo del cono visual en el minimundo.")]
        public float gazeConeMaxLength = 4.0f;

        [Header("Estilo Visual")]
        public Material hologramMaterial;
        public float miniLineWidth = 0.02f;

        // --- DICCIONARIOS DEL GRAFO ---
        public Dictionary<string, GameObject> miniNodesMap = new Dictionary<string, GameObject>();
        private Dictionary<string, Color> originalColorsMap = new Dictionary<string, Color>();
        private GameObject _highlightedNode = null;

        // --- VARIABLES DE SINCRONIZACIÓN ESPACIAL ---
        private Transform _realGraphRoot; // Para saber qué estamos mirando
        private Vector3 _realGraphCenter;
        private bool _isBuilt = false;

        private class MiniAvatarData
        {
            public GameObject root;
            public Transform head;
            public LineRenderer gazeCone;
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
        // 2. SINCRONIZACIÓN ESPACIAL (Mini-Avatares)
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

            GameObject avatarRoot = new GameObject($"MiniAvatar_P{playerId}");
            avatarRoot.transform.SetParent(miniWorldRoot);
            avatarRoot.transform.localScale = Vector3.one;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(avatarRoot.transform);
            head.transform.localPosition = Vector3.zero;
            head.transform.localScale = Vector3.one * 2.5f;
            Destroy(head.GetComponent<Collider>());

            Renderer r = head.GetComponent<Renderer>();
            if (hologramMaterial != null) r.material = hologramMaterial;
            r.material.color = playerColor;

            GameObject gazeObj = new GameObject("GazeCone");
            gazeObj.transform.SetParent(head.transform);
            gazeObj.transform.localPosition = Vector3.zero;
            gazeObj.transform.localRotation = Quaternion.identity;

            LineRenderer lr = gazeObj.AddComponent<LineRenderer>();
            if (hologramMaterial != null) lr.material = hologramMaterial;
            else lr.material = new Material(Shader.Find("Sprites/Default"));

            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.startWidth = 0.1f;
            lr.endWidth = 1.0f;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(playerColor, 0.0f), new GradientColorKey(playerColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.7f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            lr.colorGradient = gradient;

            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.forward * gazeConeMaxLength); // Usamos la variable de control

            _miniAvatars.Add(playerId, new MiniAvatarData
            {
                root = avatarRoot,
                head = head.transform,
                gazeCone = lr
            });
        }

        private void UpdateMiniAvatar(int playerId, HardwareRigSync syncData)
        {
            if (!_miniAvatars.TryGetValue(playerId, out MiniAvatarData avatar)) return;

            // 1. POSICIÓN MULTIPLICADA (Para que no se meta dentro del grafo)
            Vector3 relativePos = syncData.HeadPos - _realGraphCenter;
            avatar.root.transform.localPosition = relativePos * avatarDistanceMultiplier;
            avatar.head.localRotation = syncData.HeadRot;

            // 2. CONO DE VISIÓN INTELIGENTE (Raycast en el mundo real)
            bool isLookingAtGraph = false;
            float distanceToGraph = gazeConeMaxLength;

            // Lanzamos un rayo desde la cabeza real hacia donde está mirando (MÁXIMO 20 metros)
            if (Physics.Raycast(syncData.HeadPos, syncData.HeadRot * Vector3.forward, out RaycastHit hit, 20f))
            {
                // Si chocamos con algo, verificamos si es un nodo del grafo
                if (hit.collider.GetComponent<GraphNode>() != null)
                {
                    isLookingAtGraph = true;
                    // Escalamos la distancia de impacto para que se vea bien en miniatura
                    distanceToGraph = Mathf.Min(hit.distance * scaleFactor * avatarDistanceMultiplier, gazeConeMaxLength);
                }
            }

            // Apagamos o prendemos el cono según lo que miramos
            avatar.gazeCone.enabled = isLookingAtGraph;

            if (isLookingAtGraph)
            {
                // Ajustamos el largo del cono hasta tocar el nodo en miniatura
                avatar.gazeCone.SetPosition(1, Vector3.forward * distanceToGraph);
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