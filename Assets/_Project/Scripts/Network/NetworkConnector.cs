using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

namespace ImmersiveGraph.Network
{
    public class NetworkConnector : MonoBehaviour
    {
        [Header("Configuración de Sala")]
        public string roomName = "SalaColaborativa";

        [Header("Referencias UI")]
        public GameObject welcomePanel; // El panel que contiene el botón (para ocultarlo al conectar)

        private NetworkRunner _runner;

        private void Start()
        {
            // Buscamos el runner que creamos en el AppManager
            _runner = GetComponent<NetworkRunner>();
            if (_runner == null) _runner = gameObject.AddComponent<NetworkRunner>();
        }

        // --- ESTA FUNCIÓN SE LLAMA DESDE EL BOTÓN VR ---
        public async void ConnectToRoom()
        {
            if (_runner == null) return;

            if (welcomePanel != null) welcomePanel.SetActive(false);
            Debug.Log("Iniciando conexión a Fusion...");

            // --- Construir NetworkSceneInfo a partir de la escena actual ---
            var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if (sceneRef.IsValid)
            {
                // Usa LoadSceneMode.Single o Additive según lo que necesites
                sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("SceneRef inválido. Asegúrate de que la escena esté incluida en Build Settings.");
                // Opcional: dejar sceneInfo vacío (null) si prefieres no establecer escena
            }

            var result = await _runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                SessionName = roomName,
                Scene = sceneInfo, // <-- ahora pasamos NetworkSceneInfo en lugar de int
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            if (result.Ok)
            {
                Debug.Log("¡Conectado exitosamente!");
            }
            else
            {
                Debug.LogError($"Error al conectar: {result.ShutdownReason}");
                if (welcomePanel != null) welcomePanel.SetActive(true);
            }
        }
    }
}