using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ImmersiveGraph.Visual;

namespace ImmersiveGraph.Interaction
{
    public class GraphInteractionManager : MonoBehaviour
    {
        [Header("Metáfora Google Maps (Fases 1 y 2)")]
        [Tooltip("Distancia hacia afuera (zoom local) que avanza el continente/comunidad al seleccionarse.")]
        public float localZoomOffset = 0.4f;

        [Header("Animación")]
        public float animationDuration = 0.8f;

        // ESTADO
        private Transform _rootNode;
        private GraphNode _currentFocusedCommunity;
        private bool _isExpandedMode = false;

        public void InitializeGraph(Transform rootNode)
        {
            _rootNode = rootNode;
            _isExpandedMode = false;
            _currentFocusedCommunity = null;
        }

        public void OnCommunityHoldActivated(GraphNode selectedNode)
        {
            if (_isExpandedMode)
            {
                if (_currentFocusedCommunity == selectedNode)
                {
                    // Si toco el mismo que está abierto -> Alejar (Zoom Out)
                    StartCoroutine(AnimateResetToMiniature());
                }
                else
                {
                    // Si toco otro distinto -> Cambiar foco fluidamente
                    StartCoroutine(AnimateSwapCommunity(selectedNode));
                }
            }
            else
            {
                // Si el planeta está cerrado -> Acercar (Zoom In)
                StartCoroutine(AnimateFocusInSitu(selectedNode));
            }
        }

        // --- FASE 2: EXPANSIÓN IN-SITU ---
        IEnumerator AnimateFocusInSitu(GraphNode targetCommunity)
        {
            _isExpandedMode = true;
            _currentFocusedCommunity = targetCommunity;

            // 1. Expandimos los archivos visualmente al inicio para que el despliegue sea orgánico
            targetCommunity.ForceExpand(true);

            float timer = 0f;
            Vector3 startPos = targetCommunity.transform.localPosition;

            // 2. Calculamos la posición hacia afuera (Zoom In guiado por su vector normal)
            Vector3 directionOut = targetCommunity.originalLocalPosition.normalized;
            Vector3 endPos = targetCommunity.originalLocalPosition + (directionOut * localZoomOffset);

            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                // Usamos SmoothStep para una aceleración y desaceleración elegante
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                targetCommunity.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }
        }

        // --- FASE 1: RETORNO AL PLANETA BASE ---
        IEnumerator AnimateResetToMiniature()
        {
            if (_currentFocusedCommunity != null) _currentFocusedCommunity.ForceExpand(false);

            float timer = 0f;
            Vector3 startPos = _currentFocusedCommunity.transform.localPosition;
            Vector3 endPos = _currentFocusedCommunity.originalLocalPosition;

            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                _currentFocusedCommunity.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            _isExpandedMode = false;
            _currentFocusedCommunity = null;
        }

        // --- TRANSICIÓN DIRECTA ENTRE COMUNIDADES ---
        IEnumerator AnimateSwapCommunity(GraphNode newFocusNode)
        {
            GraphNode oldFocusNode = _currentFocusedCommunity;

            // Colapsamos el anterior y expandimos el nuevo
            oldFocusNode.ForceExpand(false);
            newFocusNode.ForceExpand(true);

            _currentFocusedCommunity = newFocusNode;

            float timer = 0f;

            Vector3 startOldPos = oldFocusNode.transform.localPosition;
            Vector3 endOldPos = oldFocusNode.originalLocalPosition;

            Vector3 startNewPos = newFocusNode.transform.localPosition;
            Vector3 directionOut = newFocusNode.originalLocalPosition.normalized;
            Vector3 endNewPos = newFocusNode.originalLocalPosition + (directionOut * localZoomOffset);

            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                oldFocusNode.transform.localPosition = Vector3.Lerp(startOldPos, endOldPos, t);
                newFocusNode.transform.localPosition = Vector3.Lerp(startNewPos, endNewPos, t);

                yield return null;
            }
        }
    }
}