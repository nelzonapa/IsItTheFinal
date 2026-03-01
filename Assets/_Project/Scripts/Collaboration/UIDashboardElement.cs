using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ImmersiveGraph.Collaboration
{
    public class UIDashboardElement : MonoBehaviour
    {
        public enum ElementType { Token, PostIt, Line }

        [Header("Datos de Identidad")]
        public ElementType type;
        public string networkId;

        [Header("Datos Específicos")]
        public string originDocumentID;

        [Header("Referencias Visuales")]
        public RectTransform rectTransform;
        public Image visualImage;
        public TextMeshProUGUI contentText; // NUEVO: Referencia al texto

        private void Awake()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (visualImage == null) visualImage = GetComponent<Image>();
        }

        // NUEVO: Ahora recibe el string 'textContent'
        public void Setup(string netId, Color playerColor, string textContent = "", string documentId = "")
        {
            networkId = netId;
            originDocumentID = documentId;

            if (visualImage != null)
            {
                visualImage.color = playerColor;
            }

            // Aplicamos el texto si existe el componente
            if (contentText != null)
            {
                contentText.text = textContent;
            }
        }
    }
}