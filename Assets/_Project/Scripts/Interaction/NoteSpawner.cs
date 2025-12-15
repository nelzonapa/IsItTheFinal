using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ImmersiveGraph.Interaction
{
    // Este script va en el objeto "Bloque de Notas" fijo en la mesa
    public class NoteSpawner : MonoBehaviour
    {
        public GameObject notePrefab; // El prefab de la nota individual (Post-it)

        // Usamos un evento simple del XR Toolkit
        public void OnGrabAttempt(SelectEnterEventArgs args)
        {
            // 1. Identificar quién está intentando agarrar (La mano)
            IXRSelectInteractor interactor = args.interactorObject;

            // 2. Forzar que la mano suelte este bloque (porque el bloque es infinito/fijo)
            // Nota: Esto depende de cómo configures el Interactable, 
            // pero una forma más limpia es INSTANCIAR la nota y dársela a la mano.
        }

        // MÉTODO ALTERNATIVO MÁS FÁCIL PARA UNITY XR:
        // El bloque es un botón o un objeto estático. Al tocarlo o pulsar gatillo sobre él:

        public void SpawnNote(Transform spawnPoint)
        {
            GameObject newNote = Instantiate(notePrefab, spawnPoint.position, spawnPoint.rotation);
            newNote.name = $"PostIt_{Time.time}";

            // La nota nueva debe tener su propio XRGrabInteractable y Rigidbody
        }
    }
}