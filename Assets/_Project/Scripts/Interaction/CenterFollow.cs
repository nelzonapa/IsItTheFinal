using UnityEngine;
namespace ImmersiveGraph.Interaction
{
    public class CenterFollow : MonoBehaviour
    {
        public Transform a, b;

        void Update()
        {
            // Si alguno desaparece, este objeto (el cubo de borrado) también debería desaparecer
            // Pero normalmente el padre (ConnectionLine) se destruye antes, así que esto es seguro.
            if (a != null && b != null)
            {
                transform.position = (a.position + b.position) / 2;
            }
        }
    }
}