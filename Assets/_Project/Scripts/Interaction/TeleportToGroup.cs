using Fusion;
using ImmersiveGraph.Data;
using ImmersiveGraph.Core; // <--- NECESARIO
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    [RequireComponent(typeof(AudioSource))]
    public class TeleportToGroup : MonoBehaviour
    {
        private NetworkRunner _runner;
        private AudioSource _audioSource;

        [Header("Configuración")]
        public MigrationManager migrator;
        public AudioClip teleportSound;

        void Start()
        {
            _audioSource = GetComponent<AudioSource>();
            var interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnButtonPressed);
        }

        public void OnButtonPressed(SelectEnterEventArgs args)
        {
            // 1. SONIDO
            if (_audioSource != null && teleportSound != null) _audioSource.PlayOneShot(teleportSound);

            if (_runner == null) _runner = FindFirstObjectByType<NetworkRunner>();

            if (GroupTableManager.Instance == null || _runner == null)
            {
                Debug.LogError("Error: Falta Manager o Conexión.");
                return;
            }

            // 2. MIGRACIÓN
            if (migrator != null) migrator.ExecuteMigration();
            else Debug.LogWarning("Viajando sin migración...");

            // 3. LOGS
            if (ExperimentDataLogger.Instance != null)
            {
                ExperimentDataLogger.Instance.LogEvent("TRANSITION", "Workspace Change", "User Teleported", transform.position);
            }

            Debug.Log("Teletransportando...");

            // 4. MOVER AL JUGADOR (PC O VR) 
            GameObject playerToMove = null;

            if (PlatformManager.Instance != null && PlatformManager.Instance.ActiveRig != null)
            {
                playerToMove = PlatformManager.Instance.ActiveRig;
            }
            else
            {
                // Fallback de seguridad
                var legacyOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (legacyOrigin != null) playerToMove = legacyOrigin.gameObject;
            }

            // Obtener punto de spawn GRUPAL
            Transform targetSpawn = GroupTableManager.Instance.GetSpawnPointForPlayer(_runner.LocalPlayer);

            if (playerToMove != null && targetSpawn != null)
            {
                // Mover
                playerToMove.transform.position = targetSpawn.position + new Vector3(0, 0.05f, 0);

                // Rotar
                Vector3 targetRotation = targetSpawn.rotation.eulerAngles;
                playerToMove.transform.rotation = Quaternion.Euler(0, targetRotation.y, 0);

                // Sincronizar Físicas
                Physics.SyncTransforms();
            }
            else
            {
                Debug.LogError("No se pudo teletransportar: Falta PlayerRig o SpawnPoint.");
            }
        }
    }
}