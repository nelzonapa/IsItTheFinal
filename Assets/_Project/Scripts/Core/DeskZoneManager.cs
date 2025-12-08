using UnityEngine;
 // Importante para detectar el Rayo

namespace ImmersiveGraph.Core
{
    public enum ZoneType
    {
        Zone1_DeskConfig,
        Zone2_DataPanel,
        Zone3_ObjectZone,
        Zone4_Mural,
        Zone5_Shared,
        Zone6_Notes,
        TrashCan
    }

    // Requiere que el objeto tenga un componente Interactable
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
    public class DeskZone : MonoBehaviour
    {
        [Header("Configuraci�n de Zona")]
        public ZoneType type;

        [Tooltip("Si es true, muestra un borde o color cuando el rayo o la mano entran.")]
        public bool highlightOnHover = true;

        private Renderer _renderer;
        private Color _originalColor;
        private Color _highlightColor;

        void Start()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                _originalColor = _renderer.material.color;
                // Hacemos el color un 40% m�s opaco al resaltar
                _highlightColor = new Color(_originalColor.r, _originalColor.g, _originalColor.b, 0.6f);
            }

            SetupInteractionEvents();
        }

        void SetupInteractionEvents()
        {
            // Conectamos con el sistema de eventos de Unity XR
            var interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

            // Cuando el Rayo ENTRA (Hover Enter)
            interactable.hoverEntered.AddListener((args) => {
                if (highlightOnHover && _renderer != null) _renderer.material.color = _highlightColor;
            });

            // Cuando el Rayo SALE (Hover Exit)
            interactable.hoverExited.AddListener((args) => {
                if (highlightOnHover && _renderer != null) _renderer.material.color = _originalColor;
            });
        }
    }
}