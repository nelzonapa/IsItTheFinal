using UnityEngine;
using Fusion;
using Unity.XR.CoreUtils; // Necesario para encontrar el XROrigin

namespace ImmersiveGraph.Network
{
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
            // Solo si soy el dueño de este avatar, busco mi hardware real
            if (Object.HasStateAuthority)
            {
                // 1. Buscar el XR Origin (la base de tu VR)
                var rig = FindFirstObjectByType<XROrigin>();
                if (rig == null)
                {
                    Debug.LogError("ERROR CRÍTICO: No encuentro el 'XR Origin' en la escena.");
                    return;
                }

                // 2. La cámara siempre es fácil de encontrar
                _localHead = rig.Camera.transform;

                // 3. Buscar las manos por NOMBRE dentro de los hijos del XR Origin
                // Esto busca recursivamente, así que las encontrará aunque estén dentro de "Camera Offset"
                Transform[] allChildren = rig.GetComponentsInChildren<Transform>(true);

                foreach (var child in allChildren)
                {
                    if (child.name.Contains("Left Controller"))
                    {
                        _localLeftHand = child;
                        Debug.Log(" Mano Izquierda encontrada: " + child.name);
                    }
                    else if (child.name.Contains("Right Controller"))
                    {
                        _localRightHand = child;
                        Debug.Log(" Mano Derecha encontrada: " + child.name);
                    }
                }

                if (_localLeftHand == null) Debug.LogWarning(" No encontré un objeto llamado 'Left Controller'");
                if (_localRightHand == null) Debug.LogWarning(" No encontré un objeto llamado 'Right Controller'");

                // 4. Ocultar la cabeza del avatar propio para no verla desde dentro
                if (headTransform != null)
                {
                    Renderer headRend = headTransform.GetComponent<Renderer>();
                    if (headRend != null) headRend.enabled = false;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Solo sincronizamos si somos el dueño y encontramos las referencias
            if (Object.HasStateAuthority)
            {
                if (_localHead != null)
                {
                    headTransform.position = _localHead.position;
                    headTransform.rotation = _localHead.rotation;
                }

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