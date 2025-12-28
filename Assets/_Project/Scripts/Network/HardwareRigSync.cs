using UnityEngine;
using Fusion;
using Unity.XR.CoreUtils;
using ImmersiveGraph.Core; // <--- NECESARIO PARA PLATFORMMANAGER
using UnityEngine.XR.Interaction.Toolkit.Interactors; // Para buscar el Rayo

namespace ImmersiveGraph.Network
{
    public class HardwareRigSync : NetworkBehaviour
    {
        [Header("Partes del Avatar (Arrastra los hijos del Prefab)")]
        public Transform headTransform;
        public Transform leftHandTransform;
        public Transform rightHandTransform;

        [Header("Visuales para Ocultar localmente")]
        public Renderer[] bodyRenderers;

        // --- VARIABLES DE RED ---
        [Networked] public Vector3 HeadPos { get; set; }
        [Networked] public Quaternion HeadRot { get; set; }

        [Networked] public Vector3 LeftHandPos { get; set; }
        [Networked] public Quaternion LeftHandRot { get; set; }

        [Networked] public Vector3 RightHandPos { get; set; }
        [Networked] public Quaternion RightHandRot { get; set; }

        // Referencias al Hardware Local
        private Transform _hardwareHead;
        private Transform _hardwareLeftHand;
        private Transform _hardwareRightHand;

        public override void Spawned()
        {
            // 1. Ocultar mi propio cuerpo localmente
            if (Object.HasInputAuthority)
            {
                foreach (var r in bodyRenderers)
                    if (r != null) r.enabled = false;

                // 2. BUSCAR HARDWARE (Lógica Híbrida)
                FindLocalHardware();
            }
        }

        void FindLocalHardware()
        {
            // Usamos el PlatformManager para saber qué Rig está activo
            GameObject activeRig = null;

            if (PlatformManager.Instance != null && PlatformManager.Instance.ActiveRig != null)
            {
                activeRig = PlatformManager.Instance.ActiveRig;
            }
            else
            {
                // Fallback por si acaso
                var xrOrig = FindFirstObjectByType<XROrigin>();
                if (xrOrig) activeRig = xrOrig.gameObject;
            }

            if (activeRig == null)
            {
                Debug.LogError("[HardwareRigSync] No se encontró ningún Rig activo.");
                return;
            }

            // --- CASO A: ES EL RIG DE PC ---
            // (Sabemos que es PC si tiene el componente SimpleFPSController)
            if (activeRig.GetComponent<SimpleFPSController>() != null)
            {
                Debug.Log("[HardwareSync] Configurando para PC...");

                // Cabeza = La Cámara del PC
                _hardwareHead = activeRig.GetComponentInChildren<Camera>().transform;

                // Mano Derecha = El Ray Interactor
                var rayInteractor = activeRig.GetComponentInChildren<XRRayInteractor>();
                if (rayInteractor != null)
                {
                    _hardwareRightHand = rayInteractor.transform;
                }

                // Mano Izquierda = No existe en PC (La dejamos null o la pegamos al cuerpo)
                _hardwareLeftHand = null;
            }
            // --- CASO B: ES EL RIG DE VR ---
            else
            {
                Debug.Log("[HardwareSync] Configurando para VR...");

                // Intentamos obtener el XROrigin del objeto activo
                XROrigin xrOrigin = activeRig.GetComponent<XROrigin>();
                if (xrOrigin == null) xrOrigin = activeRig.GetComponentInChildren<XROrigin>();

                if (xrOrigin != null)
                {
                    _hardwareHead = xrOrigin.Camera.transform;

                    // Búsqueda de manos VR
                    foreach (var t in xrOrigin.GetComponentsInChildren<Transform>())
                    {
                        // Nombres comunes en Unity XR Grid
                        if (t.name.Contains("Left") && t.name.Contains("Controller")) _hardwareLeftHand = t;
                        if (t.name.Contains("Right") && t.name.Contains("Controller")) _hardwareRightHand = t;
                    }
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            // ESCRIBIR DATOS (Solo el dueño)
            if (Object.HasInputAuthority && _hardwareHead != null)
            {
                // Sincronizar Cabeza
                HeadPos = _hardwareHead.position;
                HeadRot = _hardwareHead.rotation;

                // Sincronizar Mano Derecha
                if (_hardwareRightHand != null)
                {
                    RightHandPos = _hardwareRightHand.position;
                    RightHandRot = _hardwareRightHand.rotation;
                }
                else
                {
                    // Si no hay mano derecha (raro), la pegamos al cuerpo
                    RightHandPos = HeadPos + new Vector3(0.2f, -0.2f, 0.2f);
                    RightHandRot = Quaternion.identity;
                }

                // Sincronizar Mano Izquierda
                if (_hardwareLeftHand != null)
                {
                    LeftHandPos = _hardwareLeftHand.position;
                    LeftHandRot = _hardwareLeftHand.rotation;
                }
                else
                {
                    // EN PC: Como no hay mano izquierda, la escondemos dentro del cuerpo o la ponemos abajo
                    // Para que no flote sola en el (0,0,0)
                    LeftHandPos = HeadPos + new Vector3(-0.2f, -0.5f, 0); // Un poco abajo a la izquierda
                    LeftHandRot = Quaternion.identity;
                }
            }
        }

        public override void Render()
        {
            // LEER DATOS (Todos los clientes)
            // Esto no cambia, interpolamos lo que recibimos de la red
            if (headTransform != null)
            {
                headTransform.position = Vector3.Lerp(headTransform.position, HeadPos, Time.deltaTime * 20);
                headTransform.rotation = Quaternion.Slerp(headTransform.rotation, HeadRot, Time.deltaTime * 20);
            }

            if (leftHandTransform != null)
            {
                leftHandTransform.position = Vector3.Lerp(leftHandTransform.position, LeftHandPos, Time.deltaTime * 20);
                leftHandTransform.rotation = Quaternion.Slerp(leftHandTransform.rotation, LeftHandRot, Time.deltaTime * 20);
            }

            if (rightHandTransform != null)
            {
                rightHandTransform.position = Vector3.Lerp(rightHandTransform.position, RightHandPos, Time.deltaTime * 20);
                rightHandTransform.rotation = Quaternion.Slerp(rightHandTransform.rotation, RightHandRot, Time.deltaTime * 20);
            }
        }
    }
}