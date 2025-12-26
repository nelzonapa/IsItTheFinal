using Fusion;
using ImmersiveGraph.Data;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XR Toolkit nuevo

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    [RequireComponent(typeof(AudioSource))] // <--- Nuevo: Necesita AudioSource
    public class TeleportToGroup : MonoBehaviour
    {
        private XROrigin _xrOrigin;
        private NetworkRunner _runner;
        private AudioSource _audioSource; // Referencia al audio

        [Header("Configuración")]
        public MigrationManager migrator;
        public AudioClip teleportSound; // <--- Arrastra el sonido aquí

        void Start()
        {
            _xrOrigin = FindFirstObjectByType<XROrigin>();
            _audioSource = GetComponent<AudioSource>(); // Inicializar

            var interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnButtonPressed);
        }

        public void OnButtonPressed(SelectEnterEventArgs args)
        {
            // 1. REPRODUCIR SONIDO
            if (_audioSource != null && teleportSound != null)
            {
                // PlayOneShot permite que el sonido suene aunque nos movamos rápido (si el AudioListener viaja con nosotros)
                _audioSource.PlayOneShot(teleportSound);
            }

            if (_runner == null) _runner = FindFirstObjectByType<NetworkRunner>();

            if (GroupTableManager.Instance == null || _runner == null)
            {
                Debug.LogError("Error: Falta Manager o Conexión.");
                return;
            }

            // 2. MIGRACIÓN
            if (migrator != null)
            {
                migrator.ExecuteMigration();
            }
            else
            {
                Debug.LogWarning("No hay MigrationManager asignado, viajando sin cosas...");
            }

            // 3. LOGS
            if (ExperimentDataLogger.Instance != null)
            {
                ExperimentDataLogger.Instance.LogEvent(
                    "TRANSITION",
                    "Workspace Change",
                    "User Teleported to Group Table",
                    transform.position
                );
            }

            // 4. TELETRANSPORTE FÍSICO
            Debug.Log("Teletransportando al punto de spawn asignado...");
            Transform targetSpawn = GroupTableManager.Instance.GetSpawnPointForPlayer(_runner.LocalPlayer);

            if (_xrOrigin != null && targetSpawn != null)
            {
                _xrOrigin.transform.position = targetSpawn.position + new Vector3(0, 0.05f, 0);
                Vector3 targetRotation = targetSpawn.rotation.eulerAngles;
                _xrOrigin.transform.rotation = Quaternion.Euler(0, targetRotation.y, 0);
            }
        }
    }
}