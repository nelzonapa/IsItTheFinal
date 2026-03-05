using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ImmersiveGraph.Visual;

namespace ImmersiveGraph.Interaction
{
    public class GraphInteractionManager : MonoBehaviour
    {
        [Header("Configuración Cielo (Contexto)")]
        [Tooltip("A dónde sube el Root (relativo a la mesa).")]
        public Vector3 skyPositionOffset = new Vector3(0, 1.6f, 1.5f);
        public Vector3 skyRotationOffset = new Vector3(-20, 42, 0);
        [Tooltip("Escala del Root en el cielo.")]
        public float skyScale = 2.0f; // No lo hagas muy gigante o se verá pixelado
        [Tooltip("Radio del abanico/arco en el cielo.")]
        public float horizonArcRadius = 2.0f;

        [Header("Configuración Mesa (Foco)")]
        [Tooltip("Posición en la mesa para la comunidad seleccionada.")]
        public Vector3 tableFocusPosition = new Vector3(0, 0.2f, 0);

        [Header("Animación")]
        public float animationDuration = 1.0f;

        // ESTADO
        private Transform _rootNode;
        private GraphNode _currentFocusedCommunity;

        // Memoria del estado inicial (Bola miniatura)
        private Vector3 _originalRootPos;
        private Vector3 _originalRootScale;

        // Lista de todas las comunidades para gestionarlas
        private List<GraphNode> _allCommunities = new List<GraphNode>();

        private bool _isExpandedMode = false;

        // Inicialización (Llamado por el Spawner)
        public void InitializeGraph(Transform rootNode)
        {
            _rootNode = rootNode;
            _originalRootPos = rootNode.localPosition;
            _originalRootScale = rootNode.localScale;
            _isExpandedMode = false;
            _currentFocusedCommunity = null;

            // Recolectar todas las comunidades
            _allCommunities.Clear();
            foreach (Transform child in _rootNode)
            {
                GraphNode node = child.GetComponent<GraphNode>();
                if (node != null && node.nodeType == "community")
                {
                    _allCommunities.Add(node);
                }
            }
        }

        // --- ENTRADA: AL MANTENER PRESIONADO (HOLD) ---
        public void OnCommunityHoldActivated(GraphNode selectedNode)
        {
            if (_isExpandedMode)
            {
                if (_currentFocusedCommunity == selectedNode)
                {
                    // Si presiono la misma que ya tengo en la mesa -> REGRESAR A MINIATURA
                    StartCoroutine(AnimateResetToMiniature());
                }
                else
                {
                    // Si presiono una del cielo -> INTERCAMBIAR (SWAP)
                    StartCoroutine(AnimateSwapCommunity(selectedNode));
                }
            }
            else
            {
                // Si estoy en modo miniatura -> EXPANDIR AL CIELO
                StartCoroutine(AnimateToSkyMode(selectedNode));
            }
        }

