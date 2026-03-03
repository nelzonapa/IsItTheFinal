using UnityEngine;

namespace ImmersiveGraph.Visual
{
    public class FloatingAnim : MonoBehaviour
    {
        public float amplitude = 0.05f; // Qué tanto sube y baja
        public float frequency = 2.0f;  // Qué tan rápido sube y baja
        public Vector3 rotationSpeed = new Vector3(0, 0, 0); // Velocidad de giro

        private Vector3 startPos;

        void Start()
        {
            startPos = transform.localPosition;
        }

        void Update()
        {
            // Movimiento suave arriba y abajo
            transform.localPosition = startPos + new Vector3(0, Mathf.Sin(Time.time * frequency) * amplitude, 0);

            // Rotación constante
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}