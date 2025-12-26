using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
// using UnityEngine.XR.Interaction.Toolkit.Interactables; // Descomenta si usas XR Toolkit 3.x

namespace ImmersiveGraph.Visual
{
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Renderer))]
    public class InteractableFeedback : MonoBehaviour
    {
        [Header("Configuración de Brillo")]
        private Color hoverGlowColor = new Color(0.05f, 0.05f, 0.05f); // Brillo apenas perceptible
        private Color grabGlowColor = new Color(0.12f, 0.12f, 0.12f); // Brillo activo pero calmado


        private Renderer _renderer;
        private Material _material;
        private XRGrabInteractable _interactable;

        // Variables de estado
        private bool _isHovered = false;
        private bool _isSelected = false;

        // Variables para la Zona 5
        private bool _isZoneOverride = false;
        private Color _zoneOverrideColor = Color.green;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _interactable = GetComponent<XRGrabInteractable>();

            // Crear una instancia del material para no afectar a todos los prefabs a la vez
            if (_renderer != null)
            {
                _material = _renderer.material;
                // Habilitamos la emisión en el shader estándar
                _material.EnableKeyword("_EMISSION");
            }

            // Suscribirse a eventos XR
            _interactable.hoverEntered.AddListener(OnHoverEnter);
            _interactable.hoverExited.AddListener(OnHoverExit);
            _interactable.selectEntered.AddListener(OnSelectEnter);
            _interactable.selectExited.AddListener(OnSelectExit);
        }

        private void OnDestroy()
        {
            if (_interactable != null)
            {
                _interactable.hoverEntered.RemoveListener(OnHoverEnter);
                _interactable.hoverExited.RemoveListener(OnHoverExit);
                _interactable.selectEntered.RemoveListener(OnSelectEnter);
                _interactable.selectExited.RemoveListener(OnSelectExit);
            }
        }

        // --- EVENTOS XR ---
        private void OnHoverEnter(HoverEnterEventArgs args)
        {
            _isHovered = true;
            UpdateVisuals();
        }

        private void OnHoverExit(HoverExitEventArgs args)
        {
            _isHovered = false;
            UpdateVisuals();
        }

        private void OnSelectEnter(SelectEnterEventArgs args)
        {
            _isSelected = true;
            UpdateVisuals();
        }

        private void OnSelectExit(SelectExitEventArgs args)
        {
            _isSelected = false;
            UpdateVisuals();
        }

        // --- CONTROL DESDE ZONA 5 ---
        public void SetZoneHighlight(bool active, Color color)
        {
            _isZoneOverride = active;
            _zoneOverrideColor = color;
            UpdateVisuals();
        }

        // --- LÓGICA CENTRAL DE COLOR ---
        private void UpdateVisuals()
        {
            if (_material == null) return;

            Color finalEmission = Color.black; // Negro = Apagado

            // Prioridad 1: Zona de Migración (Gana a todo)
            if (_isZoneOverride)
            {
                finalEmission = _zoneOverrideColor;
            }
            // Prioridad 2: Agarrado
            else if (_isSelected)
            {
                finalEmission = grabGlowColor;
            }
            // Prioridad 3: Apuntado (Hover)
            else if (_isHovered)
            {
                finalEmission = hoverGlowColor;
            }

            // Aplicar al shader
            _material.SetColor("_EmissionColor", finalEmission);

            // Si usas el Shader Standard de Unity, esto asegura que la emisión se vea
            if (finalEmission != Color.black)
                DynamicGI.SetEmissive(_renderer, finalEmission);
        }
    }
}