        // --------------------------------------------------------
        // ANIMACIÓN 1: DE MINIATURA A CIELO + MESA
        // --------------------------------------------------------
        IEnumerator AnimateToSkyMode(GraphNode targetCommunity)
        {
            _isExpandedMode = true;
            _currentFocusedCommunity = targetCommunity;

            // 1. Calcular posiciones finales del ARCO para los NO seleccionados
            List<GraphNode> skyNodes = new List<GraphNode>();
            foreach (var node in _allCommunities)
            {
                if (node != targetCommunity) skyNodes.Add(node);
            }

            // --- CAMBIO AQUÍ: CALCULO DINÁMICO DE FILAS ---
            // Si hay < 10 nodos, 2 filas. Si hay > 50, 5 filas.
            int dynamicRows = Mathf.Clamp(skyNodes.Count / 8, 2, 6);

            Vector3[] arcPositions = HyperbolicMath.GetHorizonArcPositions(skyNodes.Count, horizonArcRadius, 160f, dynamicRows);
            // -----------------------------------------------
            // 2. Preparar el nodo seleccionado (Despegarlo del Root)
            // Lo hacemos hijo del Manager para que se mueva independiente del Root
            targetCommunity.transform.SetParent(this.transform);

            float timer = 0f;

            // Guardamos posiciones iniciales para el Lerp
            Vector3 startRootPos = _rootNode.localPosition;
            Vector3 startRootScale = _rootNode.localScale;
            Vector3 startCommPos = targetCommunity.transform.localPosition;
            Quaternion startCommRot = targetCommunity.transform.localRotation;

            // Guardamos las posiciones locales iniciales de los nodos del cielo (dentro del root)
            Dictionary<GraphNode, Vector3> startSkyPositions = new Dictionary<GraphNode, Vector3>();
            foreach (var node in skyNodes) startSkyPositions[node] = node.transform.localPosition;

            // --- BUCLE DE ANIMACIÓN ---
            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // A. Mover Root al Cielo
                // Posición
                _rootNode.localPosition = Vector3.Lerp(
                    startRootPos,
                    _originalRootPos + skyPositionOffset,
                    t
                );

                // Rotación
                _rootNode.localRotation = Quaternion.Lerp(
                    startCommRot,
                    Quaternion.Euler(skyRotationOffset),
                    t
                );

                // Escala
                _rootNode.localScale = Vector3.Lerp(
                    startRootScale,
                    _originalRootScale * skyScale,
                    t
                );
                // B. Mover Comunidad a la Mesa (Posición Fija)
                targetCommunity.transform.localPosition = Vector3.Lerp(startCommPos, tableFocusPosition, t);
                targetCommunity.transform.localRotation = Quaternion.Lerp(startCommRot, Quaternion.identity, t);

                // C. Reorganizar los nodos del cielo en forma de ARCO
                for (int i = 0; i < skyNodes.Count; i++)
                {
                    // Lerp desde su posición esférica original -> posición de arco
                    skyNodes[i].transform.localPosition = Vector3.Lerp(startSkyPositions[skyNodes[i]], arcPositions[i], t);
                    // Opcional: Rotar para que miren al usuario (0,0,0 local del Root)
                    // skyNodes[i].transform.LookAt(_rootNode); // A veces queda mejor sin rotar, probemos posición primero.
                }

                yield return null;
            }

            // Al terminar, expandir archivos del que está en la mesa
            targetCommunity.ForceExpand(true);
        }

        // --------------------------------------------------------
        // ANIMACIÓN 2: REGRESAR A MINIATURA (RESET)
        // --------------------------------------------------------
        IEnumerator AnimateResetToMiniature()
        {
            // Colapsar archivos primero
            if (_currentFocusedCommunity != null) _currentFocusedCommunity.ForceExpand(false);

            // Devolver la comunidad al Root ANTES de animar, para que viaje con él
            // Pero necesitamos saber a qué posición local del Root debe volver.
            // Gracias a que guardamos 'originalLocalPosition' en GraphNode, sabemos dónde iba.
            _currentFocusedCommunity.transform.SetParent(_rootNode);

            float timer = 0f;

            // Datos actuales (Inicio animación)
            Vector3 startRootPos = _rootNode.localPosition;
            Vector3 startRootScale = _rootNode.localScale;

            // La posición actual de la comunidad (ahora que es hija de Root, su localPosition cambió drásticamente)
            Vector3 startCommPos = _currentFocusedCommunity.transform.localPosition;
            Quaternion startCommRot = _currentFocusedCommunity.transform.localRotation;

            // Guardamos posiciones actuales de los nodos del cielo (que están en Arco)
            Dictionary<GraphNode, Vector3> startSkyPositions = new Dictionary<GraphNode, Vector3>();
            foreach (var node in _allCommunities)
            {
                if (node != _currentFocusedCommunity) startSkyPositions[node] = node.transform.localPosition;
            }

            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // A. Root baja a la mesa
                _rootNode.localPosition = Vector3.Lerp(startRootPos, _originalRootPos, t);
                _rootNode.localScale = Vector3.Lerp(startRootScale, _originalRootScale, t);

                // B. Comunidad regresa a su posición esférica original
                _currentFocusedCommunity.transform.localPosition = Vector3.Lerp(startCommPos, _currentFocusedCommunity.originalLocalPosition, t);
                _currentFocusedCommunity.transform.localRotation = Quaternion.Lerp(startCommRot, Quaternion.identity, t);

                // C. Nodos del cielo regresan de Arco -> Esfera
                foreach (var node in _allCommunities)
                {
                    if (node != _currentFocusedCommunity)
                    {
                        node.transform.localPosition = Vector3.Lerp(startSkyPositions[node], node.originalLocalPosition, t);
                    }
                }

                yield return null;
            }

