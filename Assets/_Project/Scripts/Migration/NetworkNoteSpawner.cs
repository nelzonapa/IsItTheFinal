using UnityEngine;
using Fusion;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(XRSimpleInteractable))] // Usamos Simple para que no se mueva, solo detecte click
    public class NetworkNoteSpawner : NetworkBehaviour
    {
        [Header("Configuración")]
        public NetworkObject networkPostItPrefab; // El prefab Network_PostIt

        private XRSimpleInteractable _interactable;

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
        }

        public override void Spawned()
        {
            // Escuchamos el evento de selección (Gatillo/Agarre)
            _interactable.selectEntered.AddListener(OnSpawnNote);
        }

        public void OnSpawnNote(SelectEnterEventArgs args)
        {
            // Solo spawneamos si tenemos conexión
            if (Runner != null && Runner.IsRunning)
            {
                // Posición: Un poco arriba del bloque
                Vector3 spawnPos = transform.position + Vector3.up * 0.1f;
                Quaternion spawnRot = transform.rotation;

                Debug.Log("Spawneando Nota de Red...");

                // Spawnear la nota
                // IMPORTANTE: Le damos autoridad al jugador que lo tocó (si pudiéramos saber quién es en Shared)
                // En Shared Mode, usamos Runner.LocalPlayer como propietario inicial al spawnear.
                Runner.Spawn(networkPostItPrefab, spawnPos, spawnRot, Runner.LocalPlayer);
            }
        }
    }
}