using ImmersiveGraph.Core; // <--- NECESARIO
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class InfiniteStackItem : MonoBehaviour
    {
        private bool hasSpawnedReplacement = false;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Renderer _myRenderer; // Para cambiar mi color

        void Start()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
            _myRenderer = GetComponentInChildren<Renderer>(); // Obtenemos referencia visual

            var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.selectEntered.AddListener(OnGrab);
        }

        void OnGrab(SelectEnterEventArgs args)
        {
            if (!hasSpawnedReplacement)
            {
                hasSpawnedReplacement = true; // Ya soy libre

                // 1. PINTARME A M� MISMO (El que tengo en la mano)
                if (_myRenderer != null)
                {
                    _myRenderer.material.color = UserColorPalette.GetLocalPlayerColor();
                }

                // 2. CREAR EL REEMPLAZO EN LA MESA
                GameObject replacement = Instantiate(this.gameObject, startPosition, startRotation);
                replacement.name = this.gameObject.name;

                // 3. RESTAURAR EL COLOR DEL REEMPLAZO
                // Como 'replacement' es una copia de 'this' (que acabamos de pintar),
                // nacer� pintado. Hay que devolverlo al color original (ej. blanco o amarillo p�lido).
                var repRenderer = replacement.GetComponentInChildren<Renderer>();
                if (repRenderer != null)
                {
                    // Asumiendo que el color base de tus post-its es blanco o un amarillo claro por defecto.
                    // Si tienes un color espec�fico, ponlo aqu�.
                    repRenderer.material.color = Color.white;
                }
            }
        }
    }
}