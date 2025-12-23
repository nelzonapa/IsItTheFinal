using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit; // O el sistema de UI que uses
using UnityEngine.UI;

namespace ImmersiveGraph.Data
{
    public class SessionFinisher : MonoBehaviour
    {
        public Button uiButton; // Asigna tu botón de UI aquí

        void Start()
        {
            if (uiButton != null) uiButton.onClick.AddListener(FinishSession);
        }

        public void FinishSession()
        {
            if (ExperimentDataLogger.Instance != null)
            {
                // 1. Registrar el momento exacto del fin (Para restar con el inicio en Excel)
                ExperimentDataLogger.Instance.LogEvent("SYSTEM", "SESSION_FINISHED_BY_USER", "Button Pressed", Vector3.zero);

                // 2. Exportar el JSON del Grafo
                ExperimentDataLogger.Instance.ExportFinalGraphState();

                // 3. Guardar el CSV en disco
                ExperimentDataLogger.Instance.SaveLogsToDisk();

                Debug.Log("--- EXPERIMENTO FINALIZADO ---");
            }
            else
            {
                Debug.LogError("No se encontró el ExperimentDataLogger en la escena.");
            }
        }
    }
}