using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // <--- NECESARIO PARA UI
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

        void Start()
        {
            _rayInteractor = GetComponent<XRRayInteractor>();

            if (_rayInteractor.interactionManager != null)
                _manager = _rayInteractor.interactionManager;
            else
                _manager = FindFirstObjectByType<XRInteractionManager>();
        }

        void Update()
        {
            // --- PARTE A: CLIC EN UI (WelcomeCanvas, Botones 2D) ---
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // 1. Preguntamos al EventSystem si el mouse está sobre algo de UI
                if (IsPointerOverUI())
                {
                    // Si estamos sobre UI, no hacemos nada más aquí, dejamos que Unity lo maneje 
                    // PERO, si Unity no lo maneja porque el Input Module está raro en PC, lo forzamos:
                    ForceUIClick();
                    return; // Importante: Si tocamos UI, no intentamos agarrar Tokens a la vez
                }
            }

            if (_manager == null || _rayInteractor == null) return;

            // --- PARTE B: AGARRE DE OBJETOS 3D (Tokens, Nodos) ---
            // (Esta es la parte que ya te funcionaba)
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (_rayInteractor.interactablesHovered.Count > 0)
                {
                    var target = _rayInteractor.interactablesHovered[0] as IXRSelectInteractable;
                    if (target != null)
                    {
                        _manager.SelectEnter(_rayInteractor as IXRSelectInteractor, target);
                    }
                }
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (_rayInteractor.hasSelection)
                {
                    var target = _rayInteractor.interactablesSelected[0];
                    _manager.SelectExit(_rayInteractor as IXRSelectInteractor, target);
                }
            }
        }

        // --- FUNCIONES MÁGICAS PARA UI ---
        private bool IsPointerOverUI()
        {
            // Método estándar para saber si el mouse toca un Canvas
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void ForceUIClick()
        {
            // Creamos un evento de puntero falso en la posición del mouse
            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = Mouse.current.position.ReadValue();

            // Lanzamos un rayo de UI
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (RaycastResult result in results)
            {
                // Buscamos si tocamos un botón y le hacemos CLICK forzado
                var button = result.gameObject.GetComponentInParent<UnityEngine.UI.Button>();
                if (button != null && button.interactable)
                {
                    button.onClick.Invoke(); // <--- ¡HACER CLICK AHORA!
                    Debug.Log($"[PC] Click forzado en botón UI: {button.name}");
                    break; // Solo un botón a la vez
                }
            }
        }
    }
}