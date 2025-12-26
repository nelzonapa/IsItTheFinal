using UnityEngine;
using Fusion;

namespace ImmersiveGraph.Core
{
    public static class UserColorPalette
    {
        // La misma paleta que definimos antes
        public static readonly Color[] PlayerColors = new Color[]
        {
            Color.cyan,      // Jugador 0 / Local Default
            Color.magenta,   // Jugador 1
            Color.green,     // Jugador 2
            Color.yellow     // Jugador 3
        };

        public static Color GetColor(int playerIndex)
        {
            return PlayerColors[Mathf.Abs(playerIndex) % PlayerColors.Length];
        }

        // Función inteligente: Detecta si estás conectado o no
        public static Color GetLocalPlayerColor()
        {
            // Intentamos buscar un Runner activo
            NetworkRunner runner = Object.FindFirstObjectByType<NetworkRunner>();

            if (runner != null && runner.IsRunning)
            {
                // Si estamos online, usamos nuestro ID real
                return GetColor(runner.LocalPlayer.PlayerId);
            }

            // Si estamos offline (Espacio Individual antes de conectar), usamos el color 0 (Cian)
            return GetColor(0);
        }
    }
}