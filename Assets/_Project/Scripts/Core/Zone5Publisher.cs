using UnityEngine;
using Fusion;
using ImmersiveGraph.Network; // Para ver el GroupTableManager
using System.Collections.Generic;

namespace ImmersiveGraph.Interaction
{
    // Este script va en el objeto Zone5_Shared junto con DeskZone.cs
    public class Zone5Publisher : NetworkBehaviour
    {
        [Header("Configuración")]
        [Tooltip("El prefab que tiene NetworkObject (Networked_PostIt)")]
        public NetworkObject networkObjectPrefab;

        // Control de rebote para no clonar 50 veces lo mismo
        private HashSet<GameObject> processedObjects = new HashSet<GameObject>();

        private void OnTriggerEnter(Collider other)
        {
            // 1. Filtro: ¿El objeto tiene el tag correcto?
            if (other.CompareTag("ClonableItem"))
            {
                // 2. Filtro: ¿Ya lo procesamos?
                if (!processedObjects.Contains(other.gameObject))
                {
                    // 3. Filtro: ¿Estamos conectados a internet?
                    if (Runner != null && Runner.IsRunning)
                    {
                        CloneToGroupTable(other.gameObject);
                    }
                    else
                    {
                        Debug.LogWarning("Intento de clonar, pero no estás conectado a Fusion.");
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Si sacas el objeto y lo vuelves a meter, permitimos clonarlo de nuevo
            if (processedObjects.Contains(other.gameObject))
            {
                processedObjects.Remove(other.gameObject);
            }
        }

        void CloneToGroupTable(GameObject localObject)
        {
            Debug.Log($"¡Clonando {localObject.name} a la mesa grupal!");
            processedObjects.Add(localObject);

            // A. Buscar dónde ponerlo (usando el Manager que creamos antes)
            Transform targetZone = null;

            if (GroupTableManager.Instance != null)
            {
                // Pide la zona asignada a MI jugador
                targetZone = GroupTableManager.Instance.GetReceptionZoneForPlayer(Runner.LocalPlayer);
            }

            // Si no hay manager (error), usamos el centro del mundo por seguridad
            Vector3 spawnPos = (targetZone != null) ? targetZone.position : Vector3.zero;

            // Añadir variación para que no caigan todos en el mismo punto exacto
            spawnPos += new Vector3(Random.Range(-0.1f, 0.1f), 0.1f, Random.Range(-0.1f, 0.1f));

            // B. SPAWN: Crear la copia en red
            // Usamos el prefab asignado en el inspector
            Runner.Spawn(networkObjectPrefab, spawnPos, Quaternion.identity, Runner.LocalPlayer);
        }
    }
}