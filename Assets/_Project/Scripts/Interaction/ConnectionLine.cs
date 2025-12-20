using UnityEngine;

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(LineRenderer))]
    public class ConnectionLine : MonoBehaviour
    {
        [Header("Debug Info (Se llenan solos)")]
        public Transform startNode;
        public Transform endNode;

        private LineRenderer _lr;

        void Awake()
        {
            _lr = GetComponent<LineRenderer>();

            // CORRECCIÓN 1: Coordenadas Mundiales
            _lr.useWorldSpace = true;
            _lr.positionCount = 2;

            // CORRECCIÓN 2: Evitar el color ROSA si falta material
            if (_lr.sharedMaterial == null)
            {
                _lr.material = new Material(Shader.Find("Sprites/Default")); // Material blanco básico
            }
        }

        public void Initialize(Transform start, Transform end)
        {
            startNode = start;
            endNode = end;

            // Actualización inmediata para que no aparezca en la cara el primer frame
            UpdatePositions();
        }

        void LateUpdate() // Usamos LateUpdate para que se mueva SUAVE después de que los nodos se muevan
        {
            // Lógica de destrucción automática si un nodo muere (TrashZone)
            if (startNode == null || endNode == null)
            {
                Destroy(gameObject); // La línea muere, el otro nodo vive.
                return;
            }

            UpdatePositions();
        }

        void UpdatePositions()
        {
            if (_lr != null && startNode != null && endNode != null)
            {
                _lr.SetPosition(0, startNode.position);
                _lr.SetPosition(1, endNode.position);
            }
        }
    }
}