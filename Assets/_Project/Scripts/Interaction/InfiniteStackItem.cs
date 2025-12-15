using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ImmersiveGraph.Interaction
{
    // Poner esto en el Prefab del Post-it
    [RequireComponent(typeof(XRGrabInteractable))]
    public class InfiniteStackItem : MonoBehaviour
    {
        private bool hasSpawnedReplacement = false;
        private Vector3 startPosition;
        private Quaternion startRotation;

        void Start()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;

            var grab = GetComponent<XRGrabInteractable>();
            grab.selectEntered.AddListener(OnGrab);
        }

        void OnGrab(SelectEnterEventArgs args)
        {
            if (!hasSpawnedReplacement)
            {
                hasSpawnedReplacement = true; // Yo ya soy libre, ya no genero más

                // Crear mi reemplazo en la mesa
                GameObject replacement = Instantiate(this.gameObject, startPosition, startRotation);
                replacement.name = this.gameObject.name; // Mantener nombre limpio

                // Importante: El reemplazo nace "nuevo" (hasSpawnedReplacement = false), 
                // así que estará listo para el siguiente agarre.
            }
        }
    }
}