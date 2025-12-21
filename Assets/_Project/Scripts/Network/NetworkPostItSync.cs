using Fusion;
using ImmersiveGraph.Interaction;
using TMPro;
using UnityEngine;

namespace ImmersiveGraph.Network
{
    public class NetworkPostItSync : NetworkBehaviour
    {
        [Header("Referencias")]
        public TMP_InputField inputField;
        public EditablePostIt localLogic; // Referencia al script que estira el fondo

        // Variable Sincronizada: Capacidad de 512 caracteres
        [Networked, OnChangedRender(nameof(OnContentChanged))]
        public NetworkString<_512> NetworkContent { get; set; }

        public override void Spawned()
        {
            // Si SOY el dueño, inicializo el texto de red con lo que tenga el input
            if (Object.HasStateAuthority)
            {
                NetworkContent = inputField.text;
            }
            else
            {
                // Si NO soy el dueño, recibo el texto de la red
                inputField.text = NetworkContent.ToString();

                // Bloqueamos el input para que solo el dueño pueda editar (Opcional, 
                // pero recomendado para evitar conflictos de escritura simultánea)
                // inputField.interactable = false; 
            }

            // Escuchar cambios locales para enviarlos a la red
            inputField.onValueChanged.AddListener(OnLocalInputChanged);
        }

        public void OnLocalInputChanged(string text)
        {
            // Solo si tengo autoridad escribo en la variable Networked
            if (Object.HasStateAuthority)
            {
                NetworkContent = text;
            }
        }

        // Se ejecuta en TODOS los clientes cuando NetworkContent cambia
        void OnContentChanged()
        {
            // Evitar bucle infinito: Si el texto ya es igual, no hacemos nada
            if (inputField.text != NetworkContent.ToString())
            {
                inputField.text = NetworkContent.ToString();

                // Forzamos al script visual a recalcular el tamaño del fondo
                if (localLogic != null)
                {
                    // Llamamos a un método público que expongamos en EditablePostIt
                    // O simplemente el evento onValueChanged lo hará disparar solo.
                    localLogic.SendMessage("OnTextChanged", inputField.text, SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }
}