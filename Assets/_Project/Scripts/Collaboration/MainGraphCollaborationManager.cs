using ImmersiveGraph.Core;
using ImmersiveGraph.Interaction;
using ImmersiveGraph.Network;
using ImmersiveGraph.Visual;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveGraph.Collaboration
{
    public class MainGraphCollaborationManager : MonoBehaviour
    {
        [Header("Configuración de Marcadores")]
        [Tooltip("Arrastra aquí tu RemoteUserMarker_Prefab (El Cubo sin colliders)")]
        public GameObject remoteUserMarkerPrefab;

        [Tooltip("Qué tan alto sobre el nodo debe flotar el cubo")]
        public Vector3 markerOffset = new Vector3(0, 0.25f, 0);

        // Diccionario para mantener un solo cubo instanciado por cada jugador remoto
        private Dictionary<int, CollabUserMarker> _activeMarkers = new Dictionary<int, CollabUserMarker>();

        // Temporizador para no inundar la consola de Unity
        private float _debugTimer = 0f;

        void Update()
        {
            // Control de logs: true solo una vez por segundo
            bool shouldLog = false;
            _debugTimer += Time.deltaTime;
            if (_debugTimer >= 1.0f)
            {
                shouldLog = true;
                _debugTimer = 0f;
            }

            // 1. Verificación del Spawner
            if (H3GraphSpawner.Instance == null || H3GraphSpawner.Instance.spawnedNodesMap.Count == 0)
            {
                if (shouldLog) Debug.LogWarning("[CollabManager] Esperando... H3GraphSpawner no existe o el mapa de nodos está vacío.");
                return;
            }

            // 2. Verificación del Jugador Local
            if (HardwareRigSync.Local == null)
            {
                if (shouldLog) Debug.LogWarning("[CollabManager] Esperando... El jugador local (HardwareRigSync) aún no se ha asignado.");
                return;
            }

            int localPlayerId = HardwareRigSync.Local.Object.StateAuthority.PlayerId;

            // 3. Verificación de la sala
            if (shouldLog && SharedWorkspaceTracker.RegisteredAvatars.Count <= 1)
            {
                Debug.Log($"[CollabManager] Sala solitaria. Solo hay {SharedWorkspaceTracker.RegisteredAvatars.Count} avatar(es) registrados. Esperando a un compañero...");
            }

            // Iteramos sobre la lista de avatares en red
            foreach (var avatar in SharedWorkspaceTracker.RegisteredAvatars)
            {
                if (avatar == null || avatar.Object == null || !avatar.Object.IsValid) continue;

                int playerId = avatar.Object.StateAuthority.PlayerId;

                // Ignoramos nuestro propio avatar
                if (playerId == localPlayerId) continue;

                string targetNodeId = avatar.SelectedNodeId.ToString();

                // 4. Verificación de Selección
                if (string.IsNullOrEmpty(targetNodeId))
                {
                    if (shouldLog) Debug.Log($"[CollabManager] El compañero (ID: {playerId}) no está agarrando/seleccionando ningún nodo.");
                    if (_activeMarkers.ContainsKey(playerId)) _activeMarkers[playerId].gameObject.SetActive(false);
                    continue;
                }

                // 5. Verificación de Coincidencia en el Diccionario
                if (H3GraphSpawner.Instance.spawnedNodesMap.TryGetValue(targetNodeId, out GraphNode targetNode))
                {
                    // ¡ÉXITO! Se encontró el nodo
                    if (!_activeMarkers.ContainsKey(playerId))
                    {
                        Debug.Log($"[CollabManager] ¡EXITO! Instanciando nuevo CUBO para el compañero {playerId} en el nodo '{targetNodeId}'.");
                        GameObject newMarkerObj = Instantiate(remoteUserMarkerPrefab, transform);
                        CollabUserMarker markerLogic = newMarkerObj.GetComponent<CollabUserMarker>();
                        markerLogic.SetupMarker(UserColorPalette.GetColor(playerId));
                        _activeMarkers[playerId] = markerLogic;
                    }

                    CollabUserMarker marker = _activeMarkers[playerId];

                    if (!marker.gameObject.activeSelf)
                    {
                        Debug.Log($"[CollabManager] Reactivando cubo oculto para el compañero {playerId}.");
                        marker.gameObject.SetActive(true);
                    }

                    // Lógica UX de nodos colapsados
                    GraphNode visualTarget = targetNode;
                    if (!visualTarget.gameObject.activeInHierarchy && visualTarget.parentNodeTransform != null)
                    {
                        GraphNode parentNode = visualTarget.parentNodeTransform.GetComponent<GraphNode>();
                        if (parentNode != null)
                        {
                            visualTarget = parentNode;
                            if (shouldLog) Debug.Log($"[CollabManager] El nodo '{targetNodeId}' está colapsado. Moviendo el cubo al padre '{parentNode.myData.id}'.");
                        }
                    }

                    // Posicionamiento
                    marker.transform.position = visualTarget.transform.position + markerOffset;
                    marker.transform.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);

                    if (shouldLog) Debug.Log($"[CollabManager] El cubo del ID {playerId} está flotando sobre el nodo visual: {visualTarget.name}");
                }
                else
                {
                    // ERROR CRÍTICO: El ID viajó por la red, pero no existe en tu mapa local
                    if (shouldLog) Debug.LogError($"[CollabManager] ERROR DE MAPA: El compañero {playerId} seleccionó el nodo '{targetNodeId}', pero ese ID NO existe en tu spawnedNodesMap.");
                }
            }
        }
    }
}