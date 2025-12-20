using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class SmartPen : MonoBehaviour
    {
        [Header("Referencias")]
        public Transform tipPoint;
        public Renderer tipRenderer;
        public GameObject connectionLinePrefab;

        [Header("Configuración")]
        public float detectionRadius = 0.02f;

        // ESTADO
        private XRGrabInteractable _interactable;
        private Transform _startNode = null;
        private LineRenderer _ghostLine;
        private bool _isDrawing = false;

        void Awake()
        {
            _interactable = GetComponent<XRGrabInteractable>();

            // Línea fantasma
            GameObject ghostObj = new GameObject("GhostLine");
            ghostObj.transform.SetParent(transform);
            _ghostLine = ghostObj.AddComponent<LineRenderer>();
            _ghostLine.startWidth = 0.002f;
            _ghostLine.endWidth = 0.002f;
            _ghostLine.material = new Material(Shader.Find("Sprites/Default"));
            _ghostLine.positionCount = 2; // Importante inicializar
            _ghostLine.useWorldSpace = true; // Importante
            _ghostLine.enabled = false;
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
            if (_isDrawing)
            {
                UpdateGhostLine();
            }
        }

        void OnTriggerPressed(ActivateEventArgs args)
        {
            Collider[] hits = Physics.OverlapSphere(tipPoint.position, detectionRadius);

            foreach (var hit in hits)
            {
                // A. BORRADOR (Buscamos el CenterFollow o ConnectionLine)
                // El cubo del medio tendrá CenterFollow, que es hijo de ConnectionLine
                ConnectionLine line = hit.GetComponentInParent<ConnectionLine>();

                // Verificamos que lo que tocamos sea el "Handle" (el cubo) o la línea en sí
                if (line != null)
                {
                    // Truco: Para no borrar la línea apenas la creas, verifica distancia o tag
                    // Pero por ahora, asumimos que si tocas el cubo central, es para borrar.
                    if (hit.GetComponent<CenterFollow>() != null)
                    {
                        Destroy(line.gameObject);
                        Debug.Log("Conexión borrada");
                        return;
                    }
                }

                // B. DIBUJAR (Nodos)
                if (hit.CompareTag("Connectable"))
                {
                    _startNode = hit.transform;
                    _isDrawing = true;
                    _ghostLine.enabled = true;
                    // Actualizar ghost line inmediatamente para que salga de la punta
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
                    // Evitar conectarse a sí mismo
                    if (hit.CompareTag("Connectable") && hit.transform != _startNode)
                    {
                        endNode = hit.transform;
                        break;
                    }
                }

                if (endNode != null)
                {
                    CreateConnection(_startNode, endNode);
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

        void CreateConnection(Transform start, Transform end)
        {
            GameObject lineObj = Instantiate(connectionLinePrefab);

            ConnectionLine script = lineObj.GetComponent<ConnectionLine>();
            script.Initialize(start, end);

            // Crear el cubo de borrado
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "DeleteHandle";
            handle.transform.SetParent(lineObj.transform);
            handle.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f); // Tamaño del cubo borrador

            // Material rojo para el cubo (opcional, para saber que es borrable)
            Renderer r = handle.GetComponent<Renderer>();
            if (r != null) r.material.color = Color.red;

            // Script para seguir el centro
            CenterFollow follower = handle.AddComponent<CenterFollow>();
            follower.a = start;
            follower.b = end;

            // Asegurar que el cubo tenga collider (CreatePrimitive ya se lo pone)
            // Asegurar que sea Trigger para que el lápiz entre suave
            Collider col = handle.GetComponent<Collider>();
            if (col) col.isTrigger = true;
        }
    }
}