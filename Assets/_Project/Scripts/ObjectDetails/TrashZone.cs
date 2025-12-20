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

                // Ejecutamos la lógica de reversión
                token.DestroyAndRevert();
            }
        }
    }
}