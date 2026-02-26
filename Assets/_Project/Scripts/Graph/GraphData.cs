using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveGraph.Data
{
    [Serializable]
    public class GenericDetails
    {
        // --- ROOT ---
        public string[] focos;
        public string conclusion;

        // --- COMMUNITY ---
        // CAMBIO CRÍTICO: El JSON dice "entidades_frecuentes", no "entidades"
        public string[] entidades_frecuentes;
        public string fechas;
        public string amenaza;
    }

    [Serializable]
    public class FileDataContent
    {
        // CAMBIO CRÍTICO: El JSON dice "texto_full", no "full_text"
        public string texto_full;

        public string[] images;
        public string source;
        public string date;
    }

    [Serializable]
    public class NodeData
    {
        public string id;
        public string type;
        public string title;
        public string summary;

        // --- NUEVO: Faltaba en tu estructura anterior ---
        // "keywords" aparece en Community al nivel del nodo
        public string keywords; // El JSON dice que es string, no array.

        // "entidades" aparece en File al nivel del nodo (fuera de data)
        public string[] entidades;
        // -----------------------------------------------

        public string risk_level;

        public GenericDetails details; // Para Root y Community
        public FileDataContent data;   // Para File

        public List<NodeData> children;
    }
}