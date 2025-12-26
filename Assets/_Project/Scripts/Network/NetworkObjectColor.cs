using UnityEngine;
using Fusion;
using ImmersiveGraph.Core;

namespace ImmersiveGraph.Network
{
    public class NetworkObjectColor : NetworkBehaviour
    {
        [Header("Configuración")]
        public Renderer targetRenderer; //MeshRenderer del cubo aquí

        public override void Spawned()
        {
            ApplyColorBasedOnOwner();
        }

        void ApplyColorBasedOnOwner()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null) return;

            // Obtenemos quién es el "Dueño"
            PlayerRef owner = Object.StateAuthority;

            // Usamos la paleta compartida
            Color assignedColor = UserColorPalette.GetColor(owner.PlayerId);

            targetRenderer.material.color = assignedColor;
        }
    }
}