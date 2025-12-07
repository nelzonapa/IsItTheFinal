using UnityEngine;
using TMPro;

namespace ImmersiveGraph.Visualization
{
    public class NodeLOD : MonoBehaviour
    {
        private TextMeshPro textMesh;
        private Transform mainCamera;

        // Ajustable desde el editor si lo necesitas
        public float cullingDistance = 15.0f; // Distancia a la que desaparece el texto
        private float checkInterval = 0.5f;   // Chequear 2 veces por segundo

        void Start()
        {
            textMesh = GetComponentInChildren<TextMeshPro>();
            if (Camera.main != null) mainCamera = Camera.main.transform;

            // Iniciar rutina de chequeo optimizada
            InvokeRepeating(nameof(CheckVisibility), Random.Range(0f, 1f), checkInterval);
        }

        void CheckVisibility()
        {
            if (textMesh == null || mainCamera == null) return;

            float distance = Vector3.Distance(transform.position, mainCamera.position);
            bool shouldShow = distance < cullingDistance;

            if (textMesh.enabled != shouldShow)
                textMesh.enabled = shouldShow;

            // Billboard: Que el texto siempre mire al usuario
            if (shouldShow)
            {
                // Invertimos la dirección para que el texto no salga en espejo
                textMesh.transform.rotation = Quaternion.LookRotation(textMesh.transform.position - mainCamera.position);
            }
        }
    }
}