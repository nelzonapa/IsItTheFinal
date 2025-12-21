using UnityEngine;
using Fusion;

namespace ImmersiveGraph.Network
{
    public class NetworkObjectColor : NetworkBehaviour
    {
        public Renderer targetRenderer; // El MeshRenderer del PostIt o Token

        // Color predefinido para cada jugador (0, 1, 2...)
        // Puedes ampliar esto o conectarlo a tu GroupTableManager
        private Color[] playerColors = new Color[]
        {
            Color.cyan,      // Jugador 0
            Color.magenta,   // Jugador 1
            Color.yellow,    // Jugador 2
            Color.green      // Jugador 3
        };

        public override void Spawned()
        {
            ApplyColor();
        }

        // Se llama también cuando cambia el dueño (si implementas cambio de autoridad)
        public override void FixedUpdateNetwork()
        {
            // Opcional: Si permites robar objetos, descomenta esto para actualizar color
            // ApplyColor(); 
        }

        void ApplyColor()
        {
            if (targetRenderer == null) return;

            // Obtenemos el ID del jugador dueño de este objeto
            int playerId = Object.StateAuthority.PlayerId;

            // Selección segura de color
            Color c = playerColors[playerId % playerColors.Length];

            targetRenderer.material.color = c;
        }
    }
}