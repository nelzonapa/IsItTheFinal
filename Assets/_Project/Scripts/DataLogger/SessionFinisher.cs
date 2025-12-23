using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit; // O el sistema de UI que uses
using UnityEngine.UI;

namespace ImmersiveGraph.Data
{
    public class SessionFinisher : MonoBehaviour
    {
        // OPCIÓN A: Si usas un Botón de UI (Canvas)
        public Button uiButton;

        // OPCIÓN B: Si usas un objeto 3D físico interactuable
        // public UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable physicalButton;

        void Start()
        {
            if (uiButton != null)
            {
                uiButton.onClick.AddListener(FinishSession);
            }
        }

        public void FinishSession()
        {
            if (ExperimentDataLogger.Instance != null)
            {
                ExperimentDataLogger.Instance.ExportFinalGraphState();
                Debug.Log("Sesión finalizada. Datos exportados.");

                // Opcional: Sonido de éxito o cerrar la app
                // Application.Quit(); 
            }
            else
            {
                Debug.LogError("No se encontró el ExperimentDataLogger en la escena.");
            }
        }
    }
}