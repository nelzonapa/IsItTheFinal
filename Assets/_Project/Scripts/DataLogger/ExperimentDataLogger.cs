using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;
using Fusion;
using ImmersiveGraph.Network; // Para acceder a tus scripts NetworkPostItSync, etc.

namespace ImmersiveGraph.Data
{
    public class ExperimentDataLogger : MonoBehaviour
    {
        public static ExperimentDataLogger Instance;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            DontDestroyOnLoad(gameObject); // Para que sobreviva cambios de escena si los hubiera
        }

        // --- FUNCIÓN PRINCIPAL: EXPORTAR EL GRAFO ---
        public void ExportFinalGraphState()
        {
            Debug.Log("[LOGGER] Iniciando exportación del grafo...");

            GraphSnapshot snapshot = new GraphSnapshot();
            snapshot.sessionDate = DateTime.Now.ToString("yyyy-MM-dd");
            snapshot.timestamp = DateTime.Now.ToString("HH:mm:ss");

            // 1. RECOLECTAR POST-ITS
            // Buscamos todos los scripts de sincronización de PostIts activos
            var postIts = FindObjectsByType<NetworkPostItSync>(FindObjectsSortMode.None);
            foreach (var post in postIts)
            {
                // Obtenemos el NetworkObject asociado
                NetworkObject no = post.Object;

                GraphNodeData node = new GraphNodeData();
                node.networkID = no.Id.ToString();
                node.type = "PostIt";
                node.content = post.NetworkContent.ToString(); // El texto sincronizado
                node.ownerID = no.StateAuthority.PlayerId.ToString();
                node.position = post.transform.position;

                snapshot.nodes.Add(node);
            }

            // 2. RECOLECTAR TOKENS
            var tokens = FindObjectsByType<NetworkTokenSync>(FindObjectsSortMode.None);
            foreach (var tok in tokens)
            {
                NetworkObject no = tok.Object;

                GraphNodeData node = new GraphNodeData();
                node.networkID = no.Id.ToString();
                node.type = "Token";
                node.content = tok.TokenLabel.ToString(); // El texto del token
                node.ownerID = no.StateAuthority.PlayerId.ToString();
                node.position = tok.transform.position;

                snapshot.nodes.Add(node);
            }

            // 3. RECOLECTAR LÍNEAS (CONEXIONES)
            var lines = FindObjectsByType<NetworkConnectionLine>(FindObjectsSortMode.None);
            foreach (var line in lines)
            {
                NetworkObject no = line.Object;

                // Solo guardamos si la línea conecta algo real
                if (line.StartNodeID.IsValid && line.EndNodeID.IsValid)
                {
                    GraphEdgeData edge = new GraphEdgeData();
                    edge.networkID = no.Id.ToString();
                    edge.fromNodeID = line.StartNodeID.ToString();
                    edge.toNodeID = line.EndNodeID.ToString();
                    edge.ownerID = no.StateAuthority.PlayerId.ToString();

                    snapshot.connections.Add(edge);
                }
            }

            // 4. GUARDAR EN JSON
            SaveJsonToFile(snapshot);
        }

        private void SaveJsonToFile(GraphSnapshot data)
        {
            // Convertir a texto JSON (pretty print para que sea legible)
            string jsonString = JsonUtility.ToJson(data, true);

            // Definir nombre y ruta
            string filename = $"Graph_Result_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            string path = Path.Combine(Application.persistentDataPath, filename);

            try
            {
                File.WriteAllText(path, jsonString);
                Debug.Log($"[LOGGER] ¡ÉXITO! Grafo guardado en: {path}");

                // Feedback visual simple en consola (opcional)
                Debug.Log($"[LOGGER] Nodos: {data.nodes.Count} | Conexiones: {data.connections.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LOGGER] Error guardando JSON: {e.Message}");
            }
        }
    }
}