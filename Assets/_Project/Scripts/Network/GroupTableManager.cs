using UnityEngine;
using Fusion;
using System.Collections.Generic;

namespace ImmersiveGraph.Network
{
    public class GroupTableManager : NetworkBehaviour
    {
        public static GroupTableManager Instance;

        [Header("Zonas de Recepción (Bandejas)")]
        [Tooltip("Arrastra aquí los 4 objetos ReceptionZone en orden")]
        public Transform[] receptionZones;

        [Header("Zona Central")]
        public Transform timelineZone;

        private void Awake()
        {
            // Singleton simple para poder llamarlo desde cualquier lado
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // Función para saber dónde debe aparecer el objeto de un jugador
        public Transform GetReceptionZoneForPlayer(PlayerRef player)
        {
            // Lógica simple: Usamos el ID del jugador y el módulo de la cantidad de zonas
            // Jugador 1 -> Zona 1, Jugador 5 -> Zona 1 (si hay 4 zonas)
            int zoneIndex = player.PlayerId % receptionZones.Length;

            // Protección por si las zonas no están asignadas
            if (receptionZones.Length == 0) return transform;

            return receptionZones[zoneIndex];
        }
    }
}