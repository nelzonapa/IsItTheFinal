using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveGraph.Data
{
    [Serializable]
    public class GraphSnapshot
    {
        public string sessionDate;
        public string timestamp;
        public List<GraphNodeData> nodes = new List<GraphNodeData>();
        public List<GraphEdgeData> connections = new List<GraphEdgeData>();
    }

    [Serializable]
    public class GraphNodeData
    {
        public string networkID;      // ID único de Fusion (para referencia)
        public string type;           // "PostIt" o "Token"
        public string content;        // Lo que dice el texto
        public string ownerID;        // Quién lo creó (PlayerId)
        public Vector3 position;      // Dónde quedó en la mesa
    }

    [Serializable]
    public class GraphEdgeData
    {
        public string networkID;      // ID de la línea
        public string fromNodeID;     // ID del nodo origen
        public string toNodeID;       // ID del nodo destino
        public string ownerID;        // Quién tiró la línea
    }
}