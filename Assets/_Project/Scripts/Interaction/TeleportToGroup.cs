using Fusion;
using Unity.XR.CoreUtils; // Para mover el XROrigin
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Para el botón

namespace ImmersiveGraph.Network
{
    // Requiere un componente interactuable (como un botón o cubo simple)
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class TeleportToGroup : NetworkBehaviour
    {
        private XROrigin _xrOrigin;

        void Start()
        {
            // Buscar el XR Origin automáticamente
            _xrOrigin = FindFirstObjectByType<XROrigin>();

            // Configurar el evento de selección
            var interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnButtonPressed);
        }

        public void OnButtonPressed(SelectEnterEventArgs args)
        {
            if (GroupTableManager.Instance == null || Runner == null) return;

            Debug.Log("Teletransportando al espacio grupal...");

            // 1. Obtener mi zona asignada (Spawn Point)
            // Usamos la misma lógica que las bandejas: Mi ID define mi lugar
            Transform myReceptionZone = GroupTableManager.Instance.GetReceptionZoneForPlayer(Runner.LocalPlayer);

            // 2. Calcular posición frente a la mesa
            // La zona de recepción está EN la mesa. Nosotros queremos estar PARADOS frente a ella.
            // Movemos la posición 1 metro hacia atrás respecto al centro de la mesa (0,0,0)
            Vector3 tableCenter = Vector3.zero;
            Vector3 directionFromCenter = (myReceptionZone.position - tableCenter).normalized;

            // Posición final: A 2 metros del centro, en la dirección de mi bandeja
            Vector3 standPosition = directionFromCenter * 2.0f;
            standPosition.y = 0.0f; // Al suelo

            // 3. Mover el Rig
            if (_xrOrigin != null)
            {
                _xrOrigin.transform.position = standPosition;

                // 4. Rotar para mirar al centro de la mesa
                _xrOrigin.transform.LookAt(new Vector3(0, _xrOrigin.Camera.transform.position.y, 0));
            }
        }
    }
}