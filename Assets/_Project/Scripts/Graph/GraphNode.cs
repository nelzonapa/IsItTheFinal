using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;
using ImmersiveGraph.Data;
using ImmersiveGraph.Visual;
using ImmersiveGraph.Core;

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(SphereCollider))]
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

        [Header("Referencias UI")]
        public NodeUIController infoUI;
        public NodeLoaderController loaderUI;

        // --- VARIABLES PÚBLICAS PARA RECIBIR CONFIGURACIÓN ---
        public GameObject reviewedMarkerPrefab;
        public Vector3 markerLocalOffset;
        public Vector3 markerLocalScale;
        // ----------------------------------------------------

        // Lógica Interna
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

        void Awake()
        {
            _interactable = GetComponent<XRGrabInteractable>();
            _renderer = GetComponent<Renderer>();

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            _interactable.movementType = XRBaseInteractable.MovementType.Kinematic;
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
        }

        void OnGrabEnd(SelectExitEventArgs args)
        {
            _isGrabbing = false;
            _holdTimer = 0f;
            if (loaderUI != null) loaderUI.SetProgress(0);
        }

        void ExecuteActivation()
        {
            _hasActivated = true;
            if (loaderUI != null) loaderUI.SetProgress(1f);

            // --- INSTANCIAR CHINCHETA ---
            if (!_isReviewed && reviewedMarkerPrefab != null)
            {
                Debug.Log($"Activando chincheta en {name}");

                GameObject marker = Instantiate(reviewedMarkerPrefab, transform);

                // Usamos las variables que nos pasó el Spawner
                marker.transform.localPosition = markerLocalOffset;
                marker.transform.localScale = markerLocalScale;

                // Aseguramos rotación cero relativa
                marker.transform.localRotation = Quaternion.identity;

                _isReviewed = true;
            }
            else if (reviewedMarkerPrefab == null)
            {
                Debug.LogWarning("No aparece la chincheta porque 'reviewedMarkerPrefab' es NULL en el GraphNode.");
            }
            // -----------------------------

            if (nodeType == "community")
            {
                _isExpanded = !_isExpanded;
                SetChildrenVisibility(_isExpanded);
                SendToZone3();
            }
            else if (nodeType == "file")
            {
                SendToZone3();
            }
            else if (nodeType == "root")
            {
                SendToZone3();
            }
        }

        void SendToZone3()
        {
            Debug.Log($"--> ENVIANDO {myData.title} A ZONE 3");
            if (localZone3Manager != null) localZone3Manager.ShowNodeDetails(myData);
            else Debug.LogError($"El nodo {name} no tiene asignado un Zone3Manager local.");
        }

        void SetChildrenVisibility(bool state)
        {
            foreach (var child in childNodes) if (child != null) child.SetActive(state);
            foreach (var line in childConnectionLines) if (line != null) line.SetActive(state);
        }

        void OnHoverEnter(HoverEnterEventArgs args) { if (_renderer != null) _renderer.material.color = _hoverColor; }
        void OnHoverExit(HoverExitEventArgs args) { if (_renderer != null) _renderer.material.color = _originalColor; }
    }
}