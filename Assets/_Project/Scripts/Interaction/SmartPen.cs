using UnityEngine;
using Fusion; // Necesario para la red
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using ImmersiveGraph.Network; // Para acceder a NetworkConnectionLine

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class SmartPen : MonoBehaviour
    {
        [Header("Modo de Red")]
        public bool isNetworked = false; // <--- SI ES TRUE, USA FUSION
        public NetworkObject netLinePrefab; // Prefab para la red (Network_ConnectionLine)

        [Header("Referencias")]
        public Transform tipPoint;
        public Renderer tipRenderer;
        public GameObject localLinePrefab; // Prefab local antiguo

        [Header("Configuración")]
        public float detectionRadius = 0.02f;

        // ESTADO INTERNO
        private XRGrabInteractable _interactable;
        private Transform _startNode = null;
        private LineRenderer _ghostLine;
        private bool _isDrawing = false;
        private NetworkRunner _runner; // Referencia al runner

        void Awake()
        {
            _interactable = GetComponent<XRGrabInteractable>();

            // Configuración Línea Fantasma (Igual que antes)
            GameObject ghostObj = new GameObject("GhostLine");
            ghostObj.transform.SetParent(transform);
            _ghostLine = ghostObj.AddComponent<LineRenderer>();
            _ghostLine.startWidth = 0.002f;
            _ghostLine.endWidth = 0.002f;
            _ghostLine.material = new Material(Shader.Find("Sprites/Default"));
            _ghostLine.positionCount = 2;
            _ghostLine.useWorldSpace = true;
            _ghostLine.enabled = false;
        }

        void Start()
        {
            // Si es de red, buscamos el Runner
            if (isNetworked) _runner = FindFirstObjectByType<NetworkRunner>();
        }

        void OnEnable()
        {
            _interactable.activated.AddListener(OnTriggerPressed);
            _interactable.deactivated.AddListener(OnTriggerReleased);
        }

        void OnDisable()
        {
            _interactable.activated.RemoveListener(OnTriggerPressed);
            _interactable.deactivated.RemoveListener(OnTriggerReleased);
        }

        void Update()
        {
            if (_isDrawing) UpdateGhostLine();
        }

        void OnTriggerPressed(ActivateEventArgs args)
        {
            Collider[] hits = Physics.OverlapSphere(tipPoint.position, detectionRadius);

            foreach (var hit in hits)
            {
                // --- LÓGICA DE BORRADO ---
                // Buscamos líneas (Ya sean locales o de red)
                ConnectionLine localLine = hit.GetComponentInParent<ConnectionLine>();
                NetworkConnectionLine netLine = hit.GetComponentInParent<NetworkConnectionLine>();

                // Si tocamos el "Handle" (cubo de borrado) o la línea directa
                bool hitHandle = hit.GetComponent<CenterFollow>() != null || hit.name == "DeleteHandle";

                if (hitHandle || localLine != null || netLine != null)
                {
                    // BORRADO DE RED
                    if (isNetworked && netLine != null)
                    {
                        // Para borrar en red, usamos Despawn
                        // OJO: Solo el dueño o el servidor pueden despawnear. 
                        // Si no eres el dueño, primero pides autoridad o usas un RPC (Remote Procedure Call).
                        // Por simplicidad ahora: Asumimos que podemos borrar o solicitamos autoridad.
                        if (netLine.Object.HasStateAuthority)
                        {
                            _runner.Despawn(netLine.Object);
                        }
                        else
                        {
                            // Si no soy dueño, truco rápido: destruir localmente y esperar que la red sincronice? NO.
                            // Lo correcto es pedir autoridad y luego borrar, o RPC.
                            // Por ahora solo permitimos borrar si tienes autoridad.
                            Debug.LogWarning("No tienes autoridad para borrar esta línea de red.");
                        }
                        return;
                    }
                    // BORRADO LOCAL
                    else if (!isNetworked && localLine != null)
                    {
                        Destroy(localLine.gameObject);
                        return;
                    }
                }

                // --- LÓGICA DE DIBUJO (INICIO) ---
                if (hit.CompareTag("Connectable"))
                {
                    _startNode = hit.transform;
                    _isDrawing = true;
                    _ghostLine.enabled = true;
                    _ghostLine.SetPosition(0, _startNode.position);
                    _ghostLine.SetPosition(1, tipPoint.position);
                    return;
                }
            }
        }

        void OnTriggerReleased(DeactivateEventArgs args)
        {
            if (_isDrawing)
            {
                Collider[] hits = Physics.OverlapSphere(tipPoint.position, detectionRadius);
                Transform endNode = null;

                foreach (var hit in hits)
                {
                    if (hit.CompareTag("Connectable") && hit.transform != _startNode)
                    {
                        endNode = hit.transform;
                        break;
                    }
                }

                if (endNode != null)
                {
                    if (isNetworked)
                    {
                        CreateNetworkConnection(_startNode, endNode);
                    }
                    else
                    {
                        CreateLocalConnection(_startNode, endNode);
                    }
                }

                _isDrawing = false;
                _ghostLine.enabled = false;
                _startNode = null;
            }
        }

        void UpdateGhostLine()
        {
            if (_startNode != null)
            {
                _ghostLine.SetPosition(0, _startNode.position);
                _ghostLine.SetPosition(1, tipPoint.position);
            }
        }

        // --- CREACIÓN LOCAL (Tu código anterior) ---
        void CreateLocalConnection(Transform start, Transform end)
        {
            GameObject lineObj = Instantiate(localLinePrefab);
            ConnectionLine script = lineObj.GetComponent<ConnectionLine>();
            script.Initialize(start, end);
            AddDeleteHandle(lineObj, start, end);
        }

        // --- CREACIÓN EN RED (Nuevo) ---
        void CreateNetworkConnection(Transform start, Transform end)
        {
            if (_runner == null || !_runner.IsRunning) return;

            // Obtenemos los NetworkObjects de los nodos (PostIts/Tokens)
            NetworkObject startNet = start.GetComponentInParent<NetworkObject>();
            NetworkObject endNet = end.GetComponentInParent<NetworkObject>();

            if (startNet != null && endNet != null)
            {
                // Spawneamos la línea en la red
                NetworkObject lineObj = _runner.Spawn(netLinePrefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer);

                // Configurar conexiones usando IDs de red
                lineObj.GetComponent<NetworkConnectionLine>().SetConnections(startNet.Id, endNet.Id);

                // El Handle de borrado se crea localmente en el script NetworkConnectionLine (Start/Update) 
                // o añadimos lógica aquí para crearlo visualmente, pero NetworkConnectionLine debería encargarse.
            }
        }

        // Helper para el cubo de borrado local
        // Helper para el cubo de borrado local
        void AddDeleteHandle(GameObject parent, Transform a, Transform b)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "DeleteHandle";
            handle.transform.SetParent(parent.transform);
            handle.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);

            Renderer r = handle.GetComponent<Renderer>();
            if (r != null)
            {
                // --- CORRECCIÓN ANTI-ROSA PARA QUEST ---
                // Usamos el mismo shader seguro que en la red
                r.material = new Material(Shader.Find("Sprites/Default"));
                r.material.color = Color.red;
            }

            CenterFollow follower = handle.AddComponent<CenterFollow>();
            follower.a = a;
            follower.b = b;

            Collider col = handle.GetComponent<Collider>();
            if (col) col.isTrigger = true;
        }
    }
}