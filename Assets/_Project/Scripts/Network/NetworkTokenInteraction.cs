using Fusion;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(XRGrabInteractable))]

    [RequireComponent(typeof(AudioSource))] // <--- Nuevo requisito
    public class NetworkTokenInteraction : NetworkBehaviour
    {
        [Header("Configuración")]
        public float holdDuration = 4.0f;
        // El offset ahora lo maneja el panel, pero lo dejamos aquí por si acaso

        [Header("Referencias")]
        public Image loadingBarImage;
        public Canvas loaderCanvas;
        public NetworkObject docPanelPrefab;
        public AudioClip openPanelSound; // mi sonido

        private NetworkTokenSync _tokenSync;
        private XRGrabInteractable _interactable;

        private AudioSource _audioSource; // <--- Referencia audio

        [Networked] public NetworkId ActivePanelId { get; set; }
        // -----------------------------------------------

        private bool _isHeld = false;
        private float _timer = 0f;

        private void Awake()
        {
            _tokenSync = GetComponent<NetworkTokenSync>();
            _interactable = GetComponent<XRGrabInteractable>();

            _audioSource = GetComponent<AudioSource>(); // <--- Inicializar audio

            if (loaderCanvas != null) loaderCanvas.enabled = false;
        }

        public override void Spawned()
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.AddListener(OnGrabStart);
                _interactable.selectExited.AddListener(OnGrabEnd);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.RemoveListener(OnGrabStart);
                _interactable.selectExited.RemoveListener(OnGrabEnd);
            }
        }

        void Update()
        {
            // Solo corremos la barra si no hay panel (o si queremos forzar el check)
            if (_isHeld)
            {
                _timer += Time.deltaTime;
                float progress = _timer / holdDuration;

                if (loadingBarImage != null) loadingBarImage.fillAmount = progress;

                if (_timer >= holdDuration)
                {
                    TrySpawnOrFlash();

                    // Resetear para que no spamee
                    _timer = 0f;
                    _isHeld = false; // Soltamos lógica interna aunque siga agarrado
                    if (loaderCanvas != null) loaderCanvas.enabled = false;
                }
            }
        }

        void OnGrabStart(SelectEnterEventArgs args)
        {
            _isHeld = true;
            _timer = 0f;
            if (loaderCanvas != null)
            {
                loaderCanvas.enabled = true;
                if (loadingBarImage != null) loadingBarImage.fillAmount = 0;
            }
        }

        void OnGrabEnd(SelectExitEventArgs args)
        {
            _isHeld = false;
            _timer = 0f;
            if (loaderCanvas != null) loaderCanvas.enabled = false;
        }

        void TrySpawnOrFlash()
        {
            if (docPanelPrefab == null || _tokenSync == null) return;

            // 1. VERIFICAR SI YA EXISTE UN PANEL ACTIVO
            if (ActivePanelId.IsValid)
            {
                // Intentamos buscarlo en la red
                NetworkObject existingPanel = Runner.FindObject(ActivePanelId);

                if (existingPanel != null)
                {
                    // CASO A: Ya existe -> Hacerlo parpadear
                    Debug.Log("Panel ya existe. Parpadeando...");
                    var viewer = existingPanel.GetComponent<NetworkDocViewer>();
                    if (viewer != null) viewer.Rpc_FlashError(); // Llamada RPC
                    return;
                }
                else
                {
                    // CASO B: Teníamos un ID, pero el objeto fue destruido (cerrado con la X)
                    // Así que el ID es basura vieja. Procedemos a crear uno nuevo.
                }
            }

            // 2. CREAR NUEVO PANEL
            SpawnDocumentPanel();
        }

        void SpawnDocumentPanel()
        {
            string sourceID = _tokenSync.SourceNodeID.ToString();
            Debug.Log($"[Token] Creando panel para ID: {sourceID}");

            // --- SONIDO DE APERTURA ---
            if (_audioSource != null && openPanelSound != null)
            {
                _audioSource.PlayOneShot(openPanelSound);
            }
            // --------------------------

            // Spawneamos cerca del token
            Vector3 spawnPos = transform.position + new Vector3(0, 0.4f, 0);

            // Lo creamos
            NetworkObject panelObj = Runner.Spawn(docPanelPrefab, spawnPos, Quaternion.identity, Runner.LocalPlayer);

            if (panelObj != null)
            {
                // Guardamos la referencia para el futuro (Singleton Logic)
                ActivePanelId = panelObj.Id;

                // Inicializamos pasando el ID del documento Y el ID de este Token (para que nos siga)
                panelObj.GetComponent<NetworkDocViewer>().InitializePanel(sourceID, Object.Id);
            }
        }
    }
}