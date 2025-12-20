using UnityEngine;
using TMPro;

namespace ImmersiveGraph.Interaction
{
    public class ExtractedToken : MonoBehaviour
    {
        [Header("UI del Token")]
        public TextMeshProUGUI labelText; // El texto que se mostrará en la fichita

        // DATOS PARA LA PAPELERA
        // Guardamos quién nos creó y qué ID tenemos para poder borrarnos
        private SelectableText _sourceScript;
        private string _highlightID;

        public void SetupToken(string text, SelectableText source, string id)
        {
            // 1. Visual
            if (labelText != null) labelText.text = text;

            // 2. Datos Lógicos
            _sourceScript = source;
            _highlightID = id;
        }

        // Esta función será llamada por la TrashZone
        public void DestroyAndRevert()
        {
            if (_sourceScript != null)
            {
                // Le decimos al padre: "¡Borra el rojo del ID tal!"
                _sourceScript.RemoveHighlight(_highlightID);
            }

            // Efecto de sonido o partículas aquí si quieres...
            Destroy(gameObject);
        }
    }
}