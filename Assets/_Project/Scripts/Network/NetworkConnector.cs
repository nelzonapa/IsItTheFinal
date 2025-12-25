using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.UI;

namespace ImmersiveGraph.Network
{
    public class NetworkConnector : MonoBehaviour
    {
        [Header("Configuración de Sala")]
        public string roomName = "SalaColaborativa";

        [Header("Referencias UI")]
        public GameObject welcomePanel;
        public Button connectButton;
        public TextMeshProUGUI statusText;

        [Header("Configuración UX")]
        public float uiDistance = 1.5f; // A qué distancia flota el panel de la cara
        public float uiSmoothSpeed = 10f; // Qué tan rápido sigue al usuario

        private NetworkRunner _runner;
        private bool _isFollowingUser = false; // Flag para activar el movimiento
        private Transform _mainCameraTransform;

        private void Start()
        {
            _runner = GetComponent<NetworkRunner>();
            if (_runner == null) _runner = gameObject.AddComponent<NetworkRunner>();

            if (statusText != null) statusText.text = "Conectar a Sala";
        }

        private void Update()
        {
            // --- LÓGICA DE SEGUIMIENTO (TAG-ALONG) ---
            // Si estamos conectando, el Canvas persigue al usuario aunque se teletransporte
            if (_isFollowingUser && welcomePanel != null)
            {
                if (_mainCameraTransform == null)
                {
                    if (Camera.main != null) _mainCameraTransform = Camera.main.transform;
                    else return;
                }

                // 1. Calcular posición objetivo (frente a la cámara)
                Vector3 targetPosition = _mainCameraTransform.position + (_mainCameraTransform.forward * uiDistance);

                // Mantenemos la altura (Y) un poco ajustada para que no se meta en el suelo si miras abajo
                // Opcional: Si quieres que siga la mirada vertical estrictamente, borra la siguiente línea comentada
                // targetPosition.y = _mainCameraTransform.position.y; 

                // 2. Mover suavemente
                welcomePanel.transform.position = Vector3.Lerp(welcomePanel.transform.position, targetPosition, Time.deltaTime * uiSmoothSpeed);

                // 3. Rotar para mirar siempre al usuario
                // En UI World Space, para que nos mire, debe rotar hacia (PosPanel - PosCamara)
                welcomePanel.transform.rotation = Quaternion.LookRotation(welcomePanel.transform.position - _mainCameraTransform.position);
            }
        }

        public async void ConnectToRoom()
        {
            if (_runner == null) return;

            // ACTIVAR SEGUIMIENTO
            _isFollowingUser = true;

            // Feedback Inmediato
            if (connectButton != null) connectButton.interactable = false;
            if (statusText != null) statusText.text = "Conectando...";

            Debug.Log("Iniciando conexión a Fusion...");

            // Configuración de Escena
            var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if (sceneRef.IsValid)
            {
                sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);
            }

            // Iniciar Conexión
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

                // El usuario ya habrá sido teletransportado por Fusion aquí, 
                // pero como _isFollowingUser sigue true, el cartel habrá viajado con él.

                // Espera de 2 segundos
                await Task.Delay(2000);

                // Finalizar
                _isFollowingUser = false; // Dejar de seguir
                if (welcomePanel != null) welcomePanel.SetActive(false);
            }
            else
            {
                // Error
                Debug.LogError($"Error al conectar: {result.ShutdownReason}");
                _isFollowingUser = false; // Dejar de seguir si falló

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