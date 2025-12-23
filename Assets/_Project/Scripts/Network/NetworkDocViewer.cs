using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.UI;
using ImmersiveGraph.Data;
using ImmersiveGraph.Visual;
using System.Collections;

namespace ImmersiveGraph.Network
{
    public class NetworkDocViewer : NetworkBehaviour
    {
        [Header("Referencias UI")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI contentText;
        public Button closeButton;
        public TextMeshProUGUI riskText;
        public Image backgroundPanel; // Arrastra el fondo del panel aquí para pintarlo de rojo

        [Header("Configuración Spawning")]
        public NetworkObject netTokenPrefab;

        [Networked] public NetworkString<_64> TargetNodeID { get; set; }

        // --- NUEVO: ID del Token que me creó (Padre Lógico) ---
        [Networked] public NetworkId ParentTokenId { get; set; }
        // -----------------------------------------------------

        private NetworkString<_64> _cachedTargetID;
        private Transform _targetToFollow;
        private Vector3 _followOffset = new Vector3(0, 0.4f, 0); // Altura

        public override void Spawned()
        {
            _cachedTargetID = TargetNodeID;
            if (!string.IsNullOrEmpty(TargetNodeID.ToString())) LoadLocalData(TargetNodeID.ToString());
            if (closeButton != null) closeButton.onClick.AddListener(OnClosePressed);
        }

        // --- LÓGICA DE SEGUIMIENTO ---
        public override void FixedUpdateNetwork()
        {
            // Solo la autoridad (quien creó el panel) calcula el movimiento y lo sincroniza
            if (Object.HasStateAuthority)
            {
                // Si aún no tenemos target físico, lo buscamos por ID
                if (_targetToFollow == null && ParentTokenId.IsValid)
                {
                    NetworkObject tokenObj = Runner.FindObject(ParentTokenId);
                    if (tokenObj != null) _targetToFollow = tokenObj.transform;
                }

                // Si tenemos target, lo seguimos
                if (_targetToFollow != null)
                {
                    // Lerp suave para que no vibre
                    Vector3 targetPos = _targetToFollow.position + _followOffset;
                    transform.position = Vector3.Lerp(transform.position, targetPos, Runner.DeltaTime * 10f);

                    // Opcional: Que siempre mire al usuario o mantenga rotación cero
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(transform.position - Camera.main.transform.position), Runner.DeltaTime * 5f);
                }
            }
        }

        // --- LÓGICA DE PARPADEO (RPC) ---
        [Rpc(RpcSources.All, RpcTargets.All)]
        public void Rpc_FlashError()
        {
            StartCoroutine(FlashRedRoutine());
        }

        IEnumerator FlashRedRoutine()
        {
            if (backgroundPanel != null)
            {
                Color original = backgroundPanel.color;
                backgroundPanel.color = Color.red;
                yield return new WaitForSeconds(0.2f);
                backgroundPanel.color = original;
                yield return new WaitForSeconds(0.2f);
                backgroundPanel.color = Color.red;
                yield return new WaitForSeconds(0.2f);
                backgroundPanel.color = original;
            }
        }
        // -------------------------------

        public override void Render()
        {
            if (TargetNodeID != _cachedTargetID)
            {
                _cachedTargetID = TargetNodeID;
                LoadLocalData(TargetNodeID.ToString());
            }
        }

        // ... (Mantén tu función LoadLocalData IGUAL que antes) ...
        void LoadLocalData(string id)
        {
            // ... CÓDIGO EXISTENTE ...
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
                    var selectable = contentText.GetComponent<Interaction.SelectableText>();
                    if (selectable != null)
                    {
                        selectable.UpdateOriginalText();
                        selectable.currentContextNodeID = id;
                        selectable.isNetworkMode = true;
                        selectable.netTokenPrefab = netTokenPrefab;
                    }
                }
            }
            else
            {
                if (titleText != null) titleText.text = "Error: Nodo no encontrado";
            }
        }

        public void InitializePanel(string nodeID, NetworkId parentTokenId)
        {
            TargetNodeID = nodeID;
            ParentTokenId = parentTokenId; // Guardamos quién es nuestro "padre"
        }

        void OnClosePressed()
        {
            if (Object.HasStateAuthority)
            {
                // Antes de morir, avisamos al Token que ya no existimos (opcional, pero el token lo detectará solo)
                Runner.Despawn(Object);
            }
        }
    }
}