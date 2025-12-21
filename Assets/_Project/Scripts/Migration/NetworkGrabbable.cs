using UnityEngine;
using Fusion;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ImmersiveGraph.Network
{
    // Requerimos que el objeto tenga NetworkObject y sea agarrable
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class NetworkGrabbable : NetworkBehaviour
    {
        private XRGrabInteractable _interactable;

        private void Awake()
        {
            _interactable = GetComponent<XRGrabInteractable>();
        }

        public override void Spawned()
        {
            // Nos suscribimos al evento "Select Entered" (Al agarrar)
            _interactable.selectEntered.AddListener(OnGrab);
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            // Si ya soy el dueño, no hago nada
            if (Object.HasStateAuthority) return;

            Debug.Log($"Solicitando Autoridad sobre {gameObject.name}...");

            // ¡LA MAGIA DE FUSION!
            // Pedimos ser el dueño. Fusion gestionará la transferencia.
            Object.RequestStateAuthority();
        }

        // Opcional: Al soltar, podríamos devolver la autoridad, pero en VR 
        // es mejor quedársela hasta que otro la pida.
    }
}