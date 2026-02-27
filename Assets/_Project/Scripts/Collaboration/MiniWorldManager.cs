using UnityEngine;
using System.Collections.Generic;
using ImmersiveGraph.Interaction;

namespace ImmersiveGraph.Collaboration
{
    public class MiniWorldManager : MonoBehaviour
    {
        [Header("Configuración del Espacio")]
        public Transform miniWorldRoot;
        public float scaleFactor = 0.05f;

        [Header("Estilo Visual")]
        public Material hologramMaterial;
        public float miniLineWidth = 0.02f;

        // Diccionarios para sincronización rápida
        public Dictionary<string, GameObject> miniNodesMap = new Dictionary<string, GameObject>();
        private Dictionary<string, Color> originalColorsMap = new Dictionary<string, Color>();

        // Variables para el Resaltado
        private GameObject _highlightedNode = null;

        public void BuildMiniatureFromRealGraph(Transform realRoot, List<GraphNode> realCommunities)
        {
            if (miniWorldRoot == null) return;

            foreach (Transform child in miniWorldRoot) Destroy(child.gameObject);
            miniNodesMap.Clear();
            originalColorsMap.Clear();

            GraphNode rootLogic = realRoot.GetComponent<GraphNode>();
            if (rootLogic == null) return;

            // 1. Extraer color real de la Raíz y crearla
            Color rColor = realRoot.GetComponent<Renderer>()?.material.color ?? Color.white;
            GameObject miniRoot = CreateMiniNode(rootLogic.myData.id, Vector3.zero, rColor, 1.5f);

            miniNodesMap.Add(rootLogic.myData.id, miniRoot);
            originalColorsMap.Add(rootLogic.myData.id, rColor);

            // 2. Extraer color real de las Comunidades y crearlas
            foreach (GraphNode comm in realCommunities)
            {
                Color cColor = comm.GetComponent<Renderer>()?.material.color ?? Color.cyan;

                // Usamos la posición local exacta calculada por H3
                GameObject miniComm = CreateMiniNode(comm.myData.id, comm.transform.localPosition, cColor, 1.0f);

                miniNodesMap.Add(comm.myData.id, miniComm);
                originalColorsMap.Add(comm.myData.id, cColor);

                // --- FIX: Llamamos a la línea pasándole solo la comunidad ---
                DrawMiniLine(miniComm.transform);
            }

            miniWorldRoot.localScale = Vector3.one * scaleFactor;
            Debug.Log($"[MiniWorld] Holograma 3D creado. {miniNodesMap.Count} nodos indexados.");
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

        // --- CORRECCIÓN CRÍTICA DE LA LÍNEA ---
        private void DrawMiniLine(Transform childComm)
        {
            GameObject lineObj = new GameObject("MiniLink");

            // Emparentamos directamente a la raíz del minimundo, no a la comunidad
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

            // Trabajamos puramente en espacio local relativo a miniWorldRoot
            lr.useWorldSpace = false;

            // Posición 0: El centro del minimundo (donde está el Root miniatura)
            lr.SetPosition(0, Vector3.zero);
            // Posición 1: La posición local exacta de la comunidad
            lr.SetPosition(1, childComm.localPosition);
        }

        // --- SISTEMA DE RESALTADO (ATENCIÓN COLABORATIVA) ---
        public void HighlightNode(string nodeId, Color highlightColor)
        {
            if (miniNodesMap.TryGetValue(nodeId, out GameObject node))
            {
                ResetCurrentHighlight(); // Limpiamos el anterior si lo hubiera

                _highlightedNode = node;
                _highlightedNode.GetComponent<Renderer>().material.color = highlightColor;
                _highlightedNode.transform.localScale *= 2.0f; // Lo hacemos el doble de grande
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
                    _highlightedNode.GetComponent<Renderer>().material.color = ogColor; // Volver al color original
                }
                _highlightedNode.transform.localScale /= 2.0f; // Volver al tamaño normal
                _highlightedNode = null;
            }
        }
    }
}