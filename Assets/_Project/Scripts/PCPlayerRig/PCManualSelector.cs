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
        private Camera _mainCamera; // <--- Cacheamos la cámara para evitar errores

        [Header("Configuración de Bloqueo")]
        public LayerMask physicalBlockers = ~0;

        [Header("Configuración de Distancia")]
        public float scrollSpeed = 0.5f;
        public float minDistance = 0.5f;
        public float maxDistance = 5.0f;

        private float _currentDistance;

        void Start()
        {
            _rayInteractor = GetComponent<XRRayInteractor>();

            // 1. BUSCAR MANAGER DE FORMA SEGURA
            if (_rayInteractor.interactionManager != null)
                _manager = _rayInteractor.interactionManager;
            else
                _manager = FindFirstObjectByType<XRInteractionManager>();

            if (_manager == null) Debug.LogError("[PCManualSelector] CRÍTICO: No se encontró XRInteractionManager.");

            // 2. BUSCAR CÁMARA DE FORMA SEGURA
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                // Si falla Camera.main, buscamos en los padres (el FPS Controller tiene cámara)
                _mainCamera = GetComponentInParent<Camera>();
            }

            if (_mainCamera == null) Debug.LogError("[PCManualSelector] CRÍTICO: No se encontró la Cámara Principal. Asegúrate de etiquetarla como 'MainCamera'.");

            // 3. INICIALIZAR DISTANCIA
            if (_rayInteractor.attachTransform != null)
                _currentDistance = _rayInteractor.attachTransform.localPosition.z;
            else
                _currentDistance = 2.0f;
        }

        void Update()
        {
            // Protecciones básicas: Si falta algo vital, no hacemos nada
            if (_manager == null || _rayInteractor == null || _mainCamera == null || Mouse.current == null) return;

            HandleObjectPushPull();

            // --- CLICK DERECHO (ACTIVAR) ---
            HandleRightClickActivation();

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
                    // Protección extra al castear
                    if (_rayInteractor.interactablesSelected.Count > 0)
                    {
                        var interactable = _rayInteractor.interactablesSelected[0] as IXRSelectInteractable;
                        var interactor = _rayInteractor as IXRSelectInteractor;
                        if (interactable != null && interactor != null)
                            _manager.SelectExit(interactor, interactable);
                    }
                }

                if (_currentUIPressed != null) HandleUIRelease();
            }
        }

        // --- LÓGICA DE SCROLL SEGURA ---
        void HandleObjectPushPull()
        {
            if (_rayInteractor.hasSelection)
            {
                // Leer scroll de forma segura
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
            // CHEQUEO 1: ¿La lista es nula?
            if (_rayInteractor.interactablesHovered == null)
            {
                Debug.LogError("[PC ERROR] La lista interactablesHovered es NULA.");
                return false;
            }

            // CHEQUEO 2: ¿Hay elementos?
            if (_rayInteractor.interactablesHovered.Count > 0)
            {
                // CHEQUEO 3: ¿El primer elemento es nulo?
                var rawObject = _rayInteractor.interactablesHovered[0];
                if (rawObject == null)
                {
                    Debug.LogError("[PC ERROR] El objeto Hovered[0] es NULL. (Referencia perdida)");
                    return false;
                }

                Debug.Log($"[PC INFO] Intentando agarrar: {rawObject.transform.name}");

                // Cast seguro
                var interactable = rawObject as IXRSelectInteractable;
                var interactor = _rayInteractor as IXRSelectInteractor;

                // CHEQUEO 4: ¿Falló el cast de interfaces?
                if (interactable == null) Debug.LogError($"[PC ERROR] El objeto {rawObject.transform.name} no tiene IXRSelectInteractable.");
                if (interactor == null) Debug.LogError("[PC ERROR] El RayInteractor no es un IXRSelectInteractor (Imposible pero chequeamos).");

                if (interactable != null && interactor != null)
                {
                    // CHEQUEO 5: ¿El Manager existe?
                    if (_manager == null)
                    {
                        Debug.LogError("[PC ERROR] ¡El XRInteractionManager es NULL justo antes de seleccionar!");
                        return false;
                    }

                    try
                    {
                        Debug.Log("[PC INFO] Llamando a SelectEnter...");
                        _manager.SelectEnter(interactor, interactable);
                        return true;
                    }
                    catch (System.Exception e)
                    {
                        // AQUÍ ATRAPAMOS EL ERROR REAL
                        Debug.LogError($"[PC CRITICAL FAIL] Excepción DENTRO de SelectEnter: {e.Message}\nStack: {e.StackTrace}");
                        return false;
                    }
                }
            }
            return false;
        }

        // --- PROTECCIÓN FÍSICA USANDO EL CENTRO DE LA PANTALLA ---
        bool IsPhysicalObjectBlockingUI()
        {
            // USAMOS EL CENTRO DE LA PANTALLA (0.5, 0.5) EN LUGAR DEL MOUSE
            Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;
            float radius = 0.05f;

            if (Physics.SphereCast(ray, radius, out hit, 100f, physicalBlockers))
            {
                if (hit.collider.isTrigger) return false;

                if (EventSystem.current == null) return false; // Protección contra EventSystem nulo

                PointerEventData pe = new PointerEventData(EventSystem.current);
                // USAMOS EL CENTRO DE LA PANTALLA EN PIXELES
                pe.position = new Vector2(Screen.width / 2f, Screen.height / 2f);

                List<RaycastResult> uiRes = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pe, uiRes);

                if (uiRes.Count > 0)
                {
                    if (hit.distance < uiRes[0].distance - 0.1f) return true;
                }
            }
            return false;
        }

        // --- MANEJO DE UI USANDO EL CENTRO DE LA PANTALLA ---
        void HandleUIPress()
        {
            if (EventSystem.current == null) return;

            _pointerData = new PointerEventData(EventSystem.current);
            // CENTRO DE LA PANTALLA
            _pointerData.position = new Vector2(Screen.width / 2f, Screen.height / 2f);

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
            if (EventSystem.current == null) return;

            // CENTRO DE LA PANTALLA
            _pointerData.position = new Vector2(Screen.width / 2f, Screen.height / 2f);

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(_pointerData, results);
            if (results.Count > 0) _pointerData.pointerCurrentRaycast = results[0];
            ExecuteEvents.Execute(_currentUIPressed, _pointerData, ExecuteEvents.dragHandler);
        }

        void HandleUIRelease()
        {
            if (EventSystem.current == null) return;

            // CENTRO DE LA PANTALLA
            _pointerData.position = new Vector2(Screen.width / 2f, Screen.height / 2f);

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(_pointerData, results);
            if (results.Count > 0) _pointerData.pointerCurrentRaycast = results[0];
            ExecuteEvents.Execute(_currentUIPressed, _pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(_currentUIPressed, _pointerData, ExecuteEvents.pointerClickHandler);
            _currentUIPressed = null;
            _pointerData = null;
        }

        // --- LÓGICA DE ACTIVACIÓN ---
        void HandleRightClickActivation()
        {
            if (_rayInteractor.hasSelection && _rayInteractor.interactablesSelected.Count > 0)
            {
                var interactableObject = _rayInteractor.interactablesSelected[0];
                var activatable = interactableObject as IXRActivateInteractable;

                if (activatable != null)
                {
                    if (Mouse.current.rightButton.wasPressedThisFrame)
                    {
                        var args = new ActivateEventArgs { interactorObject = _rayInteractor, interactableObject = activatable };
                        activatable.OnActivated(args);
                    }
                    if (Mouse.current.rightButton.wasReleasedThisFrame)
                    {
                        var args = new DeactivateEventArgs { interactorObject = _rayInteractor, interactableObject = activatable };
                        activatable.OnDeactivated(args);
                    }
                }
            }
        }
    }
}