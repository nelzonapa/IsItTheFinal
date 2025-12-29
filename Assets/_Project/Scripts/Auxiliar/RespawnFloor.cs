using UnityEngine;
using Unity.XR.CoreUtils;
using ImmersiveGraph.Core;

namespace ImmersiveGraph.Core
{
    public class RespawnFloor : MonoBehaviour
    {
        [Tooltip("Arrastra aquí un objeto vacío (Empty) que esté SEGURO en el piso (Y > 0)")]
        public Transform respawnPoint;

        private void OnTriggerEnter(Collider other)
        {
            if (respawnPoint == null)
            {
                Debug.LogError("[Respawn] ¡Falta asignar el RespawnPoint!");
                return;
            }

            // 1. Detectar Jugador PC
            var fpsController = other.GetComponentInParent<SimpleFPSController>();
            if (fpsController != null)
            {
                // Pasamos el transform RAÍZ
                TeleportWithReset(fpsController.transform);
                return;
            }

            // 2. Detectar Jugador VR
            var xrOrigin = other.GetComponentInParent<XROrigin>();
            if (xrOrigin != null)
            {
                TeleportWithReset(xrOrigin.transform);
                return;
            }
        }

        void TeleportWithReset(Transform target)
        {
            Debug.Log($"[Respawn] Reiniciando posición de {target.name}...");

            // 1. APAGAR EL OBJETO COMPLETO
            // Esto detiene scripts de movimiento, inputs y resetea el CharacterController
            target.gameObject.SetActive(false);

            // 2. MOVER
            target.position = respawnPoint.position;
            target.rotation = respawnPoint.rotation;

            // 3. ENCENDER
            // Al reactivarse, el CharacterController aparece "fresco" en la nueva posición
            target.gameObject.SetActive(true);
        }
    }
}