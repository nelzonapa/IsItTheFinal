using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace ImmersiveGraph.Visual
{
    public class NodeLoaderController : MonoBehaviour
    {
        [Header("Referencias")]
        public Image fillImage; // La imagen horizontal que se llena (Type: Filled)

        private Transform _targetCamera;

        void Start()
        {
            var xrOrigin = FindFirstObjectByType<XROrigin>();
            if (xrOrigin != null) _targetCamera = xrOrigin.Camera.transform;
            else _targetCamera = Camera.main.transform;

            // Asegurar que empiece vacía
            SetProgress(0);
        }

        public void SetProgress(float value)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = value;
            }
        }

        void Update()
        {
            // Billboard: Mirar siempre al usuario
            if (_targetCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - _targetCamera.position);
            }
        }
    }
}