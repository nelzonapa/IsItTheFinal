using UnityEngine;
using Fusion; // Necesario para Photon

namespace ImmersiveGraph.Network
{
    // Este script va en el PREFAB del NetworkPlayer
    public class HardwareRigSync : NetworkBehaviour
    {
        [Header("Partes del Avatar (Red)")]
        public Transform headTransform;
        public Transform leftHandTransform;
        public Transform rightHandTransform;

        // Referencias a tu Hardware Real (Local)
        private Transform _localHead;
        private Transform _localLeftHand;
        private Transform _localRightHand;

        public override void Spawned()
        {
            // Esta función se ejecuta cuando el objeto nace en la red.

            // Si yo soy el dueño de este objeto (IsStateAuthority), debo buscar mi VR Rig local.
            // Si no soy el dueño (es el avatar de otro jugador), no hago nada (solo lo veo moverse).
            if (Object.HasStateAuthority)
            {
                // Buscar el XR Origin en la escena automáticamente
                // Asumimos que usas el nombre estándar de Unity XR
                var rig = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (rig == null)
                {
                    Debug.LogError("¡No encuentro el XR Origin! ¿Estás en la escena correcta?");
                    return;
                }

                _localHead = rig.Camera.transform;

                // Buscar manos (Truco: buscamos por nombre o componentes)
                // Ajusta estos nombres si tus manos se llaman diferente en el XR Origin
                var hands = rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
                foreach (var hand in hands)
                {
                    if (hand.name.Contains("Left")) _localLeftHand = hand.transform;
                    if (hand.name.Contains("Right")) _localRightHand = hand.transform;
                }

                // Ocultar mi propia cabeza para no ver una esfera azul dentro de mis ojos
                headTransform.GetComponent<Renderer>().enabled = false;
            }
        }

        // FixedUpdateNetwork es como el Update, pero sincronizado
        public override void FixedUpdateNetwork()
        {
            // Solo si soy el dueño, copio la posición de mi VR real al Avatar de red
            if (Object.HasStateAuthority && _localHead != null)
            {
                // Sincronizar Cabeza
                headTransform.position = _localHead.position;
                headTransform.rotation = _localHead.rotation;

                // Sincronizar Manos
                if (_localLeftHand != null)
                {
                    leftHandTransform.position = _localLeftHand.position;
                    leftHandTransform.rotation = _localLeftHand.rotation;
                }
                if (_localRightHand != null)
                {
                    rightHandTransform.position = _localRightHand.position;
                    rightHandTransform.rotation = _localRightHand.rotation;
                }
            }
        }
    }
}