using UnityEngine;
using TMPro;

namespace ImmersiveGraph.Interaction
{
    // Añade este script al padre del PostIt_Prefab
    public class EditablePostIt : MonoBehaviour
    {
        [Header("Referencias UI")]
        public TMP_InputField inputField;

        void Start()
        {
            // Configuración para que el InputField crezca
            if (inputField != null)
            {
                // Asegurar que el teclado virtual aparezca al hacer clic (XRI UI Module lo hace auto)
                inputField.shouldHideMobileInput = false;
            }
        }
    }
}