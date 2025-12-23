using UnityEngine;
using Fusion;

namespace ImmersiveGraph.Network
{
    public class NetworkObjectColor : NetworkBehaviour
    {
        [Header("Configuración")]
        public Renderer targetRenderer; // Arrastra el MeshRenderer del cubo aquí

        // Paleta de colores para diferenciar usuarios (puedes agregar más)
        private Color[] playerColors = new Color[]
        {
            Color.cyan,      // Jugador 0
            Color.magenta,   // Jugador 1
            Color.green,     // Jugador 2
            Color.yellow     // Jugador 3
        };

        public override void Spawned()
        {
            ApplyColorBasedOnOwner();
        }

        // Llamamos a esto también cuando cambie la autoridad (si alguien se lo roba)
        // Opcional: Si quieres que cambie de color según quien lo agarra, descomenta FixedUpdateNetwork
        // public override void FixedUpdateNetwork() { ApplyColorBasedOnOwner(); }

        void ApplyColorBasedOnOwner()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null) return;

            // Obtenemos quién es el "Dueño" (StateAuthority) del objeto
            PlayerRef owner = Object.StateAuthority;

            // Usamos el PlayerId para elegir un color
            // El PlayerId suele empezar en 1 o 0 dependiendo de la sesión.
            // Usamos el operador módulo (%) para que si hay más jugadores que colores, se repitan en ciclo.
            int colorIndex = Mathf.Abs(owner.PlayerId) % playerColors.Length;

            Color assignedColor = playerColors[colorIndex];

            // Aplicamos el color al material
            targetRenderer.material.color = assignedColor;
        }
    }
}