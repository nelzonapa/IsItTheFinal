using UnityEngine;

namespace ImmersiveGraph.Collaboration
{
    public class CollabUserMarker : MonoBehaviour
    {
        [Header("Configuración Visual")]
        [Tooltip("El Renderer del objeto 3D que cambiará de color.")]
        public Renderer markerRenderer;

        /// <summary>
        /// Aplica el color del usuario remoto al marcador.
        /// </summary>
        public void SetupMarker(Color userColor)
        {
            if (markerRenderer != null)
            {
                // Al modificar '.material', Unity crea automáticamente una instancia 
                // única de este material, evitando que todos los cubos cambien a la vez.
                markerRenderer.material.color = userColor;

                // Opcional: Si usas un material transparente/holográfico, 
                // asegúrate de mantener el Alpha (transparencia) original del material.
                // Color finalColor = new Color(userColor.r, userColor.g, userColor.b, markerRenderer.material.color.a);
                // markerRenderer.material.color = finalColor;
            }
            else
            {
                Debug.LogWarning("[CollabMarker] Falta asignar el Renderer en el inspector.");
            }
        }
    }
}