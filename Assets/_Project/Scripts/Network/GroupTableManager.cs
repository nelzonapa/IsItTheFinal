using UnityEngine;
using Fusion;

namespace ImmersiveGraph.Network
{
    public class GroupTableManager : NetworkBehaviour
    {
        public static GroupTableManager Instance;

        [Header("Zonas de Recepción (Bandejas en la mesa)")]
        public Transform[] receptionZones;

        [Header("Puntos de Aparición (Donde se para el jugador)")]
        [Tooltip("Arrastra aquí SpawnPoint_1, SpawnPoint_2, etc.")]
        public Transform[] userSpawnPoints;

        // --- Puntos de Aparición INDIVIDUAL (Escritorios privados) ---
        [Header("Escritorios Individuales")]
        [Tooltip("Arrastra aquí el SpawnPoint_Individual de cada SmartDesk (Desk1, Desk2...)")]
        public Transform[] individualDeskSpawns;

        [Header("Zona Central")]
        public Transform timelineZone;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public Transform GetReceptionZoneForPlayer(PlayerRef player)
        {
            if (receptionZones.Length == 0) return transform;
            return receptionZones[player.PlayerId % receptionZones.Length];
        }

        public Transform GetSpawnPointForPlayer(PlayerRef player)
        {
            if (userSpawnPoints.Length == 0) return transform;

            // Usamos la misma lógica: ID % Cantidad
            // Esto asegura que si te toca la Bandeja 1, te toque el SpawnPoint 1
            return userSpawnPoints[player.PlayerId % userSpawnPoints.Length];
        }

        public Transform GetIndividualDeskForPlayer(PlayerRef player)
        {
            if (individualDeskSpawns.Length == 0) return transform; // Fallback

            // Asignar escritorio según ID (Jugador 1 -> Escritorio 1)
            return individualDeskSpawns[player.PlayerId % individualDeskSpawns.Length];
        }
    }
}