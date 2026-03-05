using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion; // <-- NUEVO: Importamos la librería de red para manejar la desconexión

namespace ImmersiveGraph.Data
{
    [RequireComponent(typeof(AudioSource))]
    public class SessionFinisher : MonoBehaviour
    {
        [Header("Referencias del Botón")]
        public Button uiButton;

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

            if (uiButton != null)
            {
                uiButton.onClick.AddListener(OnFinishClicked);
            }

            if (successMessageObject != null) successMessageObject.SetActive(false);
        }

        public void OnFinishClicked()
        {
            if (_isFinished) return;

            // 1. LLAMAR AL LOGGER (Guardar todo)
            if (ExperimentDataLogger.Instance != null)
            {
                ExperimentDataLogger.Instance.LogEvent("SYSTEM", "SESSION_FINISHED_BY_USER", "Button Pressed", Vector3.zero);
                ExperimentDataLogger.Instance.ExportFinalGraphState();
                ExperimentDataLogger.Instance.SaveLogsToDisk();

                Debug.Log("--- EXPERIMENTO FINALIZADO Y GUARDADO ---");
            }
            else
            {
                Debug.LogError("Error: No se encontró ExperimentDataLogger.");
                if (statusText != null) statusText.text = "Error: No se encontró el Logger.";
                return;
            }

            // 2. FEEDBACK VISUAL Y AUDITIVO
            PerformSuccessFeedback();
        }

        void PerformSuccessFeedback()
        {
            _isFinished = true;

            if (_audioSource != null && successSound != null)
            {
                _audioSource.PlayOneShot(successSound);
            }

            if (uiButton != null)
            {
                uiButton.interactable = false;
                var colors = uiButton.colors;
                colors.disabledColor = new Color(0.2f, 0.8f, 0.2f);
                uiButton.colors = colors;
            }

            if (successMessageObject != null)
            {
                successMessageObject.SetActive(true);
            }

            if (quitAppAfterDelay)
            {
                StartCoroutine(QuitCoroutine());
            }
        }

        System.Collections.IEnumerator QuitCoroutine()
        {
            if (statusText != null) statusText.text += $"\nDesconectando de la red en {delayToQuit} segundos...";

            yield return new WaitForSeconds(delayToQuit);

            Debug.Log("Desconectando al usuario local de la sesión...");

            // --- NUEVO: Desconexión elegante y local ---
            // Buscamos el NetworkRunner activo. Es seguro usar FindFirstObjectByType aquí 
            // porque solo ocurre una vez al final de la sesión, no afecta el rendimiento (O(1) en ejecución continua no aplica aquí).
            NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
            if (runner != null && runner.IsRunning)
            {
                // Shutdown() desconecta a ESTE cliente enviando un mensaje de despedida al servidor.
                // Los demás usuarios seguirán en su mundo sin interrupciones y verán desaparecer el avatar inmediatamente.
                runner.Shutdown();
            }

            // Damos un pequeño frame de gracia (medio segundo) para que el paquete de desconexión viaje por la red antes de matar la app
            yield return new WaitForSeconds(0.5f);

            Debug.Log("Cerrando Aplicación VR...");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}