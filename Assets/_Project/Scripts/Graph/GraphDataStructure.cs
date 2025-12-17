using System.Collections.Generic;
using System;

namespace ImmersiveGraph.Data
{
    [Serializable]
    public class NodeData
    {
        public string id;       // Identificador único
        public string name;     // Nombre (Community X, File Y)
        public string type;     // "case" (root), "community", "file"
        public string content;  // Resumen o texto
        public string path;     // Ruta de la imagen (/content/drive...)

        // La estructura jerárquica: Hijos de este nodo
        public List<NodeData> children;
    }

    [Serializable]
    public class GraphContainer
    {
        // El JSON suele tener un objeto raíz o una lista. 
        // Asumiendo que hierarchy_complete.json empieza con un objeto raíz:
        public NodeData root;
    }
}