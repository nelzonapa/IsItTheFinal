using System;
using System.Collections.Generic;

namespace ImmersiveGraph.Data
{
    // 1. Clase Unificada para "details"
    // Como JsonUtility no soporta polimorfismo bien, metemos todos los campos posibles aquí.
    // Unity rellenará los que encuentre y dejará null los que no.
    [Serializable]
    public class GenericDetails
    {
        // Campos de ROOT
        public string[] focos;
        public string conclusion;

        // Campos de COMMUNITY
        public string[] entidades;
        public string fechas;    // <--- CORREGIDO: En tu JSON es un String, no un Array
        public string amenaza;
    }

    [Serializable]
    public class FileDataContent
    {
        public string full_text;
        public string[] images;
        public string source;    // Agregado por si acaso
        public string date;      // Agregado por si acaso
    }

    [Serializable]
    public class NodeData
    {
        public string id;       // Agregado (veo que en tu JSON hay ID)
        public string type;
        public string title;
        public string summary;
        public string risk_level;

        // --- CORRECCIÓN CLAVE ---
        // La variable SE TIENE QUE LLAMAR "details" para que coincida con el JSON.
        public GenericDetails details;

        public FileDataContent data;

        public List<NodeData> children;
    }
}