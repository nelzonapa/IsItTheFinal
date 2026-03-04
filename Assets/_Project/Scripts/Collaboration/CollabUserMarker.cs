using UnityEngine;

namespace ImmersiveGraph.Collaboration
{
    public class CollabUserMarker : MonoBehaviour
    {
        [Header("Configuración Visual")]
        [Tooltip("El Renderer del objeto 3D que cambiará de color.")]
        public Renderer markerRenderer;

        [Header("Efecto de Brillo (Glow)")]
        [Tooltip("Multiplicador de intensidad para el brillo HDR. Súbelo si quieres más luz.")]
        public float glowIntensity = 3.0f;

        private MaterialPropertyBlock _propBlock;

        /// <summary>
        /// Aplica el color del usuario remoto y genera emisión HDR (Optimizado O(1)).
        /// </summary>
        public void SetupMarker(Color userColor)
        {
            if (markerRenderer != null)
            {
                if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

                markerRenderer.GetPropertyBlock(_propBlock);

                // 1. Asignar el color base (Cubrimos tanto el motor clásico como URP)
                _propBlock.SetColor("_Color", userColor);
                _propBlock.SetColor("_BaseColor", userColor);

                // 2. Crear el color HDR multiplicando por la intensidad para el Glow
                Color hdrGlowColor = new Color(
                    userColor.r * glowIntensity,
                    userColor.g * glowIntensity,
                    userColor.b * glowIntensity,
                    1f
                );

                // 3. Inyectar el brillo en el canal de Emisión
                _propBlock.SetColor("_EmissionColor", hdrGlowColor);

                markerRenderer.SetPropertyBlock(_propBlock);
            }
            else
            {
                Debug.LogWarning("[CollabMarker] Falta asignar el Renderer en el inspector.");
            }
        }
    }
}