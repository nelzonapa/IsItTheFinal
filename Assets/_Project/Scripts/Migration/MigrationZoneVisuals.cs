using UnityEngine;

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class MigrationZoneVisuals : MonoBehaviour
    {
        [Header("Feedback")]
        public Color zoneReadyColor = new Color(0f, 0.8f, 0f); // Verde Matrix brillante

        private void OnTriggerEnter(Collider other)
        {
            // Buscamos el script de feedback en el objeto que entra
            // Usamos GetComponentInParent porque a veces el collider está en un hijo
            var feedback = other.GetComponentInParent<Visual.InteractableFeedback>();

            if (feedback != null)
            {
                // ¡Entró! Activar Override Verde
                feedback.SetZoneHighlight(true, zoneReadyColor);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var feedback = other.GetComponentInParent<Visual.InteractableFeedback>();

            if (feedback != null)
            {
                // ¡Salió! Desactivar Override (vuelve a su brillo normal de agarre)
                feedback.SetZoneHighlight(false, Color.black);
            }
        }
    }
}