using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;
using ImmersiveGraph.Data;

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    [RequireComponent(typeof(SphereCollider))]
    public class GraphNode : MonoBehaviour
    {
        [Header("Datos del Nodo")]
        public string nodeType;
        public NodeData myData;

        [Header("Relaciones")]
        public List<GameObject> childNodes = new List<GameObject>();
        public List<GameObject> connectionLines = new List<GameObject>();

        private bool _isExpanded = false;
        private XRSimpleInteractable _interactable;

        // Variables para el Feedback Visual
        private Renderer _renderer;
        private Color _originalColor;
        private Color _hoverColor;
        private bool _isInitialized = false;

        void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            _renderer = GetComponent<Renderer>();
        }

        void OnEnable()
        {
            if (_interactable != null)
            {
                // Evento: GATILLO (Click)
                _interactable.selectEntered.AddListener(OnNodeSelected);

                // Evento: MIRAR (Hover - El rayo toca el objeto)
                _interactable.hoverEntered.AddListener(OnHoverEnter);

                // Evento: DEJAR DE MIRAR (Hover Exit - El rayo sale)
                _interactable.hoverExited.AddListener(OnHoverExit);
            }
        }

        void OnDisable()
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.RemoveListener(OnNodeSelected);
                _interactable.hoverEntered.RemoveListener(OnHoverEnter);
                _interactable.hoverExited.RemoveListener(OnHoverExit);
            }
        }

        // Se llama desde el Spawner cuando ya tiene color y datos
        public void InitializeNode()
        {
            // 1. Guardar el color que nos puso el Spawner
            if (_renderer != null)
            {
                _originalColor = _renderer.material.color;

                // Calculamos un color más brillante para el Hover (Mezcla con blanco al 50%)
                _hoverColor = Color.Lerp(_originalColor, Color.white, 0.4f);
                // Opcional: Si quieres un borde luminoso real, necesitarías cambiar el shader a Emission,
                // pero cambiar el color es la forma más barata y efectiva por ahora.
            }

            // 2. Estado inicial según tipo
            if (nodeType == "community")
            {
                _isExpanded = false;
                SetChildrenVisibility(false);
            }

            _isInitialized = true;
        }

        // --- FEEDBACK VISUAL (Estado Mirar) ---
        void OnHoverEnter(HoverEnterEventArgs args)
        {
            if (!_isInitialized) InitializeNode(); // Por seguridad

            // Iluminar la esfera
            if (_renderer != null) _renderer.material.color = _hoverColor;

            // Aquí en el futuro mostraremos el Panel UI, pero por ahora solo color
            Debug.Log($"HOVER ENTRADA: {name}");
        }

        void OnHoverExit(HoverExitEventArgs args)
        {
            // Volver al color original
            if (_renderer != null) _renderer.material.color = _originalColor;
        }

        // --- INTERACCIÓN (Estado Seleccionar) ---
        void OnNodeSelected(SelectEnterEventArgs args)
        {
            Debug.Log($"CLICK CONFIRMADO EN: {name}");

            if (nodeType == "community")
            {
                _isExpanded = !_isExpanded;
                SetChildrenVisibility(_isExpanded);
            }
            else if (nodeType == "file")
            {
                Debug.Log($"ABRIENDO ARCHIVO: {myData.title}");
                // Lógica futura para Zone 3
            }
        }

        void SetChildrenVisibility(bool state)
        {
            foreach (var child in childNodes)
            {
                if (child != null) child.SetActive(state);
            }
            foreach (var line in connectionLines)
            {
                if (line != null) line.SetActive(state);
            }
        }
    }
}