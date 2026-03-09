using ImmersiveGraph.Visual;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ImmersiveGraph.Interaction
{
    public class GraphInteractionManager : MonoBehaviour
    {
        [Header("Metáfora Detail (La Mesa)")]
        [Tooltip("Punto exacto (Objeto Vacío) en la mesa donde se analiza la comunidad elegida.")]
        public Transform tableFocusAnchor;

        [Header("Animación")]
        public float animationDuration = 0.8f;

        // ESTADO
        private Transform _rootNode;
        private GraphNode _currentFocusedCommunity;
        private bool _isExpandedMode = false;

        private List<GraphNode> _allCommunities = new List<GraphNode>();

        // Lista para proteger los nodos de la física mientras se están animando
        private List<GraphNode> _animatingNodes = new List<GraphNode>();

        public void InitializeGraph(Transform rootNode, float minRadius, float expRadius)
        {
            _rootNode = rootNode;
            _isExpandedMode = false;
            _currentFocusedCommunity = null;
            _animatingNodes.Clear();
            _allCommunities.Clear();

            foreach (Transform child in _rootNode)
            {
                GraphNode node = child.GetComponent<GraphNode>();
                if (node != null && node.nodeType == "community")
                {
                    _allCommunities.Add(node);
                    // Inicialmente apagamos los archivos por completo
                    SetCommunityFileState(node, false, false);
                }
            }
        }

        public bool IsNodeAnimating(GraphNode node)
        {
            return _animatingNodes.Contains(node);
        }

        public void OnCommunityHoldActivated(GraphNode selectedNode)
        {
            XRGrabInteractable grab = selectedNode.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected)
            {
                grab.interactionManager.SelectExit(grab.firstInteractorSelecting, grab);
            }

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

            if (community.childNodes != null)
            {
                foreach (GameObject fileObj in community.childNodes)
                {
                    if (fileObj == null) continue;

                    // Encendemos o apagamos el objeto contenedor
                    fileObj.SetActive(showFiles);

                    if (!showFiles) continue; // Si está apagado, no calculamos el resto

                    Collider col = fileObj.GetComponent<Collider>();
                    if (col != null) col.enabled = isInteractive;

                    Canvas[] uis = fileObj.GetComponentsInChildren<Canvas>(true);
                    foreach (Canvas ui in uis)
                    {
                        ui.gameObject.SetActive(isInteractive);
                    }

                    // Magia de Transparencia para los "Fantasmas"
                    Renderer[] renderers = fileObj.GetComponentsInChildren<Renderer>();
                    foreach (Renderer ren in renderers)
                    {
                        if (ren.material.HasProperty("_Color"))
                        {
                            Color c = ren.material.color;
                            c.a = isInteractive ? 1.0f : 0.2f; // Transparente si es ficticio
                            ren.material.color = c;

                            if (!isInteractive)
                            {
                                ren.material.SetFloat("_Mode", 3); // Transparent
                                ren.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                                ren.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                                ren.material.SetInt("_ZWrite", 0);
                                ren.material.DisableKeyword("_ALPHATEST_ON");
                                ren.material.EnableKeyword("_ALPHABLEND_ON");
                                ren.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                                ren.material.renderQueue = 3000;
                            }
                            else
                            {
                                ren.material.SetFloat("_Mode", 0); // Opaque
                                ren.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                                ren.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                                ren.material.SetInt("_ZWrite", 1);
                                ren.material.DisableKeyword("_ALPHATEST_ON");
                                ren.material.DisableKeyword("_ALPHABLEND_ON");
                                ren.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                                ren.material.renderQueue = -1;
                            }
                        }
                    }
                }
            }
        }

        IEnumerator AnimateFocusToTable(GraphNode targetCommunity)
        {
            _isExpandedMode = true;
            _currentFocusedCommunity = targetCommunity;
            _animatingNodes.Add(targetCommunity);

            XRGrabInteractable grab = targetCommunity.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = false;

            // ASEGURAR QUE ESTÉN APAGADOS DURANTE EL VIAJE
            SetCommunityFileState(targetCommunity, false, false);

            targetCommunity.transform.SetParent(tableFocusAnchor, true);

            Vector3 startCommPos = targetCommunity.transform.localPosition;
            Quaternion startCommRot = targetCommunity.transform.localRotation;

            float timer = 0f;
            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // Viaja hacia la mesa
                targetCommunity.transform.localPosition = Vector3.Lerp(startCommPos, Vector3.zero, t);
                targetCommunity.transform.localRotation = Quaternion.Lerp(startCommRot, Quaternion.identity, t);

                yield return null;
            }

            // --- RECIÉN AL LLEGAR AL FOCO, ENCENDEMOS LOS ARCHIVOS ---
            SetCommunityFileState(targetCommunity, true, true);

            if (grab != null) grab.enabled = true;
            _animatingNodes.Remove(targetCommunity);
        }

        IEnumerator AnimateResetToMiniature()
        {
            GraphNode targetCommunity = _currentFocusedCommunity;
            _animatingNodes.Add(targetCommunity);

            XRGrabInteractable grab = targetCommunity.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = false;

            // --- APAGAR INMEDIATAMENTE TODOS LOS ARCHIVOS ANTES DE REGRESAR ---
            SetCommunityFileState(targetCommunity, false, false);

            targetCommunity.transform.SetParent(_rootNode, true);

            Vector3 startCommPos = targetCommunity.transform.localPosition;
            Quaternion startCommRot = targetCommunity.transform.localRotation;

            float timer = 0f;
            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // Regresa a su sitio original en el root
                targetCommunity.transform.localPosition = Vector3.Lerp(startCommPos, targetCommunity.originalLocalPosition, t);
                targetCommunity.transform.localRotation = Quaternion.Lerp(startCommRot, Quaternion.identity, t);

                yield return null;
            }

            _isExpandedMode = false;
            _currentFocusedCommunity = null;
            if (grab != null) grab.enabled = true;
            _animatingNodes.Remove(targetCommunity);
        }

        IEnumerator AnimateSwapCommunity(GraphNode newFocusNode)
        {
            GraphNode oldFocusNode = _currentFocusedCommunity;
            _animatingNodes.Add(oldFocusNode);
            _animatingNodes.Add(newFocusNode);

            XRGrabInteractable grabOld = oldFocusNode.GetComponent<XRGrabInteractable>();
            XRGrabInteractable grabNew = newFocusNode.GetComponent<XRGrabInteractable>();
            if (grabOld != null) grabOld.enabled = false;
            if (grabNew != null) grabNew.enabled = false;

            // --- APAGAR LOS ARCHIVOS DEL VIEJO INMEDIATAMENTE Y MANTENER APAGADO AL NUEVO ---
            SetCommunityFileState(oldFocusNode, false, false);
            SetCommunityFileState(newFocusNode, false, false);

            oldFocusNode.transform.SetParent(_rootNode, true);
            newFocusNode.transform.SetParent(tableFocusAnchor, true);

            _currentFocusedCommunity = newFocusNode;

            Vector3 startOldPos = oldFocusNode.transform.localPosition;
            Vector3 startNewPos = newFocusNode.transform.localPosition;
            Quaternion startNewRot = newFocusNode.transform.localRotation;

            float timer = 0f;
            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, timer / animationDuration);

                // Viejo regresa a su punto base
                oldFocusNode.transform.localPosition = Vector3.Lerp(startOldPos, oldFocusNode.originalLocalPosition, t);

                // Nuevo viene a la mesa
                newFocusNode.transform.localPosition = Vector3.Lerp(startNewPos, Vector3.zero, t);
                newFocusNode.transform.localRotation = Quaternion.Lerp(startNewRot, Quaternion.identity, t);

                yield return null;
            }

            // --- RECIÉN AL LLEGAR AL FOCO, ENCENDEMOS LOS ARCHIVOS DEL NUEVO ---
            SetCommunityFileState(newFocusNode, true, true);

            if (grabOld != null) grabOld.enabled = true;
            if (grabNew != null) grabNew.enabled = true;
            _animatingNodes.Remove(oldFocusNode);
            _animatingNodes.Remove(newFocusNode);
        }
    }
}