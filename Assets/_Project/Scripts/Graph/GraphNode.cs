using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Unity 6
using System.Collections.Generic;
using ImmersiveGraph.Data;
using ImmersiveGraph.Visual; // Para acceder al NodeUIController

namespace ImmersiveGraph.Interaction
{
    // CAMBIO: Ahora usamos GrabInteractable para poder mover el objeto
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(SphereCollider))]
    public class GraphNode : MonoBehaviour
    {
        [Header("Datos del Nodo")]
        public string nodeType;
        public NodeData myData;

        [Header("Relaciones Visuales")]
        // Referencia al nodo PADRE (de quien colgamos) para dibujar la línea
        public Transform parentNodeTransform;
        // La línea que me conecta con mi padre (Yo controlo esta línea)
        public LineRenderer incomingLine;

        // Mis hijos (para expandir/colapsar)
        public List<GameObject> childNodes = new List<GameObject>();
        // Las líneas hacia mis hijos (No las controlo yo, las controlan ellos, pero necesito la lista para ocultarlas)
        public List<GameObject> childConnectionLines = new List<GameObject>();

        [Header("Referencias Internas")]
        public NodeUIController uiController; // El script del UI Flotante

        // --- LÓGICA DE INTERACCIÓN ---
        private XRGrabInteractable _interactable;
        private Renderer _renderer;
        private Color _originalColor;
        private Color _hoverColor;

        // Lógica de Hold (4 segundos)
        private bool _isGrabbing = false;
        private float _holdTimer = 0f;
        private float _activationTime = 4.0f; // TIEMPO DE CARGA
        private bool _hasActivated = false;

        private bool _isExpanded = false;

        void Awake()
        {
            _interactable = GetComponent<XRGrabInteractable>();
            _renderer = GetComponent<Renderer>();

            // Configuración física para mover objetos en VR sin gravedad
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true; // Importante para que no salga volando al soltarlo

            // Configurar Interactable
            _interactable.movementType = XRBaseInteractable.MovementType.Kinematic;
            // IMPORTANTE: Evitar que al agarrar se "desparente" completamente de la lógica
            // Aunque XRI suele cambiar el padre al agarrar, guardaremos la referencia visual.
        }

        void OnEnable()
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.AddListener(OnGrabStart);
                _interactable.selectExited.AddListener(OnGrabEnd);
                _interactable.hoverEntered.AddListener(OnHoverEnter);
                _interactable.hoverExited.AddListener(OnHoverExit);
            }
        }

        void OnDisable()
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.RemoveListener(OnGrabStart);
                _interactable.selectExited.RemoveListener(OnGrabEnd);
                _interactable.hoverEntered.RemoveListener(OnHoverEnter);
                _interactable.hoverExited.RemoveListener(OnHoverExit);
            }
        }

        public void InitializeNode(Transform parent, LineRenderer lineFromParent)
        {
            // Guardar referencias para el dibujo de líneas
            parentNodeTransform = parent;
            incomingLine = lineFromParent;

            if (_renderer != null)
            {
                _originalColor = _renderer.material.color;
                _hoverColor = Color.Lerp(_originalColor, Color.white, 0.4f);
            }

            if (nodeType == "community")
            {
                _isExpanded = false;
                SetChildrenVisibility(false);
            }
        }

        void Update()
        {
            // 1. ACTUALIZAR LÍNEAS (Si me muevo, la línea debe seguirme)
            if (incomingLine != null && parentNodeTransform != null)
            {
                // Punto 0: Mi padre. Punto 1: Yo.
                incomingLine.SetPosition(0, parentNodeTransform.position);
                incomingLine.SetPosition(1, transform.position);
            }

            // 2. LÓGICA DE CARGA (HOLD TO ACTIVATE)
            if (_isGrabbing && !_hasActivated)
            {
                _holdTimer += Time.deltaTime;

                // Actualizar UI
                float progress = _holdTimer / _activationTime;
                if (uiController != null) uiController.UpdateLoader(progress);

                // CHEQUEO DE ÉXITO
                if (_holdTimer >= _activationTime)
                {
                    ExecuteActivation();
                }
            }
        }

        void OnGrabStart(SelectEnterEventArgs args)
        {
            _isGrabbing = true;
            _holdTimer = 0f;
            _hasActivated = false;
            Debug.Log($"Agarrando {name}. Iniciando carga...");
        }

        void OnGrabEnd(SelectExitEventArgs args)
        {
            _isGrabbing = false;
            _holdTimer = 0f;

            // Resetear UI
            if (uiController != null) uiController.UpdateLoader(0);
        }

        // --- ESTA ES LA FUNCIÓN QUE SE EJECUTA A LOS 4 SEGUNDOS ---
        void ExecuteActivation()
        {
            _hasActivated = true;
            Debug.Log("¡ACTIVACIÓN COMPLETADA!");

            // Feedback visual de éxito (opcional: vibración, sonido)
            if (uiController != null) uiController.UpdateLoader(1f); // Barra llena

            // Lógica según tipo
            if (nodeType == "community")
            {
                _isExpanded = !_isExpanded;
                SetChildrenVisibility(_isExpanded);

                // También enviamos a Zone 3
                SendToZone3();
            }
            else if (nodeType == "file")
            {
                SendToZone3();
            }
        }

        void SendToZone3()
        {
            Debug.Log($"--> ENVIANDO {myData.title} A ZONE 3 (Lector de Documentos)");
            // Aquí conectarás la lógica futura
        }

        void SetChildrenVisibility(bool state)
        {
            foreach (var child in childNodes)
            {
                if (child != null) child.SetActive(state);
            }
            // Ocultar las líneas que salen de mí hacia mis hijos
            foreach (var line in childConnectionLines)
            {
                if (line != null) line.SetActive(state);
            }
        }

        // Feedback Hover Simple
        void OnHoverEnter(HoverEnterEventArgs args)
        {
            if (_renderer != null) _renderer.material.color = _hoverColor;
        }
        void OnHoverExit(HoverExitEventArgs args)
        {
            if (_renderer != null) _renderer.material.color = _originalColor;
        }
    }
}