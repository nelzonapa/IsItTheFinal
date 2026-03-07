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

        // Memoria del estado inicial y factor de expansión de radio
        private Vector3 _originalRootPos;
        private Vector3 _originalRootScale;
        private float _radiusExpandFactor = 1f; // Calculado dinámicamente

        private List<GraphNode> _allCommunities = new List<GraphNode>();

        // SE ACTUALIZÓ PARA RECIBIR LOS DOS RADIOS
        public void InitializeGraph(Transform rootNode, float minRadius, float expRadius)
        {
            _rootNode = rootNode;
            _originalRootPos = rootNode.localPosition;
            _originalRootScale = rootNode.localScale;
            _isExpandedMode = false;
            _currentFocusedCommunity = null;

            // Calculamos cuánto tienen que separarse los nodos (Ej: Si min es 0.4 y exp es 1.2, el factor es 3)
            _radiusExpandFactor = expRadius / minRadius;

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

        public void OnCommunityHoldActivated(GraphNode selectedNode)
        {
            if (_isExpandedMode)
            {
                if (_currentFocusedCommunity == selectedNode)
                {
                    StartCoroutine(AnimateResetToMiniature());
                }
                else
                {
                    StartCoroutine(AnimateSwapCommunity(selectedNode));
                }
            }
            else
            {
                StartCoroutine(AnimateFocusToTable(selectedNode));
            }
        }

        private void SetCommunityFileState(GraphNode community, bool showFiles, bool isInteractive)
        {
            if (community == null) return;
            community.ForceExpand(showFiles);

            if (showFiles && community.childNodes != null)
            {
                foreach (GameObject fileObj in community.childNodes)
                {
                    if (fileObj == null) continue;

                    Collider col = fileObj.GetComponent<Collider>();
                    if (col != null) col.enabled = isInteractive;

                    Canvas[] uis = fileObj.GetComponentsInChildren<Canvas>(true);
                    foreach (Canvas ui in uis)
                    {
                        ui.gameObject.SetActive(isInteractive);
                    }
                }
            }
        }

        // ==========================================
        // CINEMÁTICAS CON EXPANSIÓN DE RADIO
        // ==========================================

        IEnumerator AnimateFocusToTable(GraphNode targetCommunity)
        {
            _isExpandedMode = true;
            _currentFocusedCommunity = targetCommunity;

            SetCommunityFileState(targetCommunity, true, true);
            foreach (GraphNode comm in _allCommunities)
            {
                if (comm != targetCommunity) SetCommunityFileState(comm, true, false);
            }

            targetCommunity.transform.SetParent(this.transform, true);

            Vector3 startRootPos = _rootNode.localPosition;
            Vector3 startRootScale = _rootNode.localScale;
            Vector3 startCommPos = targetCommunity.transform.localPosition;
            Quaternion startCommRot = targetCommunity.transform.localRotation;

            // Capturamos desde dónde arranca cada comunidad para hacer el Lerp seguro
            Dictionary<GraphNode, Vector3> startPositions = new Dictionary<GraphNode, Vector3>();
            foreach (GraphNode comm in _allCommunities)
            {
                startPositions[comm] = comm.transform.localPosition;
            }

            float timer = 0f;
            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // 1. El mundo crece y retrocede
                _rootNode.localPosition = Vector3.Lerp(startRootPos, _originalRootPos + vaultPositionOffset, t);
                _rootNode.localScale = Vector3.Lerp(startRootScale, _originalRootScale * vaultScale, t);

                // 2. La comunidad seleccionada viene a la mesa
                targetCommunity.transform.localPosition = Vector3.Lerp(startCommPos, tableFocusPosition, t);
                targetCommunity.transform.localRotation = Quaternion.Lerp(startCommRot, Quaternion.identity, t);

                // 3. ¡NUEVO! Las comunidades de fondo se expanden outward multiplicando su posición original
                foreach (GraphNode comm in _allCommunities)
                {
                    if (comm != targetCommunity)
                    {
                        Vector3 expandedPos = comm.originalLocalPosition * _radiusExpandFactor;
                        comm.transform.localPosition = Vector3.Lerp(startPositions[comm], expandedPos, t);
                    }
                }

                yield return null;
            }
        }

        IEnumerator AnimateResetToMiniature()
        {
            foreach (GraphNode comm in _allCommunities)
            {
                SetCommunityFileState(comm, false, false);
            }

            _currentFocusedCommunity.transform.SetParent(_rootNode, true);

            Vector3 startRootPos = _rootNode.localPosition;
            Vector3 startRootScale = _rootNode.localScale;

            Dictionary<GraphNode, Vector3> startPositions = new Dictionary<GraphNode, Vector3>();
            foreach (GraphNode comm in _allCommunities)
            {
                startPositions[comm] = comm.transform.localPosition;
            }

            Quaternion startCommRot = _currentFocusedCommunity.transform.localRotation;

            float timer = 0f;
            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // 1. El mundo se encoge y regresa a la mesa
                _rootNode.localPosition = Vector3.Lerp(startRootPos, _originalRootPos, t);
                _rootNode.localScale = Vector3.Lerp(startRootScale, _originalRootScale, t);

                // 2. TODAS las comunidades regresan a su radio miniatura original (incluyendo la que estaba en la mesa)
                foreach (GraphNode comm in _allCommunities)
                {
                    if (comm == _currentFocusedCommunity)
                    {
                        comm.transform.localPosition = Vector3.Lerp(startPositions[comm], comm.originalLocalPosition, t);
                        comm.transform.localRotation = Quaternion.Lerp(startCommRot, Quaternion.identity, t);
                    }
                    else
                    {
                        comm.transform.localPosition = Vector3.Lerp(startPositions[comm], comm.originalLocalPosition, t);
                    }
                }

                yield return null;
            }

            _isExpandedMode = false;
            _currentFocusedCommunity = null;
        }

        IEnumerator AnimateSwapCommunity(GraphNode newFocusNode)
        {
            GraphNode oldFocusNode = _currentFocusedCommunity;

            SetCommunityFileState(oldFocusNode, true, false);
            SetCommunityFileState(newFocusNode, true, true);

            oldFocusNode.transform.SetParent(_rootNode, true);
            newFocusNode.transform.SetParent(this.transform, true);

            _currentFocusedCommunity = newFocusNode;

            float timer = 0f;
            Vector3 startOldPos = oldFocusNode.transform.localPosition;
            Vector3 startNewPos = newFocusNode.transform.localPosition;
            Quaternion startNewRot = newFocusNode.transform.localRotation;

            // Calculamos el punto destino exacto en el fondo para la comunidad que regresa
            Vector3 oldExpandedTargetPos = oldFocusNode.originalLocalPosition * _radiusExpandFactor;

            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // El viejo foco viaja desde la mesa hasta su posición con radio EXPANDIDO en la bóveda
                oldFocusNode.transform.localPosition = Vector3.Lerp(startOldPos, oldExpandedTargetPos, t);

                // El nuevo foco viaja desde la bóveda a la mesa
                newFocusNode.transform.localPosition = Vector3.Lerp(startNewPos, tableFocusPosition, t);
                newFocusNode.transform.localRotation = Quaternion.Lerp(startNewRot, Quaternion.identity, t);

                yield return null;
            }
        }
    }
}