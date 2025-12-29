using UnityEngine;
using UnityEngine.UI; // Para botones UI estándar
using TMPro; // Para TextMeshPro

namespace ImmersiveGraph.Data
{
    [RequireComponent(typeof(AudioSource))]
    public class SessionFinisher : MonoBehaviour
    {
        [Header("Referencias del Botón")]
        public Button uiButton; // Tu botón actual

        [Header("Feedback de Éxito")]
        [Tooltip("El Panel o Canvas que contiene el mensaje de 'Datos Guardados'")]
        public GameObject successMessageObject;

        [Tooltip("Texto opcional para cambiar el mensaje dinámicamente")]
        public TextMeshProUGUI statusText;

        [Tooltip("Sonido de éxito (Ding!)")]
        public AudioClip successSound;

        [Header("Configuración")]
        public bool quitAppAfterDelay = false;
        public float delayToQuit = 5.0f;

        private AudioSource _audioSource;
        private bool _isFinished = false;

        void Start()
        {
            _audioSource = GetComponent<AudioSource>();

            // Configurar botón UI
            if (uiButton != null)
            {
                uiButton.onClick.AddListener(OnFinishClicked);
            }

            // Asegurarnos que el mensaje empiece apagado
            if (successMessageObject != null) successMessageObject.SetActive(false);
        }

        public void OnFinishClicked()
        {
            if (_isFinished) return; // Evitar doble clic

            // 1. LLAMAR AL LOGGER (Guardar todo)
            if (ExperimentDataLogger.Instance != null)
            {
                // Métrica de tiempo final
                ExperimentDataLogger.Instance.LogEvent("SYSTEM", "SESSION_FINISHED_BY_USER", "Button Pressed", Vector3.zero);

                // Exportar JSON
                ExperimentDataLogger.Instance.ExportFinalGraphState();

                // Guardar CSV
                ExperimentDataLogger.Instance.SaveLogsToDisk();

                Debug.Log("--- EXPERIMENTO FINALIZADO Y GUARDADO ---");
            }
            else
            {
                Debug.LogError("Error: No se encontró ExperimentDataLogger.");
                if (statusText != null) statusText.text = "Error: No se encontró el Logger.";
                return; // Si falla el logger, quizás no debamos mostrar éxito
            }

            // 2. FEEDBACK VISUAL Y AUDITIVO
            PerformSuccessFeedback();
        }

        void PerformSuccessFeedback()
        {
            _isFinished = true;

            // A. Sonido
            if (_audioSource != null && successSound != null)
            {
                _audioSource.PlayOneShot(successSound);
            }

            // B. Desactivar el botón para que no le den click otra vez
            if (uiButton != null)
            {
                uiButton.interactable = false;
                // Cambiar color a verde visualmente
                var colors = uiButton.colors;
                colors.disabledColor = new Color(0.2f, 0.8f, 0.2f); // Verde
                uiButton.colors = colors;
            }

            // C. Mostrar el Mensaje en Pantalla/Mesa
            if (successMessageObject != null)
            {
                successMessageObject.SetActive(true);
            }

            // D. Salir de la App (Opcional)
            if (quitAppAfterDelay)
            {
                StartCoroutine(QuitCoroutine());
            }
        }

        System.Collections.IEnumerator QuitCoroutine()
        {
            if (statusText != null) statusText.text += $"\nCerrando en {delayToQuit} segundos...";

            yield return new WaitForSeconds(delayToQuit);

            Debug.Log("Cerrando Aplicación...");
            Application.Quit();

            // Nota: Application.Quit() no funciona en el Editor de Unity, solo en la Build.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}