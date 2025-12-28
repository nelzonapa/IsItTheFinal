using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ImmersiveGraph.Core
{
    [RequireComponent(typeof(XRRayInteractor))]
    public class PCManualSelector : MonoBehaviour
    {
        private XRRayInteractor _rayInteractor;
        private XRInteractionManager _manager;

        private GameObject _currentUIPressed;
        private PointerEventData _pointerData;

        [Header("Configuración de Bloqueo")]
        public LayerMask physicalBlockers = ~0;

        [Header("Configuración de Distancia")]
        [Tooltip("Velocidad al acercar/alejar con la rueda")]
        public float scrollSpeed = 0.5f;
        [Tooltip("Distancia mínima al ojo (en metros)")]
        public float minDistance = 0.5f;
        [Tooltip("Distancia máxima (en metros)")]
        public float maxDistance = 5.0f;

        private float _currentDistance;

        void Start()
        {
            _rayInteractor = GetComponent<XRRayInteractor>();

            if (_rayInteractor.interactionManager != null)
                _manager = _rayInteractor.interactionManager;
            else
                _manager = FindFirstObjectByType<XRInteractionManager>();

            if (_rayInteractor.attachTransform != null)
                _currentDistance = _rayInteractor.attachTransform.localPosition.z;
            else
                _currentDistance = 2.0f;
        }

        void Update()
        {
            HandleObjectPushPull();

            if (_manager == null || _rayInteractor == null) return;

            // --- CLICK DERECHO (ACTIVAR / DISPARAR) ---
            HandleRightClickActivation();
            // ------------------------------------------

            // FASE 1: CLICK IZQUIERDO (AGARRAR)
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (TryGrabXRI())
                {
                    if (_rayInteractor.attachTransform != null)
                        _currentDistance = _rayInteractor.attachTransform.localPosition.z;
                    return;
                }
                if (IsPhysicalObjectBlockingUI()) return;
                HandleUIPress();
            }

            // FASE 2: ARRASTRAR
            if (Mouse.current.leftButton.isPressed)
            {
                if (_currentUIPressed != null) HandleUIDrag();
            }

            // FASE 3: SOLTAR
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (_rayInteractor.hasSelection)
                {
                    var interactable = _rayInteractor.interactablesSelected[0] as IXRSelectInteractable;
                    var interactor = _rayInteractor as IXRSelectInteractor;
                    if (interactable != null && interactor != null)
                        _manager.SelectExit(interactor, interactable);
                }

                if (_currentUIPressed != null) HandleUIRelease();
            }
        }

        // --- SOLUCIÓN AL ERROR DE COMPILACIÓN ---
        void HandleRightClickActivation()
        {
            // Solo si tenemos algo agarrado
            if (_rayInteractor.hasSelection)
            {
                // Obtenemos el objeto agarrado
                var interactableObject = _rayInteractor.interactablesSelected[0];

                // Verificamos si es "Activable" (IXRActivateInteractable)
                var activatable = interactableObject as IXRActivateInteractable;

                if (activatable != null)
                {
                    // 1. PRESIONAR CLIC DERECHO -> ACTIVAR
                    if (Mouse.current.rightButton.wasPressedThisFrame)
                    {
                        // Creamos los argumentos del evento manualmente
                        var args = new ActivateEventArgs
                        {
                            interactorObject = _rayInteractor,
                            interactableObject = activatable
                        };
                        // Disparamos el evento directamente en el objeto
                        activatable.OnActivated(args);
                    }

                    // 2. SOLTAR CLIC DERECHO -> DESACTIVAR
                    if (Mouse.current.rightButton.wasReleasedThisFrame)
                    {
                        var args = new DeactivateEventArgs
                        {
                            interactorObject = _rayInteractor,
                            interactableObject = activatable
                        };
                        activatable.OnDeactivated(args);
                    }
                }
            }
        }

        // --- FUNCIONES EXISTENTES (SIN CAMBIOS) ---
        void HandleObjectPushPull()
        {
            if (_rayInteractor.hasSelection)
            {
                float rawScroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(rawScroll) > 0.001f)
                {
                    float direction = Mathf.Sign(rawScroll);
                    float moveAmount = direction * scrollSpeed * Time.deltaTime * 20.0f;

                    _currentDistance += moveAmount;
                    _currentDistance = Mathf.Clamp(_currentDistance, minDistance, maxDistance);

                    if (_rayInteractor.attachTransform != null)
                    {
                        Vector3 newPos = _rayInteractor.attachTransform.localPosition;
                        newPos.x = 0; newPos.y = 0; newPos.z = _currentDistance;
                        _rayInteractor.attachTransform.localPosition = newPos;
                    }
                }
            }
            else if (_rayInteractor.attachTransform != null)
            {
                _currentDistance = _rayInteractor.attachTransform.localPosition.z;
            }
        }

        bool TryGrabXRI()
        {
            if (_rayInteractor.interactablesHovered.Count > 0)
            {
                var interactable = _rayInteractor.interactablesHovered[0] as IXRSelectInteractable;
                var interactor = _rayInteractor as IXRSelectInteractor;

                if (interactable != null && interactor != null)
                {
                    _manager.SelectEnter(interactor, interactable);
                    return true;
                }
            }
            return false;
        }

        bool IsPhysicalObjectBlockingUI()
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;
            float radius = 0.05f;

            if (Physics.SphereCast(ray, radius, out hit, 100f, physicalBlockers))
            {
                if (hit.collider.isTrigger) return false;
                PointerEventData pe = new PointerEventData(EventSystem.current);
                pe.position = Mouse.current.position.ReadValue();
                List<RaycastResult> uiRes = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pe, uiRes);
                if (uiRes.Count > 0)
                {
                    if (hit.distance < uiRes[0].distance - 0.1f) return true;
                }
            }
            return false;
        }

        void HandleUIPress()
        {
            _pointerData = new PointerEventData(EventSystem.current);
            _pointerData.position = Mouse.current.position.ReadValue();
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(_pointerData, results);

            if (results.Count > 0)
            {
                GameObject target = results[0].gameObject;
                _pointerData.pointerCurrentRaycast = results[0];
                _pointerData.position = results[0].screenPosition;
                ExecuteEvents.Execute(target, _pointerData, ExecuteEvents.pointerDownHandler);
                _currentUIPressed = target;
                ExecuteEvents.Execute(target, _pointerData, ExecuteEvents.initializePotentialDrag);
            }
        }

        void HandleUIDrag()
        {
            _pointerData.position = Mouse.current.position.ReadValue();
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(_pointerData, results);
            if (results.Count > 0) _pointerData.pointerCurrentRaycast = results[0];
            ExecuteEvents.Execute(_currentUIPressed, _pointerData, ExecuteEvents.dragHandler);
        }

        void HandleUIRelease()
        {
            _pointerData.position = Mouse.current.position.ReadValue();
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(_pointerData, results);
            if (results.Count > 0) _pointerData.pointerCurrentRaycast = results[0];
            ExecuteEvents.Execute(_currentUIPressed, _pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(_currentUIPressed, _pointerData, ExecuteEvents.pointerClickHandler);
            _currentUIPressed = null;
            _pointerData = null;
        }
    }
}