            _isExpandedMode = false;
            _currentFocusedCommunity = null;
        }

        // --------------------------------------------------------
        // ANIMACIÓN 3: INTERCAMBIO (SWAP) CIELO <-> MESA
        // --------------------------------------------------------
        IEnumerator AnimateSwapCommunity(GraphNode newFocusNode)
        {
            // 1. Colapsar el viejo
            GraphNode oldFocusNode = _currentFocusedCommunity;
            oldFocusNode.ForceExpand(false);

            // 2. Preparar el cambio de jerarquía
            // El viejo vuelve al Root
            oldFocusNode.transform.SetParent(_rootNode);
            // El nuevo sale del Root al Manager
            newFocusNode.transform.SetParent(this.transform);

            _currentFocusedCommunity = newFocusNode;

            // 3. Recalcular el Arco del Cielo (Ahora incluye al viejo, excluye al nuevo)
            List<GraphNode> skyNodes = new List<GraphNode>();
            foreach (var node in _allCommunities)
            {
                if (node != newFocusNode) skyNodes.Add(node);
            }

            // --- CAMBIO AQUÍ TAMBIÉN ---
            int dynamicRows = Mathf.Clamp(skyNodes.Count / 8, 2, 6);
            Vector3[] arcPositions = HyperbolicMath.GetHorizonArcPositions(skyNodes.Count, horizonArcRadius, 160f, dynamicRows);
            // ---------------------------

            float timer = 0f;

            // Capturar posiciones iniciales para Lerp
            Vector3 startOldPos = oldFocusNode.transform.localPosition; // Como hijo de Root
            Vector3 startNewPos = newFocusNode.transform.localPosition; // Como hijo de Manager

            // Para los nodos del cielo que se reacomodan
            Dictionary<GraphNode, Vector3> startSkyPositions = new Dictionary<GraphNode, Vector3>();
            foreach (var node in skyNodes) startSkyPositions[node] = node.transform.localPosition;

            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // A. El viejo sube al Cielo (a su posición de arco correspondiente)
                // Necesitamos encontrar qué índice le toca en el arco.
                // Simplificación: Buscamos su índice en la lista skyNodes
                int oldIndex = skyNodes.IndexOf(oldFocusNode);
                if (oldIndex >= 0)
                {
                    oldFocusNode.transform.localPosition = Vector3.Lerp(startOldPos, arcPositions[oldIndex], t);
                    oldFocusNode.transform.localRotation = Quaternion.Lerp(oldFocusNode.transform.localRotation, Quaternion.identity, t);
                }

                // B. El nuevo baja a la Mesa
                newFocusNode.transform.localPosition = Vector3.Lerp(startNewPos, tableFocusPosition, t);
                newFocusNode.transform.localRotation = Quaternion.Lerp(newFocusNode.transform.localRotation, Quaternion.identity, t);

                // C. Reajustar el resto del arco (para cerrar el hueco que dejó el nuevo)
                for (int i = 0; i < skyNodes.Count; i++)
                {
                    if (skyNodes[i] != oldFocusNode)
                    {
                        skyNodes[i].transform.localPosition = Vector3.Lerp(startSkyPositions[skyNodes[i]], arcPositions[i], t);
                    }
                }

                yield return null;
            }

            // Expandir el nuevo
            newFocusNode.ForceExpand(true);
        }
    }
}