using UnityEngine;
using UnityEngine.UI;

namespace ImmersiveGraph.Collaboration
{
    public class UIDashboardElement : MonoBehaviour
    {
        public enum ElementType { Token, PostIt, Line }

        [Header("Datos de Identidad")]
        public ElementType type;
        public string networkId;

        [Header("Datos Específicos (Solo Tokens)")]
        public string originDocumentID; // Aquí guardaremos de qué documento salió

        [Header("Referencias Visuales")]
        public RectTransform rectTransform;
        public Image visualImage;

        private void Awake()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (visualImage == null) visualImage = GetComponent<Image>();
        }

        // Función rápida para inyectar los datos cuando el Dashboard lo instancie
        public void Setup(string netId, Color playerColor, string documentId = "")
        {
            networkId = netId;
            originDocumentID = documentId;

            if (visualImage != null)
            {
                visualImage.color = playerColor;
            }
        }
    }
}