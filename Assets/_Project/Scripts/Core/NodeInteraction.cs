using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers; // Para manipulación

namespace ImmersiveGraph.Core
{
    // Cambiamos a GRAB interactable para permitir mover/rotar/escalar
    //[RequireComponent(typeof(XRGrabInteractable))]
    //[RequireComponent(typeof(XRGeneralGrabTransformer))]
    public class NodeInteraction : MonoBehaviour
    {
        [Header("UI Feedback")]
        public GameObject loadingBarPrefab; // Asigna el prefab NodeLoadingBar aquí desde el Manager

        private Renderer _renderer;
        private Color _originalColor;
        private Color _highlightColor = Color.cyan;

        private GraphInteractionManager _manager;
        private string _nodeType;
        private string _nodeId;

        // Lógica de Hold (Mantener presionado)
        private bool isHeld = false;
        private float holdTimer = 0f;
        private float requiredHoldTime = 3.0f; // 3 segundos para abrir nivel
        private GameObject currentBarInstance;
        private Image currentFillImage;
        private XRGrabInteractable _grabInteractable;

        public void Initialize(GraphInteractionManager manager, string type, string id, GameObject barPrefab)
        {
            _manager = manager;
            _nodeType = type;
            _nodeId = id;
            loadingBarPrefab = barPrefab;

            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                if (_renderer.material.HasProperty("_BaseColor"))
                    _originalColor = _renderer.material.GetColor("_BaseColor");
            }

            SetupXREvents();
        }

        void SetupXREvents()
        {
            _grabInteractable = GetComponent<XRGrabInteractable>();

            // Configuración para mover/rotar/escalar libremente
            _grabInteractable.trackRotation = true;
            _grabInteractable.throwOnDetach = false; // Que no salga volando

            // Hover (Resaltado)
            _grabInteractable.hoverEntered.AddListener((args) => {
                SetHighlight(true);
                _manager.OnNodeHoverEnter(this.transform, _nodeType, _nodeId);
            });

            _grabInteractable.hoverExited.AddListener((args) => {
                SetHighlight(false);
                _manager.OnNodeHoverExit();
            });

            // Select (Agarre) - Inicia el Timer
            _grabInteractable.selectEntered.AddListener((args) => {
                isHeld = true;
                holdTimer = 0f;
                CreateLoadingBar();

                // Notificar selección inmediata (para replicación)
                _manager.OnNodeGrabbed(_nodeType, _nodeId, this.gameObject);
            });

            // Deselección - Cancela el Timer
            _grabInteractable.selectExited.AddListener((args) => {
                isHeld = false;
                holdTimer = 0f;
                DestroyLoadingBar();
            });
        }

        void Update()
        {
            // Lógica de Barra de Llenado
            if (isHeld && currentFillImage != null)
            {
                holdTimer += Time.deltaTime;
                float progress = holdTimer / requiredHoldTime;
                currentFillImage.fillAmount = progress;

                if (holdTimer >= requiredHoldTime)
                {
                    // ¡Completado!
                    isHeld = false; // Reset para no disparar múltiple
                    DestroyLoadingBar();
                    _manager.OnNodeHoldComplete(_nodeType, _nodeId);
                }
            }
        }

        void CreateLoadingBar()
        {
            if (loadingBarPrefab != null && currentBarInstance == null)
            {
                // Instanciar DEBAJO del nodo
                Vector3 pos = transform.position - (Vector3.up * (transform.localScale.y * 1.5f));
                currentBarInstance = Instantiate(loadingBarPrefab, pos, Quaternion.identity, transform);

                // Buscar la imagen de relleno (asumiendo que es el segundo hijo o buscar por nombre)
                // Ajusta esto según tu prefab. Aquí busco el primer Image tipo Filled.
                Image[] images = currentBarInstance.GetComponentsInChildren<Image>();
                foreach (var img in images)
                {
                    if (img.type == Image.Type.Filled)
                    {
                        currentFillImage = img;
                        break;
                    }
                }

                // Asegurar que mire a la cámara siempre (Billboard simple)
                currentBarInstance.AddComponent<BillboardSimple>();
            }
        }

        void DestroyLoadingBar()
        {
            if (currentBarInstance != null)
            {
                Destroy(currentBarInstance);
                currentBarInstance = null;
                currentFillImage = null;
            }
        }

        public void SetHighlight(bool active)
        {
            if (_renderer == null) return;
            if (active)
            {
                _renderer.material.color = _highlightColor;
                _renderer.material.EnableKeyword("_EMISSION");
                _renderer.material.SetColor("_EmissionColor", _highlightColor * 1.5f);
            }
            else
            {
                _renderer.material.color = _originalColor;
                _renderer.material.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    // Pequeño helper para la barra
    public class BillboardSimple : MonoBehaviour
    {
        void Update()
        {
            if (Camera.main != null) transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward, Camera.main.transform.rotation * Vector3.up);
        }
    }
}