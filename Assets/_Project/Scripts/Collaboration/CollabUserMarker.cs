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
        /// Aplica el color del usuario remoto, fuerza la opacidad y genera emisión HDR.
        /// </summary>
        public void SetupMarker(Color userColor)
        {
            if (markerRenderer != null)
            {
                if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

                // Forzar opacidad total
                // Si la paleta devuelve un color transparente (a = 0), el objeto desaparece.
                // Lo forzamos a 1 (100% opaco) para garantizar que se renderice en pantalla.
                userColor.a = 1.0f;

                markerRenderer.GetPropertyBlock(_propBlock);

                // 1. Asignar el color base sólido
                _propBlock.SetColor("_Color", userColor);
                _propBlock.SetColor("_BaseColor", userColor);

                // 2. Crear el color HDR multiplicando por la intensidad para el Glow
                Color hdrGlowColor = new Color(
                    userColor.r * glowIntensity,
                    userColor.g * glowIntensity,
                    userColor.b * glowIntensity,
                    1.0f
                );

                // 3. Inyectar el brillo
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