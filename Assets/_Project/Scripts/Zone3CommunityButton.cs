using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ImmersiveGraph.Interaction;

namespace ImmersiveGraph.Visual
{
    public class Zone3CommunityButton : MonoBehaviour
    {
        [Header("Referencias")]
        public GraphInteractionManager interactionManager;

        [Header("UI Elements")]
        public Button actionButton;
        public TextMeshProUGUI buttonText;

        [Header("Textos")]
        public string textOpen = "Abrir nodo para ver archivos";
        public string textClose = "Cerrar grafo de archivos";

        private CanvasGroup _buttonCanvasGroup;

        void Start()
        {
            if (actionButton != null)
            {
                _buttonCanvasGroup = actionButton.GetComponent<CanvasGroup>();
                if (_buttonCanvasGroup == null)
                {
                    _buttonCanvasGroup = actionButton.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        void Update()
        {
            if (actionButton == null || _buttonCanvasGroup == null) return;

            GraphNode currentNode = GraphNode.CurrentSelectedLocalNode;
            GraphNode activeCommunity = interactionManager.CurrentFocusedCommunity;

            bool shouldShowButton = false;
            string currentText = "";

            if (currentNode != null)
            {
                if (currentNode.nodeType == "community")
                {
                    // Si el usuario tiene seleccionada una comunidad, vemos si es la que está abierta o es una nueva
                    shouldShowButton = true;
                    if (activeCommunity == currentNode)
                    {
                        currentText = textClose;
                    }
                    else
                    {
                        currentText = textOpen;
                    }
                }
                else if (currentNode.nodeType == "file" && activeCommunity != null)
                {
                    // Si el usuario tiene seleccionado un archivo, pero hay una comunidad abierta en la mesa
                    shouldShowButton = true;
                    currentText = textClose;
                }
            }

            // Aplicamos la visibilidad y el texto según la lógica anterior
            if (shouldShowButton)
            {
                _buttonCanvasGroup.alpha = 1f;
                _buttonCanvasGroup.interactable = true;
                _buttonCanvasGroup.blocksRaycasts = true;
                buttonText.text = currentText;
            }
            else
            {
                // Escondemos el botón si agarra el Root o si no hay nada seleccionado
                _buttonCanvasGroup.alpha = 0f;
                _buttonCanvasGroup.interactable = false;
                _buttonCanvasGroup.blocksRaycasts = false;
            }
        }

        public void OnButtonClicked()
        {
            GraphNode currentNode = GraphNode.CurrentSelectedLocalNode;
            GraphNode activeCommunity = interactionManager.CurrentFocusedCommunity;

            if (currentNode != null)
            {
                if (currentNode.nodeType == "community")
                {
                    // Si seleccionó una comunidad y hace clic, hace toggle de esa comunidad
                    interactionManager.ToggleCommunityNode(currentNode);
                }
                else if (currentNode.nodeType == "file" && activeCommunity != null)
                {
                    // Si tiene un archivo seleccionado y hace clic, cierra la comunidad activa contenedora
                    interactionManager.ToggleCommunityNode(activeCommunity);
                }
            }
        }
    }
}