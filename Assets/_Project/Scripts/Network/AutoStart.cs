using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(NetworkRunner))]
    public class AutoStart : MonoBehaviour
    {
        async void Start()
        {
            var runner = GetComponent<NetworkRunner>();

            if (runner.IsRunning) return;

            // 1) Crear SceneRef desde el buildIndex
            var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

            // 2) Construir NetworkSceneInfo
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);

            // 3) Iniciar el StartGame con ese sceneInfo
            await runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                SessionName = "Prueba CIA",
                Scene = sceneInfo,
                PlayerCount = 3
            });

            Debug.Log(">>> Auto-Start iniciado: Conectando a Prueba CIA...");
        }
    }
}
