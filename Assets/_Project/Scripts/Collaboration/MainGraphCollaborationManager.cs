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
        [Header("Configuración del Marcador")]
        [Tooltip("El prefab creado en la Fase 1 (RemoteUserMarker_Prefab)")]
        public GameObject markerPrefab;

        [Tooltip("Distancia vertical a la que se sitúa el marcador sobre el nodo para no taparlo")]
        public float markerYOffset = 0.35f;

        // Diccionario para mantener controlados los marcadores de cada jugador remoto
        private Dictionary<int, CollabUserMarker> _playerMarkers = new Dictionary<int, CollabUserMarker>();

        void Update()
        {
            // 1. Seguridad: No hacer nada si el grafo aún no se ha generado
            if (H3GraphSpawner.Instance == null || H3GraphSpawner.Instance.allSpawnedNodes.Count == 0) return;

            // 2. Buscar todos los jugadores conectados en la sala
            var players = FindObjectsByType<HardwareRigSync>(FindObjectsSortMode.None);

            foreach (var syncData in players)
            {
                // Ignorar al jugador local (no necesitas ver un cubo encima de lo que tú mismo estás tocando)
                if (syncData == HardwareRigSync.Local) continue;

                int playerId = syncData.Object.StateAuthority.PlayerId;

                // 3. Crear el marcador para este jugador si es la primera vez que lo detectamos
                if (!_playerMarkers.ContainsKey(playerId))
                {
                    CreateMarkerForPlayer(playerId);
                }

                // 4. Actualizar la posición exacta del marcador en este frame
                UpdateMarkerPosition(playerId, syncData);
            }
        }

        private void CreateMarkerForPlayer(int playerId)
        {
            if (markerPrefab == null) return;

            // Instanciar el cubo como hijo de este Gestor para mantener la jerarquía limpia
            GameObject markerObj = Instantiate(markerPrefab, transform);
            markerObj.name = $"Marker_Player_{playerId}";

            CollabUserMarker markerScript = markerObj.GetComponent<CollabUserMarker>();
            if (markerScript != null)
            {
                // Extraer el color oficial del usuario remoto y pintar el cubo
                Color pColor = UserColorPalette.GetColor(playerId);
                markerScript.SetupMarker(pColor);
            }

            _playerMarkers.Add(playerId, markerScript);
        }

        private void UpdateMarkerPosition(int playerId, HardwareRigSync syncData)
        {
            if (!_playerMarkers.TryGetValue(playerId, out CollabUserMarker marker)) return;

            // Leer qué está mirando el compañero a través de la red
            string targetNodeId = syncData.SelectedNodeId.ToString();

            // Si el compañero no tiene nada seleccionado o está mirando al vacío, apagamos su cubo
            if (string.IsNullOrEmpty(targetNodeId))
            {
                marker.gameObject.SetActive(false);
                return;
            }

            // Buscar el nodo 3D equivalente en NUESTRA mesa local
            GraphNode targetNode = FindNodeInScene(targetNodeId);

            if (targetNode != null)
            {
                // --- LA MAGIA DE LA CONCIENCIA ASIMÉTRICA ---
                // Si nuestro compañero está en un archivo, pero nosotros tenemos esa comunidad cerrada,
                // GetHighestVisibleNode encontrará automáticamente la comunidad cerrada y pondrá el cubo ahí.
                GraphNode visibleNode = GetHighestVisibleNode(targetNode);

                if (visibleNode != null)
                {
                    marker.gameObject.SetActive(true);

                    // POSICIONAMIENTO ESTÁTICO Y DIRECTO (Cero animaciones)
                    marker.transform.position = visibleNode.transform.position + new Vector3(0, markerYOffset, 0);

                    // Mantenemos la rotación fija para que siempre se vea uniforme
                    marker.transform.rotation = Quaternion.identity;
                }
                else
                {
                    marker.gameObject.SetActive(false);
                }
            }
            else
            {
                marker.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Busca un nodo específico por su ID dentro de todos los nodos instanciados en la escena.
        /// </summary>
        private GraphNode FindNodeInScene(string nodeId)
        {
            foreach (GraphNode node in H3GraphSpawner.Instance.allSpawnedNodes)
            {
                if (node != null && node.myData != null && node.myData.id == nodeId)
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>
        /// Sube por la jerarquía del grafo hasta encontrar un nodo que esté visible actualmente.
        /// Soluciona el problema de los nodos colapsados.
        /// </summary>
        private GraphNode GetHighestVisibleNode(GraphNode startNode)
        {
            GraphNode currentNode = startNode;

            // Mientras el nodo actual esté "apagado" (colapsado por el usuario local)
            while (currentNode != null && !currentNode.gameObject.activeInHierarchy)
            {
                if (currentNode.parentNodeTransform != null)
                {
                    // Subimos un nivel hacia el padre (Ej. de Archivo -> Comunidad)
                    currentNode = currentNode.parentNodeTransform.GetComponent<GraphNode>();
                }
                else
                {
                    break; // Llegamos a la raíz o el árbol está roto
                }
            }

            return currentNode;
        }
    }
}