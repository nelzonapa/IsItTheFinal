using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Unity 6 / XRI 3

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class SmartPen : MonoBehaviour
    {
        [Header("Configuración")]
        public Transform tipPoint;
        public Material drawingMaterial;
        public float lineWidth = 0.005f;

        [Header("Filtros")]
        public LayerMask drawingLayers;

        private XRGrabInteractable _interactable;
        private LineRenderer _currentLine;
        private bool _isDrawing = false;
        private int _positionCount = 0;
        private Vector3 _lastPos;

        void Start()
        {
            _interactable = GetComponent<XRGrabInteractable>();
            // Eventos de gatillo (Activate)
            _interactable.activated.AddListener(StartDrawing);
            _interactable.deactivated.AddListener(StopDrawing);
        }

        void StartDrawing(ActivateEventArgs args)
        {
            // Solo empezamos si presionas el gatillo
            _isDrawing = true;
            CreateNewLine();
        }

        void StopDrawing(DeactivateEventArgs args)
        {
            _isDrawing = false;
            _currentLine = null;
        }

        void CreateNewLine()
        {
            // Creamos el trazo
            GameObject lineObj = new GameObject($"Stroke_{Time.time}");

            _currentLine = lineObj.AddComponent<LineRenderer>();
            _currentLine.material = drawingMaterial;
            _currentLine.startWidth = lineWidth;
            _currentLine.endWidth = lineWidth;
            _currentLine.positionCount = 0;
            _currentLine.useWorldSpace = true;
            _currentLine.numCapVertices = 5;

            // IMPORTANTE: El trazo también debe ser clonable
            lineObj.tag = "ClonableItem";

            // Opcional: Si quieres pintar sobre tu propia pintura, ponle layer Writeable
            // lineObj.layer = LayerMask.NameToLayer("Writeable"); 

            _positionCount = 0;
        }

        void Update()
        {
            if (_isDrawing && _currentLine != null)
            {
                // RAYCAST CHECK: ¿La punta está tocando una superficie "Writeable"?
                // Lanzamos un rayo minúsculo desde la punta hacia adelante (o hacia adentro)
                // Si toca algo en la LayerMask, pintamos.
                if (Physics.CheckSphere(tipPoint.position, 0.005f, drawingLayers))
                {
                    if (Vector3.Distance(tipPoint.position, _lastPos) > 0.002f)
                    {
                        UpdateLine();
                    }
                }
                else
                {
                    // Si levantas el lápiz del papel pero sigues apretando,
                    // cortamos el trazo actual para no hacer líneas voladoras.
                    if (_currentLine.positionCount > 0)
                    {
                        _currentLine = null; // Romper el trazo actual
                        CreateNewLine();     // Preparar uno nuevo por si vuelve a tocar
                    }
                }
            }
        }

        void UpdateLine()
        {
            _currentLine.positionCount++;
            _currentLine.SetPosition(_positionCount - 1, tipPoint.position);
            _lastPos = tipPoint.position;
        }
    }
}