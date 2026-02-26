using UnityEngine;
using ImmersiveGraph.Core;

namespace ImmersiveGraph.Collaboration
{
    public class MiniWorldManager : MonoBehaviour
    {
        [Header("Configuración del Espacio")]
        [Tooltip("El objeto vacío que servirá como centro (0,0,0) del minimundo.")]
        public Transform miniWorldRoot;

        [Tooltip("Escala a la que se generará el holograma (ej. 0.05 es el 5% del tamaño real).")]
        public float scaleFactor = 0.05f;

        // Aquí guardaremos más adelante las referencias de los mini-avatares y los colores

        void Start()
        {
            if (miniWorldRoot == null)
            {
                Debug.LogError("[MiniWorld] Falta asignar el MiniWorld_Root en el inspector.");
            }
        }

        // Preparado para la FASE 2: Recibir la orden de construir
        public void BuildMiniatureFromRealGraph(Transform realRootNode)
        {
            Debug.Log("[MiniWorld] Iniciando construcción del holograma...");
            // Lógica pendiente para la Fase 2
        }
    }
}