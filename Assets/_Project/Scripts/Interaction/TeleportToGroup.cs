using Fusion;
using ImmersiveGraph.Data;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class TeleportToGroup : MonoBehaviour
    {
        private XROrigin _xrOrigin;
        private NetworkRunner _runner;

        // REFERENCIA AL MIGRATION MANAGER
        public MigrationManager migrator;

        void Start()
        {
            _xrOrigin = FindFirstObjectByType<XROrigin>();
            var interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnButtonPressed);
        }

        public void OnButtonPressed(SelectEnterEventArgs args)
        {
            if (_runner == null) _runner = FindFirstObjectByType<NetworkRunner>();

            if (GroupTableManager.Instance == null || _runner == null)
            {
                Debug.LogError("Error: Falta Manager o Conexión.");
                return;
            }

            // --- PASO 1: EJECUTAR LA MIGRACIÓN ---
            if (migrator != null)
            {
                migrator.ExecuteMigration();
            }
            else
            {
                Debug.LogWarning("No hay MigrationManager asignado, viajando sin cosas...");
            }
            // -------------------------------------


            // --- METRICA 4: REGISTRO DE TRANSICIÓN ---
            if (ExperimentDataLogger.Instance != null)
            {
                ExperimentDataLogger.Instance.LogEvent(
                    "TRANSITION",
                    "Workspace Change",
                    "User Teleported to Group Table",
                    transform.position
                );
            }
            // -----------------------------------------

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