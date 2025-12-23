using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.UI;
using ImmersiveGraph.Data;
using ImmersiveGraph.Visual;

namespace ImmersiveGraph.Network
{
    public class NetworkDocViewer : NetworkBehaviour
    {
        [Header("Referencias UI")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI contentText;
        public Button closeButton;
        public TextMeshProUGUI riskText;

        // --- NUEVO FASE 3: REFERENCIA AL PREFAB DE RED ---
        [Header("Configuración Spawning")]
        public NetworkObject netTokenPrefab; // Arrastra aquí el Network_Token
        // -------------------------------------------------

        [Networked]
        public NetworkString<_64> TargetNodeID { get; set; }

        private NetworkString<_64> _cachedTargetID;

        public override void Spawned()
        {
            _cachedTargetID = TargetNodeID;
            if (!string.IsNullOrEmpty(TargetNodeID.ToString())) LoadLocalData(TargetNodeID.ToString());
            if (closeButton != null) closeButton.onClick.AddListener(OnClosePressed);
        }

        public override void Render()
        {
            if (TargetNodeID != _cachedTargetID)
            {
                _cachedTargetID = TargetNodeID;
                LoadLocalData(TargetNodeID.ToString());
            }
        }

        void LoadLocalData(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (H3GraphSpawner.Instance == null) return;

            NodeData data = H3GraphSpawner.Instance.GetNodeDataByID(id);

            if (data != null)
            {
                if (titleText != null) titleText.text = data.title;
                if (riskText != null) riskText.text = "Riesgo: " + (data.risk_level ?? "-");

                if (contentText != null)
                {
                    if (data.data != null) contentText.text = data.data.full_text;
                    else contentText.text = "(Sin contenido de texto)";

                    // --- CONFIGURACIÓN FASE 3 ---
                    var selectable = contentText.GetComponent<Interaction.SelectableText>();
                    if (selectable != null)
                    {
                        selectable.UpdateOriginalText();

                        // 1. Inyectar ID del documento
                        selectable.currentContextNodeID = id;

                        // 2. Activar Modo Red
                        selectable.isNetworkMode = true;

                        // 3. Pasar el Prefab
                        selectable.netTokenPrefab = netTokenPrefab;
                    }
                    // ----------------------------
                }
            }
            else
            {
                if (titleText != null) titleText.text = "Error: Nodo no encontrado";
            }
        }

        public void InitializePanel(string nodeID)
        {
            TargetNodeID = nodeID;
        }

        void OnClosePressed()
        {
            if (Object.HasStateAuthority) Runner.Despawn(Object);
        }
    }
}