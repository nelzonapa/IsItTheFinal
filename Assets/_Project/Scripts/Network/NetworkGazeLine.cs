using UnityEngine;
using Fusion;

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(LineRenderer))]
    public class NetworkGazeLine : NetworkBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Arrastra aquí el objeto que representa la CABEZA o la CÁMARA del avatar sincronizado")]
        public Transform headTransform;

        [Header("Configuración")]
        public float lineLength = 5.0f; // Qué tan lejos llega la línea
        public float startWidth = 0.005f;
        public float endWidth = 0.001f; // Se hace finita al final para que sea sutil

        // Colores (Copiados de tu lógica anterior para mantener consistencia)
        private Color[] playerColors = new Color[]
        {
            Color.cyan,      // Jugador 0
            Color.magenta,   // Jugador 1
            Color.yellow,    // Jugador 2
            Color.green      // Jugador 3
        };

        private LineRenderer _lr;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.positionCount = 2;
            _lr.startWidth = startWidth;
            _lr.endWidth = endWidth;

            // Asignar material básico si no tiene (para evitar el color rosa)
            if (_lr.material == null)
            {
                _lr.material = new Material(Shader.Find("Sprites/Default"));
            }
        }

        public override void Spawned()
        {
            // LÓGICA DE VISIBILIDAD
            // HasInputAuthority = Soy YO. 
            // !HasInputAuthority = Son los OTROS.

            if (Object.HasInputAuthority)
            {
                // Si soy yo, apago la línea. No quiero ver mi propia mirada.
                _lr.enabled = false;
            }
            else
            {
                // Si es otro jugador, la enciendo para ver qué mira.
                _lr.enabled = true;
                ApplyColor();
            }
        }

        void ApplyColor()
        {
            // Calculamos el color basado en el ID del jugador dueño de este avatar
            int playerId = Object.StateAuthority.PlayerId;
            Color myColor = playerColors[playerId % playerColors.Length];

            // Creamos un degradado: Color sólido al inicio -> Transparente al final
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(myColor, 0.0f), new GradientColorKey(myColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.3f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) } // 30% visible a 0% visible
            );

            _lr.colorGradient = gradient;
        }

        // Usamos LateUpdate para que la línea siga a la cabeza suavemente después de que esta se mueva
        void LateUpdate()
        {
            // Solo actualizamos si la línea está visible y tenemos la referencia de la cabeza
            if (_lr.enabled && headTransform != null)
            {
                Vector3 origin = headTransform.position;
                Vector3 direction = headTransform.forward;

                _lr.SetPosition(0, origin);

                // Raycast opcional: Si quieres que la línea se corte cuando choca con la mesa
                if (Physics.Raycast(origin, direction, out RaycastHit hit, lineLength))
                {
                    _lr.SetPosition(1, hit.point);
                }
                else
                {
                    _lr.SetPosition(1, origin + direction * lineLength);
                }
            }
        }
    }
}