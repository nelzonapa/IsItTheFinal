using UnityEngine;
using Fusion;
using ImmersiveGraph.Network;
using System.Collections.Generic;
using System.Linq; // Necesario para buscar

namespace ImmersiveGraph.Interaction
{
    public class Zone5Publisher : MonoBehaviour // Ya no necesita ser NetworkBehaviour obligatoriamente
    {
        [Header("Configuración")]
        public NetworkObject networkObjectPrefab;

        private HashSet<GameObject> processedObjects = new HashSet<GameObject>();

        // Variable para guardar al jefe de la conexión
        private NetworkRunner _runner;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("ClonableItem"))
            {
                if (!processedObjects.Contains(other.gameObject))
                {
                    // BUSCAR AL RUNNER SI NO LO TENEMOS
                    if (_runner == null) _runner = FindFirstObjectByType<NetworkRunner>();

                    // Ahora verificamos la variable local _runner
                    if (_runner != null && _runner.IsRunning)
                    {
                        CloneToGroupTable(other.gameObject);
                    }
                    else
                    {
                        // Si después de buscar sigue siendo null, entonces sí hay problema
                        Debug.LogWarning("Intento de clonar, pero no encuentro el NetworkRunner activo.");
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (processedObjects.Contains(other.gameObject))
            {
                processedObjects.Remove(other.gameObject);
            }
        }

        void CloneToGroupTable(GameObject localObject)
        {
            Debug.Log($"¡Clonando {localObject.name} a la mesa grupal!");
            processedObjects.Add(localObject);

            Transform targetZone = null;

            if (GroupTableManager.Instance != null)
            {
                // Usamos _runner en lugar de Runner
                targetZone = GroupTableManager.Instance.GetReceptionZoneForPlayer(_runner.LocalPlayer);
            }

            Vector3 spawnPos = (targetZone != null) ? targetZone.position : Vector3.zero;
            spawnPos += new Vector3(Random.Range(-0.1f, 0.1f), 0.1f, Random.Range(-0.1f, 0.1f));

            // SPAWN usando la referencia encontrada
            _runner.Spawn(networkObjectPrefab, spawnPos, Quaternion.identity, _runner.LocalPlayer);
        }
    }
}