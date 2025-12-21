using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using Unity.XR.CoreUtils; // <--- NECESARIO PARA MOVER LA CÁMARA

namespace ImmersiveGraph.Network
{
    public class NetworkPlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Arrastra aquí tu NetworkPlayer")]
        public NetworkObject playerPrefab;

        private NetworkRunner _runner;

        void Start()
        {
            _runner = GetComponent<NetworkRunner>();
            if (_runner != null)
            {
                _runner.AddCallbacks(this);
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (player == runner.LocalPlayer)
            {
                Debug.Log("¡Entré a la sala! Creando mi avatar...");

                Debug.Log($"Jugador {player.PlayerId} conectado. Buscando su oficina...");

                Vector3 spawnPosition = new Vector3(0, 1, 0); // Default
                Quaternion spawnRotation = Quaternion.identity; // Default

                // Preguntar al Manager cuál es mi escritorio
                if (GroupTableManager.Instance != null)
                {
                    Transform myDesk = GroupTableManager.Instance.GetIndividualDeskForPlayer(player);
                    if (myDesk != null)
                    {
                        spawnPosition = myDesk.position;
                        spawnRotation = myDesk.rotation; // <--- NUEVO: Copiar rotación también
                    }
                }
                else
                {
                    // Si no hay Manager, usamos el 100 como backup manual
                    spawnPosition = new Vector3(100, 1, 0);
                }

                // 1. SPAWN DEL AVATAR (Lo que ven los otros)
                runner.Spawn(playerPrefab, spawnPosition, spawnRotation, player);

                // 2. MOVER LA CÁMARA REAL (Lo que ves tú) <--- IMPORTANTE
                XROrigin origin = FindFirstObjectByType<XROrigin>();
                if (origin != null)
                {
                    // Ajuste de altura (si el pivote está en el suelo)
                    origin.transform.position = spawnPosition;

                    // Ajuste de rotación (solo eje Y para no marear)
                    origin.transform.rotation = Quaternion.Euler(0, spawnRotation.eulerAngles.y, 0);

                    Debug.Log("Cámara movida al escritorio individual.");
                }

                // NUEVO: Entregar herramientas en la mesa grupal (para que estén listas cuando viajes)
                if (GroupTableManager.Instance != null)
                {
                    GroupTableManager.Instance.SpawnToolsForPlayer(runner, player);
                }
            }
        }

        // --- CALLBACKS OBLIGATORIOS ---
        // (El resto déjalo igual, solo los vacíos)
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}