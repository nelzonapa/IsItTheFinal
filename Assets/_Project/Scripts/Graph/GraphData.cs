using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveGraph.Data
{
    [Serializable]
    public class GenericDetails
    {
        public string[] focos;
        public string conclusion;
        public string[] entidades_frecuentes;
        public string fechas;
        public string amenaza;
    }

    [Serializable]
    public class FileDataContent
    {
        public string texto_full;
        public string source;
        public string date;
        // SE ELIMINÓ EL ARRAY DE IMÁGENES
    }

    // --- NUEVO: ESTRUCTURA PARA EL GRAFO DE CONOCIMIENTO (NIVEL ARCHIVO) ---
    [Serializable]
    public class KGEdge
    {
        public string sujeto;
        public string relacion;
        public string objeto;
    }

    // --- NUEVO: ESTRUCTURA PARA EL ÍNDICE GLOBAL DE ENTIDADES ---
    [Serializable]
    public class GlobalEntityData
    {
        public string nombre_original;
        public string[] nodos; // Arreglo de IDs (Ej: "doc_045", "COMUNIDAD_3")
    }

    [Serializable]
    public class NodeData
    {
        public string id;
        public string type;
        public string title;
        public string summary;
        public string keywords;
        public string[] entidades;
        public string risk_level;

        public GenericDetails details;
        public FileDataContent data;

        // --- NUEVO: EL ARRAY DEL GRAFO DE CONOCIMIENTO ---
        public KGEdge[] knowledge_graph;

        public List<NodeData> children;
    }
}