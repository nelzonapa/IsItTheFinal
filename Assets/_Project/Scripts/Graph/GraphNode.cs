using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Asegúrate de tener esto si usas Unity 6
using System.Collections.Generic;
using ImmersiveGraph.Data;
using ImmersiveGraph.Visual;
using ImmersiveGraph.Core;

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(AudioSource))]
    public class GraphNode : MonoBehaviour
    {
        [Header("Datos")]
        public string nodeType;
        public NodeData myData;

        [Header("Relaciones")]
        public Transform parentNodeTransform;
        public LineRenderer incomingLine;
        public List<GameObject> childNodes = new List<GameObject>();
        public List<GameObject> childConnectionLines = new List<GameObject>();

        [Header("Referencias Externas")]
        public Zone3Manager localZone3Manager;
        public GraphInteractionManager interactionManager; // <--- NUEVO

        [Header("Referencias UI")]
        public NodeLoaderController loaderUI;

        // --- VARIABLES PÚBLICAS PARA RECIBIR CONFIGURACIÓN ---
        public GameObject reviewedMarkerPrefab;
        public Vector3 markerLocalOffset;
        public Vector3 markerLocalScale;

        // --- MEMORIA DE POSICIÓN (NUEVO) ---
        [HideInInspector] public Vector3 originalLocalPosition; // Para saber volver a casa

        // Lógica Interna
        private XRGrabInteractable _interactable;
        private Renderer _renderer;
        private Color _originalColor;
        private Color _hoverColor;

        private bool _isGrabbing = false;
        private float _holdTimer = 0f;
        private float _activationTime = 4.0f; // 4 segundos para activar la animación
        private bool _hasActivated = false;
        private bool _isExpanded = false;

        private bool _isReviewed = false;

        public AudioClip expandSound;
        private AudioSource _audioSource;

        void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1.0f;
            _audioSource.playOnAwake = false;

            _interactable = GetComponent<XRGrabInteractable>();
            _renderer = GetComponent<Renderer>();

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            // Importante para XR Toolkit moderno
            if (_interactable != null) _interactable.movementType = XRBaseInteractable.MovementType.Kinematic;
        }

        void OnEnable()
        {
            if (_interactable != null)
            {
                // Usamos "Select" para el Click/Grab
                _interactable.selectEntered.AddListener(OnSelectStart);
                _interactable.selectExited.AddListener(OnSelectEnd);

                // Hover para color
                _interactable.hoverEntered.AddListener(OnHoverEnter);
                _interactable.hoverExited.AddListener(OnHoverExit);

                // --- NUEVO: ACTIVAR DETALLES AL HACER CLICK (SELECT) ---
                // XR Toolkit lanza "SelectEntered" cuando presionas el gatillo.
                // Usaremos eso para mostrar detalles inmediatamente.
            }
        }

        void OnDisable()
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.RemoveListener(OnSelectStart);
                _interactable.selectExited.RemoveListener(OnSelectEnd);
                _interactable.hoverEntered.RemoveListener(OnHoverEnter);
                _interactable.hoverExited.RemoveListener(OnHoverExit);
            }
        }

        public void InitializeNode(Transform parent, LineRenderer lineFromParent)
        {
            parentNodeTransform = parent;
            incomingLine = lineFromParent;

            // Guardamos la posición inicial para la animación de retorno
            originalLocalPosition = transform.localPosition;

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
            // Actualizar línea constantemente (Vital para la animación donde el padre se mueve)
            if (incomingLine != null && parentNodeTransform != null)
            {
                // Convertimos a World Space porque el LineRenderer usa World Space
                incomingLine.SetPosition(0, parentNodeTransform.position);
                incomingLine.SetPosition(1, transform.position);
            }

            // Lógica del HOLD (4 Segundos)
            if (_isGrabbing && !_hasActivated)
            {
                _holdTimer += Time.deltaTime;
                float progress = _holdTimer / _activationTime;

                if (loaderUI != null) loaderUI.SetProgress(progress);

                if (_holdTimer >= _activationTime)
                {
                    ExecuteHoldAction(); // Se cumplieron los 4 segundos
                }
            }
        }

        void OnSelectStart(SelectEnterEventArgs args)
        {
            _isGrabbing = true;
            _holdTimer = 0f;
            _hasActivated = false;

            // --- ACCIÓN INMEDIATA: MOSTRAR DETALLES (CLICK) ---
            // Esto cumple tu requerimiento: "Click sobre cualquier nodo muestra info"
            SendToZone3();
        }

        void OnSelectEnd(SelectExitEventArgs args)
        {
            _isGrabbing = false;
            _holdTimer = 0f;
            if (loaderUI != null) loaderUI.SetProgress(0);
        }

        // Esta función se llama a los 4 segundos de mantener presionado
        void ExecuteHoldAction()
        {
            _hasActivated = true;
            if (loaderUI != null) loaderUI.SetProgress(1f);

            // 1. Poner Chincheta (Marcado como Visto)
            if (!_isReviewed && reviewedMarkerPrefab != null)
            {
                GameObject marker = Instantiate(reviewedMarkerPrefab, transform);
                marker.transform.localPosition = markerLocalOffset;
                marker.transform.localScale = markerLocalScale;
                marker.transform.localRotation = Quaternion.identity;
                _isReviewed = true;
            }

            // 2. Lógica Especial por Tipo
            if (nodeType == "community")
            {
                // Aquí llamamos al MANAGER para la animación del cielo
                if (interactionManager != null)
                {
                    interactionManager.OnCommunityHoldActivated(this);
                }
                else
                {
                    // Fallback si no hay manager: Solo expandir hijos localmente
                    ForceExpand(!_isExpanded);
                }
            }
            // Para "file" o "root", el Hold solo pone la chincheta (ya mostramos detalles al click)
        }

        // Función pública llamada por el Manager
        public void ForceExpand(bool state)
        {
            _isExpanded = state;
            SetChildrenVisibility(state);

            if (state && _audioSource != null && expandSound != null)
            {
                _audioSource.PlayOneShot(expandSound);
            }
        }

        void SendToZone3()
        {
            if (localZone3Manager != null)
            {
                localZone3Manager.ShowNodeDetails(myData);
            }
        }

        void SetChildrenVisibility(bool state)
        {
            foreach (var child in childNodes) if (child != null) child.SetActive(state);
            foreach (var line in childConnectionLines) if (line != null) line.SetActive(state);
        }

        // Variables para evitar spam de logs
        private float _lastHoverLogTime = 0f;
        private float _logCooldown = 1.0f;

        void OnHoverEnter(HoverEnterEventArgs args)
        {
            if (_renderer != null) _renderer.material.color = _hoverColor;

            // Métrica de Atención (Igual que antes)
            if (ExperimentDataLogger.Instance != null && Time.time - _lastHoverLogTime > _logCooldown)
            {
                _lastHoverLogTime = Time.time;
                ExperimentDataLogger.Instance.LogEvent("ATTENTION", "Gaze/Hover", $"Node: {myData.title}", transform.position);
            }
        }
        void OnHoverExit(HoverExitEventArgs args) { if (_renderer != null) _renderer.material.color = _originalColor; }
    }
}