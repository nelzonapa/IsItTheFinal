using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveGraph.Data
{
    // --- MAPEO DE unity_graph_data.json ---
    [Serializable]
    public class GraphDataContainer
    {
        public List<NodeData> nodes;
    }

    [Serializable]
    public class NodeData
    {
        public string id;
        public string label;
        public string type;
        public Vector3Data position;
        public string parent_id;

        public string relation_label; // la relación

        //El backend usa "meta" y campos directos como "description"
        public string description;
        public NodeMetaData meta;
    }

    [Serializable]
    public class Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [Serializable]
    public class NodeMetaData
    {
        // Campos flexibles que pueden venir o no
        public string date;
        public string file_size;
        public string url;
        public int doc_count; // Visto en tu snippet
    }

    // --- MAPEO DE unity_app_config.json ---
    [Serializable]
    public class AppConfigContainer
    {
        // CORRECCIÓN: Ya no es una lista, es un Objeto mapeado
        public VisualSettingsMap visual_settings;
        public InteractionSettings interaction_settings;
    }

    // Clase auxiliar para leer el Diccionario del JSON
    [Serializable]
    public class VisualSettingsMap
    {
        public VisualSetting ROOT;
        public VisualSetting MACRO_TOPIC;
        public VisualSetting MICRO_TOPIC;
        public VisualSetting DOCUMENT;
        public VisualSetting ENTITY;
    }

    [Serializable]
    public class VisualSetting
    {
        // Nota: En este formato del backend, el "type" es la llave, así que aquí solo importan las props
        public string color;
        public string shape;
        public float scale;
        public float emission;
        public bool interactable;
    }

    [Serializable]
    public class InteractionSettings
    {
        public float selection_distance;
        public float connection_line_width;
        public float hover_scale_multiplier;
    }

    // --- MAPEO DE unity_collab_rules.json ---
    [Serializable]
    public class CollabRulesContainer
    {
        // El backend a veces envia estructuras complejas, 
        // por ahora lo dejamos genérico para que no falle.
    }
}