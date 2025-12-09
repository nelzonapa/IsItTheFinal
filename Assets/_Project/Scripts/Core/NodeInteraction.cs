using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit; // Librería estándar de VR
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Necesario para Unity 6 / XRI 3

namespace ImmersiveGraph.Core
{
    // Este componente requiere que el nodo tenga un Collider (ya lo tienen) y un XRSimpleInteractable
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class NodeInteraction : MonoBehaviour
    {
        private Renderer _renderer;
        private Material _originalMaterial;

        // Color de resaltado (lo definiremos en el Manager para que sea global)
        private Color _highlightColor = Color.cyan;
        private Color _originalColor;

        private GraphInteractionManager _manager;
        private string _nodeType;
        private string _nodeId;

        public void Initialize(GraphInteractionManager manager, string type, string id)
        {
            _manager = manager;
            _nodeType = type;
            _nodeId = id;

            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                _originalMaterial = _renderer.material;
                if (_originalMaterial.HasProperty("_BaseColor"))
                    _originalColor = _originalMaterial.GetColor("_BaseColor");
            }

            SetupXREvents();
        }

        void SetupXREvents()
        {
            var interactable = GetComponent<XRSimpleInteractable>();

            // --- HOVER (Cuando el rayo apunta al nodo) ---
            interactable.hoverEntered.AddListener((args) => {
                // 1. Cambiar color propio
                SetHighlight(true);
                // 2. Avisar al Manager para resaltar comunidad o enlaces
                _manager.OnNodeHoverEnter(this.transform, _nodeType, _nodeId);
            });

            interactable.hoverExited.AddListener((args) => {
                // 1. Restaurar color
                SetHighlight(false);
                // 2. Avisar al Manager para limpiar
                _manager.OnNodeHoverExit();
            });

            // --- SELECT (Cuando presionas el gatillo) ---
            interactable.selectEntered.AddListener((args) => {
                _manager.OnNodeSelected(_nodeType, _nodeId);
            });
        }

        public void SetHighlight(bool active)
        {
            if (_renderer == null) return;

            if (active)
            {
                // Opción A: Cambiar a material emisivo brillante
                _renderer.material.color = _highlightColor;
                _renderer.material.EnableKeyword("_EMISSION");
                _renderer.material.SetColor("_EmissionColor", _highlightColor * 2f); // Brillo intenso
            }
            else
            {
                // Restaurar
                _renderer.material.color = _originalColor;
                // Si el original tenía emisión, restaurarla, si no, apagarla (simplificado aquí)
                _renderer.material.SetColor("_EmissionColor", Color.black);
            }
        }
    }
}