using UnityEngine;

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class TrashZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            // Buscamos si lo que entró es un Token
            // Puede estar en el objeto raíz o en un padre si usas físicas compuestas
            ExtractedToken token = other.GetComponentInParent<ExtractedToken>();

            if (token != null)
            {
                Debug.Log("Token detectado en papelera. Eliminando...");
                token.DestroyAndRevert();
                return;
            }

            // 2. Intentar borrar Post-Its (Nuevo sistema)
            // Buscamos el script InfiniteStackItem (que tienen los post-its)
            // Ojo: Solo borramos si NO es el que está fijo en la pila (hasSpawnedReplacement == true)
            // O simplemente borramos cualquier objeto con el Tag "Connectable" que sea físico.

            // Verificamos si es un PostIt activo
            EditablePostIt postIt = other.GetComponentInParent<EditablePostIt>();
            if (postIt != null)
            {
                // Destruir también las líneas conectadas a él (opcional, pero recomendado para limpieza)
                // Por ahora solo destruimos el objeto
                Destroy(postIt.gameObject);
                Debug.Log("Post-It eliminado.");
            }
        }
    }
}