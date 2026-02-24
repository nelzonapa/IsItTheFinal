using UnityEngine;
using Fusion; // Necesario para saber qué ID de jugador somos
using UnityEngine.XR.Interaction.Toolkit; // Necesario para detectar el click VR
using ImmersiveGraph.Core; // Necesario para acceder al PlatformManager

namespace ImmersiveGraph.Network
{
    // Este componente requiere un Collider para poder ser "tocado"
    [RequireComponent(typeof(BoxCollider))]
    // Este componente requiere el sistema de interacción simple de Unity
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
    public class ReturnToIndividual : MonoBehaviour
    {
        [Header("Referencias de Destino")]
        [Tooltip("Arrastra aquí el objeto SpawnPointUser1 de la Jerarquía")]
        public Transform spawnPointUser1;

        [Tooltip("Arrastra aquí el objeto SpawnPointUser2 de la Jerarquía")]
        public Transform spawnPointUser2;

        [Header("Sonido (Opcional)")]
        public AudioSource audioSource;
        public AudioClip returnSound;

        private NetworkRunner _runner;

        void Start()
        {
            // Buscamos la conexión de red activa
            _runner = FindFirstObjectByType<NetworkRunner>();
        }

        // Esta función será llamada cuando presiones el botón
        public void ReturnHome()
        {
            // 1. Reproducir sonido si existe
            if (audioSource != null && returnSound != null)
            {
                audioSource.PlayOneShot(returnSound);
            }

            Debug.Log("Intentando volver a casa...");

            // 2. Identificar al Jugador
            if (_runner == null) _runner = FindFirstObjectByType<NetworkRunner>();

            if (_runner == null || !_runner.IsRunning)
            {
                Debug.LogWarning("No hay conexión de red. Asumiendo Modo Debug (Jugador 1).");
                TeleportPlayer(spawnPointUser1); // Por defecto si no hay red
                return;
            }

            // Obtenemos el ID del jugador local (0, 1, 2...)
            int myPlayerID = _runner.LocalPlayer.PlayerId;
            Debug.Log($"Soy el Jugador ID: {myPlayerID}");

            // 3. Decidir a dónde ir según el ID
            // NOTA: Fusion asigna IDs dinámicos. Usualmente el Host es el ID más bajo.
            // Por ahora usaremos par/impar o orden de llegada para simplificar tu prueba.
            // Si eres el Host (usualmente el primero), vas al 1. Si eres el cliente, vas al 2.

            if (_runner.IsSharedModeMasterClient || myPlayerID == 0)
            {
                TeleportPlayer(spawnPointUser1);
            }
            else
            {
                TeleportPlayer(spawnPointUser2);
            }
        }

        private void TeleportPlayer(Transform target)
        {
            if (target == null)
            {
                Debug.LogError("¡ERROR! No has asignado el SpawnPoint en el Inspector.");
                return;
            }

            // Buscamos quién es el jugador (VR o PC) gracias a tu PlatformManager
            GameObject playerRig = null;

            if (PlatformManager.Instance != null && PlatformManager.Instance.ActiveRig != null)
            {
                playerRig = PlatformManager.Instance.ActiveRig;
            }
            else
            {
                // Plan B: Buscar el XR Origin manualmente
                var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (xrOrigin != null) playerRig = xrOrigin.gameObject;
            }

            if (playerRig != null)
            {
                // Mover al jugador
                playerRig.transform.position = target.position;

                // Rotar al jugador para que mire al frente del escritorio
                // Solo rotamos en el eje Y (horizontal) para no marearlo
                Vector3 rotacionDestino = new Vector3(0, target.rotation.eulerAngles.y, 0);
                playerRig.transform.rotation = Quaternion.Euler(rotacionDestino);

                Debug.Log($"Viaje completado hacia: {target.name}");
            }
        }
    }
}