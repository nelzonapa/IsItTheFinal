using UnityEngine;
using TMPro;

namespace ImmersiveGraph.Interaction
{
    public class ExtractedToken : MonoBehaviour
    {
        [Header("UI del Token")]
        public TextMeshProUGUI labelText;

        // DATOS INTERNOS
        private SelectableText _sourceScript;
        private string _highlightID;

        // --- NUEVO: ID DEL NODO ORIGEN (La Memoria) ---
        public string OriginNodeID;
        // ----------------------------------------------

        // Actualizamos la firma para recibir el nodeID
        public void SetupToken(string text, SelectableText source, string hlId, string nodeID)
        {
            // 1. Visual
            if (labelText != null) labelText.text = text;

            // 2. Datos Lógicos
            _sourceScript = source;
            _highlightID = hlId;

            // 3. Guardar el Origen
            OriginNodeID = nodeID;
        }

        public void DestroyAndRevert()
        {
            if (_sourceScript != null)
            {
                _sourceScript.RemoveHighlight(_highlightID);
            }
            Destroy(gameObject);
        }
    }
}