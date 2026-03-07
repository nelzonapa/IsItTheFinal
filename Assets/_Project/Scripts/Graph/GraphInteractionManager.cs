using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ImmersiveGraph.Visual;

namespace ImmersiveGraph.Interaction
{
    public class GraphInteractionManager : MonoBehaviour
    {
        [Header("Metáfora Overview (La Bóveda)")]
        [Tooltip("Hacia dónde retrocede el mundo general. Z positivo es hacia adelante/atrás, Y es arriba.")]
        public Vector3 vaultPositionOffset = new Vector3(0, 0.8f, 2.5f);
        [Tooltip("Qué tan gigante se vuelve el planeta en el fondo.")]
        public float vaultScale = 3.5f;

        [Header("Metáfora Detail (La Mesa)")]
        [Tooltip("Posición exacta en la mesa donde se analiza la comunidad elegida.")]
        public Vector3 tableFocusPosition = new Vector3(0, 0.2f, 0);

        [Header("Animación")]
        public float animationDuration = 0.8f;

        // ESTADO
        private Transform _rootNode;
        private GraphNode _currentFocusedCommunity;
        private bool _isExpandedMode = false;

        // Memoria del estado inicial (El mundo pequeño en la mesa)
        private Vector3 _originalRootPos;
        private Vector3 _originalRootScale;

        public void InitializeGraph(Transform rootNode)
        {
            _rootNode = rootNode;
            _originalRootPos = rootNode.localPosition;
            _originalRootScale = rootNode.localScale;
            _isExpandedMode = false;
            _currentFocusedCommunity = null;
        }

        public void OnCommunityHoldActivated(GraphNode selectedNode)
        {
            if (_isExpandedMode)
            {
                if (_currentFocusedCommunity == selectedNode)
                {
                    // Si toco el que está en la mesa -> Todo vuelve a ser un planeta miniatura
                    StartCoroutine(AnimateResetToMiniature());
                }
                else
                {
                    // Si miro a la bóveda y elijo otro continente -> Hacemos el intercambio
                    StartCoroutine(AnimateSwapCommunity(selectedNode));
                }
            }
            else
            {
                // Si el planeta está cerrado en la mesa -> Expandir bóveda y traer continente a la mesa
                StartCoroutine(AnimateFocusToTable(selectedNode));
            }
        }

        // --- FASE 1 & 2: EXPANSIÓN (Mundo atrás, Comunidad a la mesa) ---
        IEnumerator AnimateFocusToTable(GraphNode targetCommunity)
        {
            _isExpandedMode = true;
            _currentFocusedCommunity = targetCommunity;

            // 1. DESVINCULAR: Sacamos temporalmente la comunidad del Root 
            // Esto evita que herede la escala gigante y nos permite moverla independiente.
            // El parámetro 'true' hace que mantenga su posición mundial exacta en este frame.
            targetCommunity.transform.SetParent(this.transform, true);

            // Guardar posiciones de partida para la interpolación
            Vector3 startRootPos = _rootNode.localPosition;
            Vector3 startRootScale = _rootNode.localScale;

            Vector3 startCommPos = targetCommunity.transform.localPosition;
            Quaternion startCommRot = targetCommunity.transform.localRotation;

            float timer = 0f;

            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // A. El Mundo General (Root) viaja hacia atrás y crece formando la bóveda
                _rootNode.localPosition = Vector3.Lerp(startRootPos, _originalRootPos + vaultPositionOffset, t);
                _rootNode.localScale = Vector3.Lerp(startRootScale, _originalRootScale * vaultScale, t);

                // B. La Comunidad seleccionada viaja hacia el centro de la mesa a escala normal
                targetCommunity.transform.localPosition = Vector3.Lerp(startCommPos, tableFocusPosition, t);
                targetCommunity.transform.localRotation = Quaternion.Lerp(startCommRot, Quaternion.identity, t);

                yield return null;
            }

            // Al terminar de viajar, expandimos visualmente los archivos (Preparando Fase 3)
            targetCommunity.ForceExpand(true);
        }

        // --- FASE 1: RETORNO (Comunidad vuelve al mundo, Mundo vuelve a la mesa) ---
        IEnumerator AnimateResetToMiniature()
        {
            // Ocultar archivos
            if (_currentFocusedCommunity != null) _currentFocusedCommunity.ForceExpand(false);

            // 1. REVINCULAR: Devolvemos la comunidad al Root ANTES de animar.
            _currentFocusedCommunity.transform.SetParent(_rootNode, true);

            Vector3 startRootPos = _rootNode.localPosition;
            Vector3 startRootScale = _rootNode.localScale;

            // Ahora la posición local de la comunidad es relativa al Root gigante
            Vector3 startCommPos = _currentFocusedCommunity.transform.localPosition;
            Quaternion startCommRot = _currentFocusedCommunity.transform.localRotation;

            float timer = 0f;

            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // A. El Mundo General se encoge y regresa a la mesa
                _rootNode.localPosition = Vector3.Lerp(startRootPos, _originalRootPos, t);
                _rootNode.localScale = Vector3.Lerp(startRootScale, _originalRootScale, t);

                // B. La Comunidad viaja internamente a su coordenada matemática original en la esfera
                _currentFocusedCommunity.transform.localPosition = Vector3.Lerp(startCommPos, _currentFocusedCommunity.originalLocalPosition, t);
                _currentFocusedCommunity.transform.localRotation = Quaternion.Lerp(startCommRot, Quaternion.identity, t);

                yield return null;
            }

            _isExpandedMode = false;
            _currentFocusedCommunity = null;
        }

        // --- TRANSICIÓN: INTERCAMBIO FLUIDO ---
        IEnumerator AnimateSwapCommunity(GraphNode newFocusNode)
        {
            GraphNode oldFocusNode = _currentFocusedCommunity;

            // Colapsar el viejo, expandir el nuevo
            oldFocusNode.ForceExpand(false);

            // 1. Jerarquías: El viejo vuelve al planeta, el nuevo sale del planeta
            oldFocusNode.transform.SetParent(_rootNode, true);
            newFocusNode.transform.SetParent(this.transform, true);

            _currentFocusedCommunity = newFocusNode;

            float timer = 0f;

            Vector3 startOldPos = oldFocusNode.transform.localPosition;
            Vector3 startNewPos = newFocusNode.transform.localPosition;
            Quaternion startNewRot = newFocusNode.transform.localRotation;

            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // A. El Mundo ya está en el fondo, no lo movemos.

                // B. El viejo viaja de la mesa hacia su posición en la bóveda
                oldFocusNode.transform.localPosition = Vector3.Lerp(startOldPos, oldFocusNode.originalLocalPosition, t);

                // C. El nuevo viaja desde la bóveda hacia la mesa
                newFocusNode.transform.localPosition = Vector3.Lerp(startNewPos, tableFocusPosition, t);
                newFocusNode.transform.localRotation = Quaternion.Lerp(startNewRot, Quaternion.identity, t);

                yield return null;
            }

            newFocusNode.ForceExpand(true);
        }
    }
}