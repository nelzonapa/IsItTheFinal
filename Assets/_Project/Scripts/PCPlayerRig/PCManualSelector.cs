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
        public LayerMask physicalBlockers = ~0; // Todo

        void Start()
        {
            _rayInteractor = GetComponent<XRRayInteractor>();

            // Búsqueda a prueba de fallos del Manager
            if (_rayInteractor.interactionManager != null)
                _manager = _rayInteractor.interactionManager;
            else
                _manager = FindFirstObjectByType<XRInteractionManager>();
        }

        void Update()
        {
            if (_manager == null || _rayInteractor == null) return;

            // --- FASE 1: CLICK DOWN ---
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // 1. Prioridad ABSOLUTA: Agarrar objeto 3D
                if (TryGrabXRI())
                {
                    Debug.Log("[PC] Objeto 3D agarrado. UI Bloqueada.");
                    return;
                }

                // 2. Si no agarramos nada, revisamos si un objeto físico nos bloquea la visión
                // (Por si el XRI falló en detectarlo pero está ahí visualmente)
                if (IsPhysicalObjectBlockingUI())
                {
                    Debug.Log("[PC] Clic UI cancelado: Objeto físico enfrente.");
                    return;
                }

                // 3. Si llegamos aquí, está libre para tocar UI
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
                // Soltar 3D
                if (_rayInteractor.hasSelection)
                {
                    var interactable = _rayInteractor.interactablesSelected[0] as IXRSelectInteractable;
                    var interactor = _rayInteractor as IXRSelectInteractor;
                    if (interactable != null && interactor != null)
                        _manager.SelectExit(interactor, interactable);
                }

                // Soltar UI
                if (_currentUIPressed != null) HandleUIRelease();
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
            // Usamos SphereCast (Rayo Gordo) para detectar mejor los objetos finos
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;
            float radius = 0.05f; // Radio de 5cm (como un dedo)

            if (Physics.SphereCast(ray, radius, out hit, 100f, physicalBlockers))
            {
                // Ignorar Triggers (Zonas)
                if (hit.collider.isTrigger) return false;

                // Verificar distancia a la UI
                // Lanzamos un raycast UI para saber a qué distancia está el canvas
                PointerEventData pe = new PointerEventData(EventSystem.current);
                pe.position = Mouse.current.position.ReadValue();
                List<RaycastResult> uiRes = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pe, uiRes);

                if (uiRes.Count > 0)
                {
                    // Si el objeto físico está más cerca que la UI... ¡BLOQUEO!
                    if (hit.distance < uiRes[0].distance - 0.1f) // Margen de 10cm
                        return true;
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
            // Recalcular raycast final es importante para eventos Click
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