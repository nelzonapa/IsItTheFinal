using UnityEngine;
using System.Collections.Generic;

namespace ImmersiveGraph.Core
{
    public class GraphInteractionManager : MonoBehaviour
    {
        [Header("Referencias")]
        public GraphManager graphBuilder;
        public Transform xrOrigin;
        public Transform zone2Container;
        public Transform zone3ObjectZone; // <-- NUEVO: Referencia a Zona 3 para replicar

        [Header("Visual Feedback")]
        public GameObject communityHighlightPrefab;

        private bool isSphericalMode = false;
        private float overviewScale = 1.0f;
        private GameObject currentHighlightRing;

        void Start()
        {
            if (communityHighlightPrefab != null)
            {
                currentHighlightRing = Instantiate(communityHighlightPrefab);
                currentHighlightRing.SetActive(false);
            }
            else
            {
                // Fallback temporal
                currentHighlightRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(currentHighlightRing.GetComponent<Collider>());
                currentHighlightRing.SetActive(false);
            }
        }

        // --- HOVER: Resaltado de Comunidad ---
        public void OnNodeHoverEnter(Transform nodeTrans, string type, string id)
        {
            if (isSphericalMode) return; // En modo esférico quizás el comportamiento cambia

            // Lógica: Si apunto a un MACRO, ver si tiene hijos visibles.
            // Si sí, el anillo rodea a TODOS. Si no, solo al MACRO.
            if (type == "MACRO_TOPIC")
            {
                List<GameObject> children = graphBuilder.GetActiveChildrenOf(id);
                if (children.Count > 0)
                {
                    // Calcular límites de la comunidad
                    Bounds bounds = new Bounds(nodeTrans.position, Vector3.zero);
                    foreach (var child in children) bounds.Encapsulate(child.transform.position);

                    ShowRingOnBounds(bounds);
                }
                else
                {
                    // Solo el nodo
                    ShowRingOnNode(nodeTrans);
                }
            }
            else if (type == "MICRO_TOPIC")
            {
                // Si apunto a un hijo, resaltar también la comunidad entera (incluyendo padre)
                // (Por simplicidad ahora solo resaltamos el nodo, pero puedes expandirlo)
                ShowRingOnNode(nodeTrans);
            }
        }

        void ShowRingOnNode(Transform t)
        {
            currentHighlightRing.SetActive(true);
            currentHighlightRing.transform.position = t.position;
            currentHighlightRing.transform.LookAt(xrOrigin);
            currentHighlightRing.transform.Rotate(90, 0, 0);
            float s = t.lossyScale.x * 2.0f;
            currentHighlightRing.transform.localScale = new Vector3(s, s * 0.05f, s);
        }

        void ShowRingOnBounds(Bounds b)
        {
            currentHighlightRing.SetActive(true);
            currentHighlightRing.transform.position = b.center;
            currentHighlightRing.transform.LookAt(xrOrigin);
            currentHighlightRing.transform.Rotate(90, 0, 0);

            // Escalar anillo para cubrir el bounds
            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float s = maxDim * 1.2f; // Un poco más grande
            currentHighlightRing.transform.localScale = new Vector3(s, s * 0.02f, s);
        }

        public void OnNodeHoverExit()
        {
            currentHighlightRing.SetActive(false);
        }

        // --- AGARRE (GRAB): Replicación a Zona 3 ---
        public void OnNodeGrabbed(string type, string id, GameObject originalObj)
        {
            // Solo replicamos si estamos en Overview y agarramos una comunidad
            if (!isSphericalMode && (type == "MACRO_TOPIC" || type == "MICRO_TOPIC"))
            {
                if (zone3ObjectZone != null)
                {
                    ReplicateCommunityToZone3(id, type);
                }
            }
        }

        void ReplicateCommunityToZone3(string nodeId, string type)
        {
            // Lógica simple de clonación:
            // 1. Identificar el MACRO padre (si agarré un Micro, busco su padre)
            // 2. Clonar el Macro y sus hijos activos en Zona 3.

            // (Esta es una implementación básica. Para producción requeriría un sistema de limpieza de zona 3)
            Debug.Log($"Replicando comunidad de {nodeId} a Zona 3");

            // TODO: Crear un contenedor en Zona 3 y copiar los GameObjects ahí.
            // Por ahora solo un Log para confirmar que la lógica se dispara.
        }

        // --- HOLD COMPLETE: Transiciones ---
        public void OnNodeHoldComplete(string type, string id)
        {
            Debug.Log($"HOLD COMPLETADO en {type}");

            if (type == "MACRO_TOPIC")
            {
                // Abrir MICRO TOPICS
                graphBuilder.RevealMicroTopicsFor(id);
            }
            else if (type == "MICRO_TOPIC")
            {
                // Pasar a ESFÉRICO
                SwitchToSphericalMode(id);
            }
        }

        void SwitchToSphericalMode(string focusNodeId)
        {
            overviewScale = graphBuilder.transform.localScale.x;
            graphBuilder.transform.SetParent(null);
            graphBuilder.transform.position = new Vector3(0, 1.5f, 0);
            graphBuilder.transform.rotation = Quaternion.identity;
            graphBuilder.transform.localScale = Vector3.one;

            graphBuilder.SetSphericalMode(true); // Avisar al manager

            isSphericalMode = true;
        }

        public void ReturnToOverview()
        {
            if (!isSphericalMode) return;
            graphBuilder.transform.SetParent(zone2Container);
            graphBuilder.transform.localPosition = Vector3.zero;
            graphBuilder.transform.localRotation = Quaternion.identity;
            graphBuilder.transform.localScale = Vector3.one * overviewScale;

            graphBuilder.SetSphericalMode(false);
            graphBuilder.HideDeepLevels();
            isSphericalMode = false;
        }
    }
}