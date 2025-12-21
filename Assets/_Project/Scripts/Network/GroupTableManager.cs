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
        public Transform[] individualDeskSpawns;

        [Header("Zona Central")]
        public Transform timelineZone;

        [Header("--- FASE 4: KIT DE HERRAMIENTAS ---")]
        public NetworkObject netSmartPenPrefab;
        public NetworkObject netNoteBlockPrefab;

        // NUEVO: Puntos exactos para las herramientas
        [Header("Spawns Exactos de Herramientas")]
        [Tooltip("Crea objetos vacíos en la mesa para cada jugador: PenPos_0, PenPos_1...")]
        public Transform[] penSpawnPoints;

        [Tooltip("Crea objetos vacíos en la mesa para cada jugador: BlockPos_0, BlockPos_1...")]
        public Transform[] blockSpawnPoints;

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
            return userSpawnPoints[player.PlayerId % userSpawnPoints.Length];
        }

        public Transform GetIndividualDeskForPlayer(PlayerRef player)
        {
            if (individualDeskSpawns.Length == 0) return transform;
            return individualDeskSpawns[player.PlayerId % individualDeskSpawns.Length];
        }

        // --- SPAWN DE HERRAMIENTAS CORREGIDO ---
        public void SpawnToolsForPlayer(NetworkRunner runner, PlayerRef player)
        {
            // Calculamos el índice seguro
            int index = player.PlayerId;

            // 1. OBTENER PUNTO DEL LÁPIZ
            Transform targetPenTrans = null;
            if (penSpawnPoints.Length > 0)
            {
                targetPenTrans = penSpawnPoints[index % penSpawnPoints.Length];
            }

            // 2. OBTENER PUNTO DEL BLOCK
            Transform targetBlockTrans = null;
            if (blockSpawnPoints.Length > 0)
            {
                targetBlockTrans = blockSpawnPoints[index % blockSpawnPoints.Length];
            }

            // SI NO HAY PUNTOS CONFIGURADOS, USAMOS LA BANDEJA COMO RESPALDO (Fallback)
            if (targetPenTrans == null) targetPenTrans = GetReceptionZoneForPlayer(player);
            if (targetBlockTrans == null) targetBlockTrans = GetReceptionZoneForPlayer(player);

            // 3. SPAWNEAR
            if (netSmartPenPrefab != null)
            {
                runner.Spawn(netSmartPenPrefab, targetPenTrans.position, targetPenTrans.rotation, player);
            }

            if (netNoteBlockPrefab != null)
            {
                runner.Spawn(netNoteBlockPrefab, targetBlockTrans.position, targetBlockTrans.rotation, player);
            }

            Debug.Log($"Kit de herramientas entregado al Jugador {player.PlayerId} en posiciones fijas.");
        }
    }
}