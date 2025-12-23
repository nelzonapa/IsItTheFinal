using UnityEngine;
using System.IO;
using System.Text;
using System;
using System.Collections.Generic;
using Fusion;
using ImmersiveGraph.Network;

namespace ImmersiveGraph.Data
{
    public class ExperimentDataLogger : MonoBehaviour
    {
        public static ExperimentDataLogger Instance;

        // Variables para el CSV (Eventos)
        private StringBuilder _csvContent = new StringBuilder();
        private string _csvFilePath;
        private float _startTime;
        private bool _isLogging = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            DontDestroyOnLoad(gameObject);

            // Iniciar cronómetro y archivo
            InitializeLogger();
        }

        void InitializeLogger()
        {
            _startTime = Time.time;
            _isLogging = true;

            // Nombre del archivo CSV
            string filename = $"Events_Log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            _csvFilePath = Path.Combine(Application.persistentDataPath, filename);

            // Cabecera del Excel (Columnas)
            _csvContent.AppendLine("Timestamp(s),Event_Type,Details,Position_X,Position_Y,Position_Z");

            // Log de inicio
            LogEvent("SYSTEM", "SESSION_START", "Inicio del Experimento", Vector3.zero);
            Debug.Log($"[LOGGER] Guardando eventos en: {_csvFilePath}");
        }

        // --- FUNCIÓN PÚBLICA PARA REGISTRAR EVENTOS (Métricas 2, 3 y 4) ---
        public void LogEvent(string eventType, string category, string details, Vector3 pos)
        {
            if (!_isLogging) return;

            float currentTime = Time.time - _startTime; // Tiempo exacto desde el inicio

            // Limpiamos comas en los detalles para no romper el CSV
            string cleanDetails = details.Replace(",", ";");

            // Formato: Tiempo, Tipo, Categoria/Detalle, PosX, PosY, PosZ
            string newLine = $"{currentTime:F2},{eventType},{category}: {cleanDetails},{pos.x:F2},{pos.y:F2},{pos.z:F2}";

            _csvContent.AppendLine(newLine);
        }

        // --- GUARDADO FÍSICO (Se llama al cerrar o finalizar) ---
        public void SaveLogsToDisk()
        {
            if (!_isLogging) return;

            try
            {
                // 1. Guardar CSV de Eventos
                File.WriteAllText(_csvFilePath, _csvContent.ToString());
                Debug.Log("[LOGGER] CSV de eventos guardado exitosamente.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LOGGER] Error guardando CSV: {e.Message}");
            }
        }

        private void OnApplicationQuit()
        {
            LogEvent("SYSTEM", "SESSION_END", "Cierre inesperado o fin de app", Vector3.zero);
            SaveLogsToDisk();
        }

        // --- EXPORTAR GRAFO JSON (Métrica 1 - F1 Score) ---
        // (Este código es el mismo que hicimos antes, lo mantengo aquí integrado)
        public void ExportFinalGraphState()
        {
            LogEvent("SYSTEM", "DATA_EXPORT", "Generando JSON final...", Vector3.zero);

            GraphSnapshot snapshot = new GraphSnapshot();
            snapshot.sessionDate = DateTime.Now.ToString("yyyy-MM-dd");
            snapshot.timestamp = DateTime.Now.ToString("HH:mm:ss");

            // 1. Post-Its
            var postIts = FindObjectsByType<NetworkPostItSync>(FindObjectsSortMode.None);
            foreach (var post in postIts)
            {
                GraphNodeData node = new GraphNodeData();
                node.networkID = post.Object.Id.ToString();
                node.type = "PostIt";
                node.content = post.NetworkContent.ToString();
                node.ownerID = post.Object.StateAuthority.PlayerId.ToString();
                node.position = post.transform.position;
                snapshot.nodes.Add(node);
            }

            // 2. Tokens
            var tokens = FindObjectsByType<NetworkTokenSync>(FindObjectsSortMode.None);
            foreach (var tok in tokens)
            {
                GraphNodeData node = new GraphNodeData();
                node.networkID = tok.Object.Id.ToString();
                node.type = "Token";
                node.content = tok.TokenLabel.ToString();
                node.ownerID = tok.Object.StateAuthority.PlayerId.ToString();
                node.position = tok.transform.position;
                snapshot.nodes.Add(node);
            }

            // 3. Líneas
            var lines = FindObjectsByType<NetworkConnectionLine>(FindObjectsSortMode.None);
            foreach (var line in lines)
            {
                if (line.StartNodeID.IsValid && line.EndNodeID.IsValid)
                {
                    GraphEdgeData edge = new GraphEdgeData();
                    edge.networkID = line.Object.Id.ToString();
                    edge.fromNodeID = line.StartNodeID.ToString();
                    edge.toNodeID = line.EndNodeID.ToString();
                    edge.ownerID = line.Object.StateAuthority.PlayerId.ToString();
                    snapshot.connections.Add(edge);
                }
            }

            // Guardar JSON
            string jsonString = JsonUtility.ToJson(snapshot, true);
            string filename = $"Graph_Result_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            string path = Path.Combine(Application.persistentDataPath, filename);
            File.WriteAllText(path, jsonString);

            LogEvent("SYSTEM", "DATA_EXPORT", "JSON Guardado Exitosamente", Vector3.zero);
        }
    }
}