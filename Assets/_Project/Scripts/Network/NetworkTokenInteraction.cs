using Fusion;
using UnityEngine;
using UnityEngine.UI; // Para la barra de carga
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Para detectar el Grab
// Si usas XR Toolkit nuevo (2.x o 3.x), usa: UnityEngine.XR.Interaction.Toolkit.Interactables;
// Si te da error la línea de arriba, bórrala y usa solo la primera.

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class NetworkTokenInteraction : NetworkBehaviour
    {
        [Header("Configuración")]
        public float holdDuration = 4.0f;
        public Vector3 panelSpawnOffset = new Vector3(0, 0.4f, 0); // Aparecerá 40cm arriba del token

        [Header("Referencias")]
        public Image loadingBarImage; // La imagen circular o barra que se llenará
        public Canvas loaderCanvas;   // Para ocultarlo cuando no se usa
        public NetworkObject docPanelPrefab; // El prefab Network_DocPanel

        // Referencia obligatoria para sacar el ID
        private NetworkTokenSync _tokenSync;
        private XRGrabInteractable _interactable;

        // Lógica Interna
        private bool _isHeld = false;
        private float _timer = 0f;
        private bool _hasSpawned = false;

        private void Awake()
        {
            _tokenSync = GetComponent<NetworkTokenSync>();
            _interactable = GetComponent<XRGrabInteractable>();

            // Apagar barra al inicio
            if (loaderCanvas != null) loaderCanvas.enabled = false;
        }

        public override void Spawned()
        {
            // Suscribirse a eventos de agarre (Solo nos importa si somos nosotros quienes lo agarramos)
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
            // La lógica visual corre en local para quien lo sostiene
            if (_isHeld && !_hasSpawned)
            {
                _timer += Time.deltaTime;
                float progress = _timer / holdDuration;

                // Actualizar barra visual
                if (loadingBarImage != null) loadingBarImage.fillAmount = progress;

                // CHEQUEO DE TIEMPO
                if (_timer >= holdDuration)
                {
                    SpawnDocumentPanel();
                    _hasSpawned = true; // Evitar que spawnee 100 paneles por segundo
                    if (loaderCanvas != null) loaderCanvas.enabled = false; // Ocultar barra
                }
            }
        }

        void OnGrabStart(SelectEnterEventArgs args)
        {
            // Solo activamos la lógica si somos nosotros quien lo agarra
            // (En VR, el SelectEnter suele dispararse en el cliente local)
            _isHeld = true;
            _timer = 0f;
            _hasSpawned = false;

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
            _hasSpawned = false;

            if (loaderCanvas != null) loaderCanvas.enabled = false;
        }

        void SpawnDocumentPanel()
        {
            // VALIDACIONES DE SEGURIDAD
            if (docPanelPrefab == null) { Debug.LogError("Falta asignar docPanelPrefab"); return; }
            if (_tokenSync == null) { Debug.LogError("No tengo NetworkTokenSync para leer el ID"); return; }

            // Leemos el ID guardado en la Fase 1
            string sourceID = _tokenSync.SourceNodeID.ToString();

            Debug.Log($"[Token] Invocando panel para ID: {sourceID}");

            // SPAWN EN LA RED
            // Usamos Runner.Spawn. 
            // Posición: Donde está el token + offset
            Vector3 spawnPos = transform.position + panelSpawnOffset;
            Quaternion spawnRot = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            // (Truco: Que aparezca mirando hacia el usuario, o usa Quaternion.identity)

            NetworkObject panelObj = Runner.Spawn(docPanelPrefab, spawnPos, Quaternion.identity, Runner.LocalPlayer);

            // INYECCIÓN DE DATOS (FASE 2)
            // Obtenemos el script del panel y le pasamos el ID
            if (panelObj != null)
            {
                panelObj.GetComponent<NetworkDocViewer>().InitializePanel(sourceID);
            }
        }
    }
}