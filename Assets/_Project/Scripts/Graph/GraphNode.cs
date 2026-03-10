using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;
using ImmersiveGraph.Data;
using ImmersiveGraph.Visual;
using ImmersiveGraph.Core;
using ImmersiveGraph.Network;

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
        public GraphInteractionManager interactionManager;
        public NodeLoaderController loaderUI; // (Puedes borrarlo del Inspector si ya no usas la barrita circular)

        [Header("Colaboración")]
        public ImmersiveGraph.Collaboration.MiniWorldManager miniWorldManager;

        public GameObject reviewedMarkerPrefab;
        public Vector3 markerLocalOffset;
        public Vector3 markerLocalScale;

        [HideInInspector] public Vector3 originalLocalPosition;

        private XRGrabInteractable _interactable;
        private Renderer _renderer;

        private Color _originalColor;
        private Color _hoverColor;
        private Color _currentColorState;
        private int _currentVisualState = 0;
        private bool _isHovered = false;

        private GameObject _glowObject;
        private Renderer _glowRenderer;

        // ¡NUEVO! Ahora es público para que la UI sepa qué nodo tiene en la mano el usuario
        public static GraphNode CurrentSelectedLocalNode { get; private set; }

        private bool _isGrabbing = false;
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

            if (_interactable != null) _interactable.movementType = XRBaseInteractable.MovementType.Kinematic;

            CreateGlowHalo();
        }

        void OnEnable()
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.AddListener(OnSelectStart);
                _interactable.selectExited.AddListener(OnSelectEnd);
                _interactable.hoverEntered.AddListener(OnHoverEnter);
                _interactable.hoverExited.AddListener(OnHoverExit);
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
            originalLocalPosition = transform.localPosition;

            if (_renderer != null)
            {
                _originalColor = _renderer.material.color;
                _currentColorState = _originalColor;
                _hoverColor = Color.Lerp(_originalColor, Color.white, 0.4f);
            }

            if (nodeType == "community")
            {
                _isExpanded = false;
                SetChildrenVisibility(false);
            }
        }

        private void CreateGlowHalo()
        {
            _glowObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _glowObject.name = "LocalSelectionGlow";
            _glowObject.transform.SetParent(this.transform);
            _glowObject.transform.localPosition = Vector3.zero;
            _glowObject.transform.localScale = Vector3.one * 1.35f;
            Destroy(_glowObject.GetComponent<Collider>());
            _glowRenderer = _glowObject.GetComponent<Renderer>();
            Material glowMat = new Material(Shader.Find("Sprites/Default"));
            _glowRenderer.material = glowMat;
            _glowObject.SetActive(false);
        }

        public void SetLocalSelectedGlow(bool isSelected)
        {
            if (_glowObject != null)
            {
                _glowObject.SetActive(isSelected);

                if (isSelected && HardwareRigSync.Local != null)
                {
                    Color myColor = UserColorPalette.GetColor(HardwareRigSync.Local.Object.StateAuthority.PlayerId);
                    myColor.a = 0.45f;
                    _glowRenderer.material.color = myColor;
                }
            }
        }

        public void SetVisualState(int stateIndex)
        {
            if (_renderer == null) return;
            _currentVisualState = stateIndex;

            switch (stateIndex)
            {
                case 0: _currentColorState = _originalColor; break;
                case 1: _currentColorState = Color.cyan; break;
                case 2: _currentColorState = new Color(0.2f, 0.2f, 0.2f, 0.15f); break;
            }

            if (!_isHovered) _renderer.material.color = _currentColorState;
        }

        void OnHoverEnter(HoverEnterEventArgs args)
        {
            _isHovered = true;
            if (_renderer != null)
            {
                if (_currentVisualState == 2) _renderer.material.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                else if (_currentVisualState == 1) _renderer.material.color = Color.white;
                else _renderer.material.color = _hoverColor;
            }

            if (ExperimentDataLogger.Instance != null && Time.time - _lastHoverLogTime > _logCooldown)
            {
                _lastHoverLogTime = Time.time;
                ExperimentDataLogger.Instance.LogEvent("ATTENTION", "Gaze/Hover", $"Node: {myData.title}", transform.position);
            }
        }

        void OnHoverExit(HoverExitEventArgs args)
        {
            _isHovered = false;
            if (_renderer != null) _renderer.material.color = _currentColorState;
        }

        void Update()
        {
            if (incomingLine != null && parentNodeTransform != null)
            {
                incomingLine.SetPosition(0, parentNodeTransform.position);
                incomingLine.SetPosition(1, transform.position);
            }
            // ¡ELIMINADO EL TIMER DE 4 SEGUNDOS DE AQUÍ!
        }

        void OnSelectStart(SelectEnterEventArgs args)
        {
            _isGrabbing = true;

            // Envía info instantánea
            SendToZone3();

            // Marca instantáneamente como visto
            if (!_isReviewed && reviewedMarkerPrefab != null)
            {
                GameObject marker = Instantiate(reviewedMarkerPrefab, transform);
                marker.transform.localPosition = markerLocalOffset;
                marker.transform.localScale = markerLocalScale;
                marker.transform.localRotation = Quaternion.identity;
                _isReviewed = true;
            }

            // Gestiona el brillo
            if (CurrentSelectedLocalNode != null && CurrentSelectedLocalNode != this)
            {
                CurrentSelectedLocalNode.SetLocalSelectedGlow(false);
            }

            CurrentSelectedLocalNode = this;
            SetLocalSelectedGlow(true);

            if (nodeType == "community" || nodeType == "root")
            {
                if (HardwareRigSync.Local != null) HardwareRigSync.Local.SetSelectedNode(myData.id);
                else if (miniWorldManager != null) miniWorldManager.HighlightNodeLocalFallback(myData.id, Color.white);
            }
            else if (nodeType == "file")
            {
                if (HardwareRigSync.Local != null) HardwareRigSync.Local.SetSelectedNode(myData.id);
            }
        }

        void OnSelectEnd(SelectExitEventArgs args)
        {
            _isGrabbing = false;
        }

        public void ForceExpand(bool state)
        {
            _isExpanded = state;
            SetChildrenVisibility(state);

            if (state && _audioSource != null && expandSound != null) _audioSource.PlayOneShot(expandSound);
        }

        void SendToZone3()
        {
            if (localZone3Manager != null) localZone3Manager.ShowNodeDetails(myData);
        }

        void SetChildrenVisibility(bool state)
        {
            foreach (var child in childNodes) if (child != null) child.SetActive(state);
            foreach (var line in childConnectionLines) if (line != null) line.SetActive(state);
        }

        private float _lastHoverLogTime = 0f;
        private float _logCooldown = 1.0f;
    }
}