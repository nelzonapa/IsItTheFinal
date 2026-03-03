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
        public NodeLoaderController loaderUI;

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

        private bool _isGrabbing = false;
        private float _holdTimer = 0f;
        private float _activationTime = 4.0f;
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

            if (_interactable != null) _interactable.movementType = XRBaseInteractable.MovementType.Kinematic;
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
                _hoverColor = Color.Lerp(_originalColor, Color.white, 0.4f);
            }

            if (nodeType == "community")
            {
                _isExpanded = false;
                SetChildrenVisibility(false);
            }
        }

        // FASE 4: CONTROL VISUAL DEL NODO
        public void SetVisualState(int stateIndex)
        {
            if (_renderer == null) return;

            switch (stateIndex) 
            {
                case 0: // NORMAL
                    _renderer.material.color = _originalColor;
                    break;
                case 1: // GLOW (Resaltado Fuerte)
                    _renderer.material.color = Color.cyan; // Color de resalte llamativo
                    break;
                case 2: // GHOST (No relacionado, opaco/gris)
                    // Nota: Para que el alfa (0.1f) funcione, tu material en Unity debe estar en Rendering Mode: Transparent o Fade.
                    _renderer.material.color = new Color(0.2f, 0.2f, 0.2f, 0.15f);
                    break;
            }
        }

        void Update()
        {
            if (incomingLine != null && parentNodeTransform != null)
            {
                incomingLine.SetPosition(0, parentNodeTransform.position);
                incomingLine.SetPosition(1, transform.position);
            }

            if (_isGrabbing && !_hasActivated)
            {
                _holdTimer += Time.deltaTime;
                float progress = _holdTimer / _activationTime;

                if (loaderUI != null) loaderUI.SetProgress(progress);

                if (_holdTimer >= _activationTime) ExecuteHoldAction();
            }
        }

        void OnSelectStart(SelectEnterEventArgs args)
        {
            _isGrabbing = true;
            _holdTimer = 0f;
            _hasActivated = false;

            SendToZone3();

            if (nodeType == "community" || nodeType == "root")
            {
                if (HardwareRigSync.Local != null)
                {
                    HardwareRigSync.Local.SetSelectedNode(myData.id);
                }
                else if (miniWorldManager != null)
                {
                    miniWorldManager.HighlightNodeLocalFallback(myData.id, UserColorPalette.GetLocalPlayerColor());
                }
            }
        }

        void OnSelectEnd(SelectExitEventArgs args)
        {
            _isGrabbing = false;
            _holdTimer = 0f;
            if (loaderUI != null) loaderUI.SetProgress(0);
        }

        void ExecuteHoldAction()
        {
            _hasActivated = true;
            if (loaderUI != null) loaderUI.SetProgress(1f);

            if (!_isReviewed && reviewedMarkerPrefab != null)
            {
                GameObject marker = Instantiate(reviewedMarkerPrefab, transform);
                marker.transform.localPosition = markerLocalOffset;
                marker.transform.localScale = markerLocalScale;
                marker.transform.localRotation = Quaternion.identity;
                _isReviewed = true;
            }

            if (nodeType == "community")
            {
                if (interactionManager != null) interactionManager.OnCommunityHoldActivated(this);
                else ForceExpand(!_isExpanded);
            }
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

        void OnHoverEnter(HoverEnterEventArgs args)
        {
            if (_renderer != null) _renderer.material.color = _hoverColor;

            if (ExperimentDataLogger.Instance != null && Time.time - _lastHoverLogTime > _logCooldown)
            {
                _lastHoverLogTime = Time.time;
                ExperimentDataLogger.Instance.LogEvent("ATTENTION", "Gaze/Hover", $"Node: {myData.title}", transform.position);
            }
        }

        void OnHoverExit(HoverExitEventArgs args)
        {
            if (_renderer != null) _renderer.material.color = _originalColor;
        }
    }
}