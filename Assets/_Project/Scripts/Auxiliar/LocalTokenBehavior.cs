using UnityEngine;
using TMPro; // Necesario para leer el texto

namespace ImmersiveGraph.Data
{
    public class LocalTokenBehavior : MonoBehaviour
    {
        [Header("Referencia al Texto")]
        [Tooltip("Arrastra aquí el componente de texto (TMP) del token")]
        public TextMeshProUGUI contentInputField;
        // Si usas TextMeshProUGUI normal (no editable), cambia la línea de arriba por:
        // public TextMeshProUGUI contentText;

        /// <summary>
        /// Devuelve el texto actual del Token para guardarlo.
        /// </summary>
        public string GetContent()
        {
            if (contentInputField != null) return contentInputField.text;
            // if (contentText != null) return contentText.text;
            return "";
        }

        /// <summary>
        /// Escribe el texto en el Token al cargarlo.
        /// </summary>
        public void SetContent(string newContent)
        {
            if (contentInputField != null) contentInputField.text = newContent;
            // if (contentText != null) contentText.text = newContent;
        }
    }
}