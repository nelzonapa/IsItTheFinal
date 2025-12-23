using Fusion;
using TMPro;
using UnityEngine;

namespace ImmersiveGraph.Network
{
    public class NetworkTokenSync : NetworkBehaviour
    {
        [Header("Referencias")]
        public TextMeshProUGUI tokenLabel;

        // VARIABLES DE RED
        [Networked] public NetworkString<_64> TokenLabel { get; set; }

        // --- NUEVO: ID DEL NODO ORIGEN SINCRONIZADO ---
        [Networked] public NetworkString<_64> SourceNodeID { get; set; }
        // ----------------------------------------------

        public override void Spawned()
        {
            UpdateVisuals();
        }

        // Actualizamos la inicialización para recibir el sourceID
        public void InitializeToken(string content, string sourceID)
        {
            TokenLabel = content;
            SourceNodeID = sourceID; // Guardamos en la nube de Fusion
            UpdateVisuals();
        }

        public override void Render()
        {
            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            if (tokenLabel != null) tokenLabel.text = TokenLabel.ToString();
        }
    }
}