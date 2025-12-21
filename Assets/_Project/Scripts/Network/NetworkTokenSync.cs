using UnityEngine;
using Fusion;
using TMPro;

namespace ImmersiveGraph.Network
{
    public class NetworkTokenSync : NetworkBehaviour
    {
        [Header("Referencias")]
        public TextMeshProUGUI labelText;

        [Networked, OnChangedRender(nameof(OnLabelChanged))]
        public NetworkString<_64> TokenLabel { get; set; }

        // Guardamos el ID original por si necesitamos lógica de trazabilidad
        [Networked] public NetworkString<_64> OriginalHighlightID { get; set; }

        public override void Spawned()
        {
            if (!Object.HasStateAuthority)
            {
                labelText.text = TokenLabel.ToString();
            }
        }

        // Método para inicializarlo al nacer (lo llamará el MigrationManager)
        public void InitializeToken(string text, string id)
        {
            if (Object.HasStateAuthority)
            {
                TokenLabel = text;
                OriginalHighlightID = id;
            }
        }

        void OnLabelChanged()
        {
            labelText.text = TokenLabel.ToString();
        }
    }
}