using ImmersiveGraph.Visual;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ImmersiveGraph.Interaction
{
    public class GraphInteractionManager : MonoBehaviour
    {
        [Header("Metáfora Overview (La Bóveda)")]
        [Tooltip("Hacia dónde retrocede el mundo general. Z positivo es hacia adelante/atrás, Y es arriba.")]
        public Vector3 vaultPositionOffset = new Vector3(0, 0.8f, 2.5f);
        [Tooltip("Rotación del mundo cuando entra en modo bóveda.")]
        public Vector3 vaultRotationOffset = new Vector3(0, 0, 0);
        [Tooltip("Qué tan gigante se vuelve el planeta en el fondo.")]
        public float vaultScale = 3.5f;

        [Header("Metáfora Detail (La Mesa)")]
        [Tooltip("Punto exacto (Objeto Vacío) en la mesa donde se analiza la comunidad elegida.")]
        public Transform tableFocusAnchor; // <-- NUEVO: EL PUNTO ANCLA

        [Header("Animación")]
        public float animationDuration = 0.8f;

        // ESTADO
        private Transform _rootNode;
        private GraphNode _currentFocusedCommunity;
        private bool _isExpandedMode = false;

        // Memoria del estado inicial y factor de expansión de radio
        private Vector3 _originalRootPos;
        private Vector3 _originalRootScale;
        private float _radiusExpandFactor = 1f;

        private List<GraphNode> _allCommunities = new List<GraphNode>();

        private Quaternion _originalRootRot;

        public void InitializeGraph(Transform rootNode, float minRadius, float expRadius)
        {
            _rootNode = rootNode;
            _originalRootPos = rootNode.localPosition;
            _originalRootRot = rootNode.localRotation;
            _originalRootScale = rootNode.localScale;
            _isExpandedMode = false;
            _currentFocusedCommunity = null;

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
            XRGrabInteractable grab = selectedNode.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected)
            {
                grab.interactionManager.SelectExit(grab.firstInteractorSelecting, grab);
            }

            // Verificación de seguridad
            if (tableFocusAnchor == null)
            {
                Debug.LogError("[Interacción] ¡Falta asignar el 'Table Focus Anchor' en el Inspector!");
                return;
            }

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
        // CINEMÁTICAS CON EL NUEVO ANCLA
        // ==========================================

        IEnumerator AnimateFocusToTable(GraphNode targetCommunity)
        {
            _isExpandedMode = true;
            _currentFocusedCommunity = targetCommunity;

            XRGrabInteractable grab = targetCommunity.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = false;


            SetCommunityFileState(targetCommunity, true, true);
            foreach (GraphNode comm in _allCommunities)
            {
                if (comm != targetCommunity) SetCommunityFileState(comm, true, false);
            }

            // AHORA LO HACEMOS HIJO DEL ANCLA DE LA MESA
            targetCommunity.transform.SetParent(tableFocusAnchor, true);

            Vector3 startRootPos = _rootNode.localPosition;
            Vector3 startRootScale = _rootNode.localScale;

            // Posiciones iniciales relativas al ancla recién asignado
            Vector3 startCommPos = targetCommunity.transform.localPosition;
            Quaternion startCommRot = targetCommunity.transform.localRotation;
            Quaternion startRootRot = _rootNode.localRotation;

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

                Quaternion targetRot = _originalRootRot * Quaternion.Euler(vaultRotationOffset);
                _rootNode.localRotation = Quaternion.Lerp(startRootRot, targetRot, t);

                // 2. La comunidad va a Vector3.zero (que es exactamente el centro del Ancla)
                targetCommunity.transform.localPosition = Vector3.Lerp(startCommPos, Vector3.zero, t);
                targetCommunity.transform.localRotation = Quaternion.Lerp(startCommRot, Quaternion.identity, t);

                // 3. Comunidades de fondo
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
            if (grab != null) grab.enabled = true;
        }

        IEnumerator AnimateResetToMiniature()
        {

            XRGrabInteractable grab = _currentFocusedCommunity.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = false;

            foreach (GraphNode comm in _allCommunities)
            {
                SetCommunityFileState(comm, false, false);
            }

            // Lo regresamos al mundo root
            _currentFocusedCommunity.transform.SetParent(_rootNode, true);

            Vector3 startRootPos = _rootNode.localPosition;
            Vector3 startRootScale = _rootNode.localScale;
            Quaternion startRootRot = _rootNode.localRotation;

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

                _rootNode.localPosition = Vector3.Lerp(startRootPos, _originalRootPos, t);
                _rootNode.localScale = Vector3.Lerp(startRootScale, _originalRootScale, t);
                _rootNode.localRotation = Quaternion.Lerp(startRootRot, _originalRootRot, t);

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
            if (grab != null) grab.enabled = true;
        }

        IEnumerator AnimateSwapCommunity(GraphNode newFocusNode)
        {
            GraphNode oldFocusNode = _currentFocusedCommunity;

            XRGrabInteractable grabOld = oldFocusNode.GetComponent<XRGrabInteractable>();
            XRGrabInteractable grabNew = newFocusNode.GetComponent<XRGrabInteractable>();
            if (grabOld != null) grabOld.enabled = false;
            if (grabNew != null) grabNew.enabled = false;

            SetCommunityFileState(oldFocusNode, true, false);
            SetCommunityFileState(newFocusNode, true, true);

            // El viejo regresa al root, el nuevo se ancla a la mesa
            oldFocusNode.transform.SetParent(_rootNode, true);
            newFocusNode.transform.SetParent(tableFocusAnchor, true);

            _currentFocusedCommunity = newFocusNode;

            float timer = 0f;
            Vector3 startOldPos = oldFocusNode.transform.localPosition;
            Vector3 startNewPos = newFocusNode.transform.localPosition;
            Quaternion startNewRot = newFocusNode.transform.localRotation;

            Vector3 oldExpandedTargetPos = oldFocusNode.originalLocalPosition * _radiusExpandFactor;

            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // El viejo foco viaja a su lugar en el root
                oldFocusNode.transform.localPosition = Vector3.Lerp(startOldPos, oldExpandedTargetPos, t);

                // El nuevo foco viaja EXACTAMENTE al centro del Ancla
                newFocusNode.transform.localPosition = Vector3.Lerp(startNewPos, Vector3.zero, t);
                newFocusNode.transform.localRotation = Quaternion.Lerp(startNewRot, Quaternion.identity, t);

                yield return null;
                if (grabOld != null) grabOld.enabled = true;
                if (grabNew != null) grabNew.enabled = true;
            }
        }
    }
}