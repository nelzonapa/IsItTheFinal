using UnityEngine;
using TMPro;
using Unity.XR.CoreUtils; // Para buscar la cámara XR

namespace ImmersiveGraph.Visual
{
    public class NodeUIController : MonoBehaviour
    {
        [Header("Referencias UI")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI summaryText;

        private Transform _targetCamera;

        void Start()
        {
            // Buscar la cámara principal del XR Origin
            var xrOrigin = FindFirstObjectByType<XROrigin>();
            if (xrOrigin != null) _targetCamera = xrOrigin.Camera.transform;
            else _targetCamera = Camera.main.transform; // Fallback
        }

        public void SetupUI(string title, string summary)
        {
            titleText.text = title;

            // YA NO CORTAMOS EL TEXTO. Dejamos que Unity UI se encargue del tamaño.
            // Si viene vacío o nulo, ponemos un texto por defecto para que el panel no colapse a cero.
            if (string.IsNullOrEmpty(summary))
            {
                summaryText.text = "Sin información disponible.";
            }
            else
            {
                summaryText.text = summary;
            }

            // TRUCO PRO: A veces Unity UI tarda un frame en recalcular el tamaño.
            // Forzamos la actualización del layout para que no salga deforme el primer frame.
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }

        void Update()
        {
            if (_targetCamera != null)
            {
                // BILLBOARD: Hacer que el UI mire siempre al usuario
                // Usamos LookRotation invertido para que el texto no salga al revés
                transform.rotation = Quaternion.LookRotation(transform.position - _targetCamera.position);
            }
        }
    }
}