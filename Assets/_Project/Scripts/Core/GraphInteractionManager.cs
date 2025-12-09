using UnityEngine;
using System.Collections.Generic;

namespace ImmersiveGraph.Core
{
    public class GraphInteractionManager : MonoBehaviour
    {
        [Header("Referencias")]
        public GraphManager graphBuilder; // Referencia al constructor para acceder a los nodos
        public Transform xrOrigin;        // La posición del jugador (Cámara/Rig)
        public Transform zone2Container;  // La caja de la mesa (Padre original)

        [Header("Visual Feedback")]
        public GameObject communityHighlightPrefab; // El círculo que pediste

        // Estado del sistema
        private bool isSphericalMode = false;
        private float overviewScale = 5.0f; // Se guardará automáticamente
        private GameObject currentHighlightRing;

        void Start()
        {
            // Crear un anillo temporal si no hay prefab asignado
            if (communityHighlightPrefab == null)
            {
                currentHighlightRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(currentHighlightRing.GetComponent<Collider>());
                currentHighlightRing.transform.localScale = new Vector3(1, 0.05f, 1);
                currentHighlightRing.SetActive(false);
                var rend = currentHighlightRing.GetComponent<Renderer>();
                rend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                rend.material.color = new Color(1, 1, 0, 0.3f); // Amarillo transparente
            }
            else
            {
                currentHighlightRing = Instantiate(communityHighlightPrefab);
                currentHighlightRing.SetActive(false);
            }
        }

        // --- EVENTOS QUE VIENEN DEL NODO (NodeInteraction.cs) ---

        public void OnNodeHoverEnter(Transform nodeTrans, string type, string id)
        {
            // REGLA PAPER: Resaltar Comunidad (Círculo)
            // Si apunto a un MACRO o MICRO topic, mostrar anillo alrededor
            if (type == "MACRO_TOPIC" || type == "MICRO_TOPIC")
            {
                currentHighlightRing.SetActive(true);
                currentHighlightRing.transform.position = nodeTrans.position;

                // Orientar hacia el usuario
                currentHighlightRing.transform.LookAt(xrOrigin);
                currentHighlightRing.transform.Rotate(90, 0, 0); // Ajustar rotación del cilindro/sprite

                // Ajustar tamaño del anillo según la escala del nodo (aprox)
                float scale = nodeTrans.lossyScale.x * 1.5f; // 5 veces más grande que el nodo
                currentHighlightRing.transform.localScale = new Vector3(scale, scale * 0.05f, scale);
            }

            // REGLA PAPER: Resaltar Enlaces
            // (Aquí llamaríamos a una función en GraphManager para pintar las líneas conectadas a 'id')
            // Por ahora nos centramos en el cambio de vista.
        }

        public void OnNodeHoverExit()
        {
            currentHighlightRing.SetActive(false);
        }

        public void OnNodeSelected(string type, string id)
        {
            Debug.Log($"Nodo seleccionado: {id} ({type})");

            // LÓGICA DE TRANSICIÓN DE VISTA

            // 1. Si estoy en la Mesa (Overview) y toco un MICRO_TOPIC -> Ir a Esférico
            if (!isSphericalMode && type == "MICRO_TOPIC")
            {
                SwitchToSphericalMode(id);
            }
            // 2. Si estoy en la Mesa y toco un MACRO -> Mostrar sus hijos MICRO (Drill down en la caja)
            else if (!isSphericalMode && type == "MACRO_TOPIC")
            {
                graphBuilder.ShowChildrenOf(id); // Función que añadiremos a GraphManager
            }
            // 3. Si ya estoy en Esférico... (Aquí iría la lógica de traer a la zona objeto, fase 2.3)
        }

        // --- LA DANZA DEL GRAFO (Cambio de Modo) ---

        void SwitchToSphericalMode(string focusNodeId)
        {
            Debug.Log(">>> ACTIVANDO MODO ESFÉRICO <<<");

            // 1. Guardar estado anterior
            overviewScale = graphBuilder.transform.localScale.x;

            // 2. Desanclar de la mesa
            graphBuilder.transform.SetParent(null); // O setParent(xrOrigin) si quieres que viaje contigo

            // 3. Posicionar en el centro de la sala (asumiendo que XR Origin está en 0,0,0)
            // O mejor, centrarlo en la cabeza del usuario pero sin rotación
            graphBuilder.transform.position = new Vector3(0, 1.5f, 5);
            graphBuilder.transform.rotation = Quaternion.identity;

            // 4. ESCALA 1:1 (Tamaño real gigante)
            graphBuilder.transform.localScale = Vector3.one;

            // 5. Mostrar los niveles profundos (Documents y Entities)
            // Aquí deberíamos filtrar solo los hijos del nodo seleccionado, 
            // pero por ahora mostramos el grafo completo para probar la escala.
            graphBuilder.ShowDeepLevels(); // Función que añadiremos

            isSphericalMode = true;
        }

        // Función pública para volver a la mesa (se usará desde el botón de reinicio o UI)
        public void ReturnToOverview()
        {
            if (!isSphericalMode) return;

            // 1. Volver a la caja
            graphBuilder.transform.SetParent(zone2Container);

            // 2. Resetear transformaciones locales (para que el Auto-Fit funcione de nuevo o usar la guardada)
            graphBuilder.transform.localPosition = Vector3.zero;
            graphBuilder.transform.localRotation = Quaternion.identity;
            graphBuilder.transform.localScale = Vector3.one * overviewScale;

            // 3. Ocultar niveles profundos
            graphBuilder.HideDeepLevels();

            isSphericalMode = false;
        }
    }
}