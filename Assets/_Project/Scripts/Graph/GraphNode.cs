using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;
using ImmersiveGraph.Data;

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    [RequireComponent(typeof(SphereCollider))]
    public class GraphNode : MonoBehaviour
    {
        [Header("Datos del Nodo")]
        public string nodeType;
        public NodeData myData;

        [Header("Relaciones")]
        public List<GameObject> childNodes = new List<GameObject>();
        public List<GameObject> connectionLines = new List<GameObject>();

        private bool _isExpanded = false;
        private XRSimpleInteractable _interactable;

        void Awake() // Usamos Awake para referencias internas
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            // Aseguramos que el collider sea Trigger para interacción simple
            GetComponent<SphereCollider>().isTrigger = true;
        }

        void OnEnable()
        {
            if (_interactable != null)
                _interactable.selectEntered.AddListener(OnNodeSelected);
        }

        void OnDisable()
        {
            if (_interactable != null)
                _interactable.selectEntered.RemoveListener(OnNodeSelected);
        }

        // --- NUEVO: Se llama desde el Spawner cuando todo está listo ---
        public void InitializeNode()
        {
            if (nodeType == "community")
            {
                // Iniciar colapsado
                _isExpanded = false;
                SetChildrenVisibility(false);
            }
        }

        void OnNodeSelected(SelectEnterEventArgs args)
        {
            Debug.Log($"Click en nodo: {name} tipo {nodeType}");

            if (nodeType == "community")
            {
                _isExpanded = !_isExpanded;
                SetChildrenVisibility(_isExpanded);
            }
            else if (nodeType == "file")
            {
                Debug.Log($"CLICK EN ARCHIVO: {myData.title}. Enviando a Lector...");
            }
        }

        void SetChildrenVisibility(bool state)
        {
            foreach (var child in childNodes)
            {
                if (child != null) child.SetActive(state);
            }
            // Las líneas también se ocultan
            foreach (var line in connectionLines)
            {
                if (line != null) line.SetActive(state);
            }
        }
    }
}