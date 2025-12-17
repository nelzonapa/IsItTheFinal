using System;
using System.Collections.Generic;

namespace ImmersiveGraph.Data
{
    // Clases auxiliares para los detalles anidados
    [Serializable]
    public class RootDetails
    {
        public string[] focos;
        public string conclusion;
    }

    [Serializable]
    public class CommunityDetails
    {
        public string[] entidades;
        public string[] fechas;
        public string amenaza;
    }

    [Serializable]
    public class FileDataContent
    {
        public string full_text;
        public string[] images; // Rutas a las imágenes
    }

    // La Clase Nodo Maestra (Sirve para Root, Community y File)
    [Serializable]
    public class NodeData
    {
        public string type;       // "root", "community", "file"
        public string title;
        public string summary;

        // Propiedades específicas de FILE
        public string risk_level; // "Alto", "Medio", "Bajo"
        public FileDataContent data;

        // Propiedades específicas de ROOT y COMMUNITY
        // Como 'details' cambia de estructura según el tipo, 
        // usaremos un truco: JsonUtility a veces falla con polimorfismo.
        // Para asegurar que funcione, definimos ambas clases contenedoras.
        public RootDetails root_details;       // (En el JSON vendrá como "details" si es root)
        public CommunityDetails comm_details;  // (En el JSON vendrá como "details" si es community)

        // NOTA TÉCNICA: Si el JSON usa la misma clave "details" para objetos diferentes,
        // JsonUtility nativo de Unity podría dar problemas. 
        // Si eso pasa, usaremos una clase "GenericDetails" o cambiaremos a Newtonsoft.json.
        // Por ahora, probemos la estructura estándar asumiendo mapeo directo.

        public List<NodeData> children; // Recursividad
    }
}