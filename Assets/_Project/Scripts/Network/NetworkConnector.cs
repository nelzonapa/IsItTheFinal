using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.UI;

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(AudioSource))] 
    public class NetworkConnector : MonoBehaviour
    {
        [Header("Configuración de Sala")]
        public string roomName = "SalaColaborativa";

        [Header("Referencias UI")]
        public GameObject welcomePanel;
        public Button connectButton;
        public TextMeshProUGUI statusText;

        [Header("Configuración UX")]
        public float uiDistance = 1.5f;
        public float uiSmoothSpeed = 10f;
        public AudioClip successSound; // audio

        private NetworkRunner _runner;
        private bool _isFollowingUser = false;
        private Transform _mainCameraTransform;
        private AudioSource _audioSource; // Referencia al audio

        private void Start()
        {
            _runner = GetComponent<NetworkRunner>();
            if (_runner == null) _runner = gameObject.AddComponent<NetworkRunner>();

            _audioSource = GetComponent<AudioSource>(); // <--- Inicializar
            if (statusText != null) statusText.text = "Conectar a Sala";
        }

        private void Update()
        {
            // (Tu lógica de seguimiento de cámara sigue igual aquí...)
            if (_isFollowingUser && welcomePanel != null)
            {
                if (_mainCameraTransform == null)
                {
                    if (Camera.main != null) _mainCameraTransform = Camera.main.transform;
                    else return;
                }
                Vector3 targetPosition = _mainCameraTransform.position + (_mainCameraTransform.forward * uiDistance);
                welcomePanel.transform.position = Vector3.Lerp(welcomePanel.transform.position, targetPosition, Time.deltaTime * uiSmoothSpeed);
                welcomePanel.transform.rotation = Quaternion.LookRotation(welcomePanel.transform.position - _mainCameraTransform.position);
            }
        }

        public async void ConnectToRoom()
        {
            if (_runner == null) return;

            _isFollowingUser = true;

            if (connectButton != null) connectButton.interactable = false;
            if (statusText != null) statusText.text = "Conectando...";

            Debug.Log("Iniciando conexión a Fusion...");

            var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if (sceneRef.IsValid) sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);

            var result = await _runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                SessionName = roomName,
                Scene = sceneInfo,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            if (result.Ok)
            {
                Debug.Log("¡Conectado exitosamente!");

                if (statusText != null)
                {
                    statusText.text = "¡CONECTADO!";
                    statusText.color = Color.green;
                }

                // --- SONIDO DE ÉXITO ---
                if (_audioSource != null && successSound != null)
                {
                    _audioSource.PlayOneShot(successSound);
                }
                // -----------------------

                await Task.Delay(2000);

                _isFollowingUser = false;
                if (welcomePanel != null) welcomePanel.SetActive(false);
            }
            else
            {
                Debug.LogError($"Error al conectar: {result.ShutdownReason}");
                _isFollowingUser = false;

                if (statusText != null)
                {
                    statusText.text = "Error al conectar";
                    statusText.color = Color.red;
                }

                if (connectButton != null) connectButton.interactable = true;
                if (welcomePanel != null) welcomePanel.SetActive(true);
            }
        }
    }
}