using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;

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
                // Spawn del jugador
                Vector3 deskPosition = new Vector3(100, 0, 0); // La misma pos donde pusiste el SmartDesk
                runner.Spawn(playerPrefab, deskPosition, Quaternion.identity, player);
            }
        }

        // --- CALLBACKS OBLIGATORIOS (Actualizados para Fusion 2) ---

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }

        // Estos usaban tipos que requerían Fusion.Sockets:
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        // Estos cambiaron recientemente (agregaron ReliableKey):
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}