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

        // Para recordar la distancia original
        private float _currentDistance;

        void Start()
        {
            _rayInteractor = GetComponent<XRRayInteractor>();

            if (_rayInteractor.interactionManager != null)
                _manager = _rayInteractor.interactionManager;
            else
                _manager = FindFirstObjectByType<XRInteractionManager>();

            // Inicializar distancia por defecto (si ya hay un AttachTransform puesto)
            if (_rayInteractor.attachTransform != null)
                _currentDistance = _rayInteractor.attachTransform.localPosition.z;
            else
                _currentDistance = 2.0f; // Valor seguro por defecto
        }

        void Update()
        {
            // --- NUEVO: LÓGICA DE ACERCAR/ALEJAR (SCROLL) ---
            HandleObjectPushPull();
            // ------------------------------------------------

            if (_manager == null || _rayInteractor == null) return;

            // --- FASE 1: CLICK DOWN ---
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // 1. Prioridad: Agarrar 3D
                if (TryGrabXRI())
                {
                    // Al agarrar, reseteamos la distancia al valor actual del AttachPoint
                    // Opcional: Podríamos calcular la distancia al objeto real para que no salte
                    if (_rayInteractor.attachTransform != null)
                        _currentDistance = _rayInteractor.attachTransform.localPosition.z;

                    return;
                }

                // 2. Verificar bloqueo físico
                if (IsPhysicalObjectBlockingUI()) return;

                // 3. UI
                HandleUIPress();
            }

            // --- FASE 2: ARRASTRAR ---
            if (Mouse.current.leftButton.isPressed)
            {
                if (_currentUIPressed != null) HandleUIDrag();
            }

            // --- FASE 3: SOLTAR ---
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (_rayInteractor.hasSelection)
                {
                    var interactable = _rayInteractor.interactablesSelected[0] as IXRSelectInteractable;
                    var interactor = _rayInteractor as IXRSelectInteractor;
                    if (interactable != null && interactor != null)
                        _manager.SelectExit(interactor, interactable);

                    // Opcional: Resetear la distancia al soltar para que el rayo no se quede corto/largo
                    // ResetAttachDistance(); 
                }

                if (_currentUIPressed != null) HandleUIRelease();
            }
        }

        // --- LÓGICA DE MOVIMIENTO DE OBJETO ---
        // --- LÓGICA DE MOVIMIENTO DE OBJETO MEJORADA ---
        void HandleObjectPushPull()
        {
            // Solo funciona si tenemos algo agarrado (hasSelection)
            if (_rayInteractor.hasSelection)
            {
                // Leemos el valor crudo del scroll
                float rawScroll = Mouse.current.scroll.ReadValue().y;

                // Verificamos si hay movimiento (usamos un umbral muy bajo por si tu mouse es sensible)
                if (Mathf.Abs(rawScroll) > 0.001f)
                {
                    // DEBUG: Descomenta esto para ver si Unity recibe señal
                    // Debug.Log($"[PC] Scroll detectado: {rawScroll}");

                    // Usamos Mathf.Sign para obtener solo la dirección (1 o -1)
                    // Esto arregla el problema de si tu mouse manda 120 o manda 1.
                    float direction = Mathf.Sign(rawScroll);

                    // Calculamos el movimiento
                    // Aumenté el multiplicador a 2.0f para que se note más
                    float moveAmount = direction * scrollSpeed * Time.deltaTime * 20.0f;

                    _currentDistance += moveAmount;

                    // Limitamos la distancia
                    _currentDistance = Mathf.Clamp(_currentDistance, minDistance, maxDistance);

                    // APLICACIÓN AL TRANSFORM
                    if (_rayInteractor.attachTransform != null)
                    {
                        Vector3 newPos = _rayInteractor.attachTransform.localPosition;

                        // IMPORTANTE: Aseguramos que solo movemos Z, manteniendo X e Y en 0
                        newPos.x = 0;
                        newPos.y = 0;
                        newPos.z = _currentDistance;

                        _rayInteractor.attachTransform.localPosition = newPos;
                    }
                    else
                    {
                        Debug.LogError("[PC] ¡Error! Attach Transform no está asignado en el Inspector.");
                    }
                }
            }
            // Si no hay selección, reseteamos la distancia lógica para que no se desincronice
            else if (_rayInteractor.attachTransform != null)
            {
                // Actualizamos la variable interna a la posición real del AttachPoint
                _currentDistance = _rayInteractor.attachTransform.localPosition.z;
            }
        }

        // (Opcional) Resetea el rayo a una distancia cómoda
        void ResetAttachDistance()
        {
            _currentDistance = 2.0f; // Distancia default
            if (_rayInteractor.attachTransform != null)
            {
                Vector3 newPos = _rayInteractor.attachTransform.localPosition;
                newPos.z = _currentDistance;
                _rayInteractor.attachTransform.localPosition = newPos;
            }
        }

        // --- RESTO DE FUNCIONES (IGUAL QUE ANTES) ---
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