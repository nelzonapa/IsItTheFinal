using UnityEngine;
using Fusion;
using Unity.XR.CoreUtils; // Necesario para encontrar el hardware

namespace ImmersiveGraph.Network
{
    public class HardwareRigSync : NetworkBehaviour
    {
        [Header("Partes del Avatar (Arrastra los hijos del Prefab)")]
        public Transform headTransform;
        public Transform leftHandTransform;
        public Transform rightHandTransform;

        [Header("Visuales para Ocultar localmente")]
        public Renderer[] bodyRenderers; // Arrastra aquí los MeshRenderers de Cabeza y Manos

        // --- VARIABLES DE RED (La "Verdad" sincronizada) ---
        // Sincronizamos Posición y Rotación de cada parte
        [Networked] public Vector3 HeadPos { get; set; }
        [Networked] public Quaternion HeadRot { get; set; }

        [Networked] public Vector3 LeftHandPos { get; set; }
        [Networked] public Quaternion LeftHandRot { get; set; }

        [Networked] public Vector3 RightHandPos { get; set; }
        [Networked] public Quaternion RightHandRot { get; set; }

        // Referencias al Hardware Local (Tu casco real)
        private Transform _hardwareHead;
        private Transform _hardwareLeftHand;
        private Transform _hardwareRightHand;
        private XROrigin _xrOrigin;

        public override void Spawned()
        {
            // 1. Ocultar mi propio cuerpo para no ver una esfera en mi cara
            if (Object.HasInputAuthority)
            {
                foreach (var r in bodyRenderers)
                    if (r != null) r.enabled = false; // Solo apago el render, el objeto sigue ahí para la lógica
            }

            // 2. Buscar el Hardware Local (Solo si soy el dueño)
            if (Object.HasInputAuthority)
            {
                _xrOrigin = FindFirstObjectByType<XROrigin>();
                if (_xrOrigin != null)
                {
                    _hardwareHead = _xrOrigin.Camera.transform;

                    // Buscar mandos (asumiendo nombres estándar de Unity XR)
                    // Un truco seguro es buscar por componentes o nombres comunes
                    var hands = _xrOrigin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor>(true);

                    // Búsqueda manual robusta en la jerarquía del XROrigin
                    foreach (var t in _xrOrigin.GetComponentsInChildren<Transform>())
                    {
                        if (t.name.Contains("Left") && t.name.Contains("Controller")) _hardwareLeftHand = t;
                        if (t.name.Contains("Right") && t.name.Contains("Controller")) _hardwareRightHand = t;
                    }

                    // Si no los encuentra por nombre, asigna la cámara como fallback para que no crashee
                    if (_hardwareLeftHand == null) _hardwareLeftHand = _hardwareHead;
                    if (_hardwareRightHand == null) _hardwareRightHand = _hardwareHead;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            // ESCRIBIR DATOS: Si soy yo, leo mi hardware y lo subo a la red
            if (Object.HasInputAuthority && _hardwareHead != null)
            {
                // Usamos LocalPosition respecto al XROrigin si el NetworkPlayer está en (0,0,0)
                // O usamos WorldPosition si el NetworkTransform mueve el root.

                // MEJOR ESTRATEGIA: Sincronizar coordenadas LOCALES relativas al "Playspace"
                // Asumimos que el NetworkPlayer Root ya está en la posición correcta (Escritorio) gracias al Spawner.
                // Entonces sincronizamos la posición local del hardware.

                // Cabeza
                HeadPos = _hardwareHead.position;
                HeadRot = _hardwareHead.rotation;

                // Manos
                if (_hardwareLeftHand != null)
                {
                    LeftHandPos = _hardwareLeftHand.position;
                    LeftHandRot = _hardwareLeftHand.rotation;
                }

                if (_hardwareRightHand != null)
                {
                    RightHandPos = _hardwareRightHand.position;
                    RightHandRot = _hardwareRightHand.rotation;
                }
            }
        }

        public override void Render()
        {
            // LEER DATOS: Todos (incluido yo para suavizado) aplicamos los datos de red a los huesos visuales

            // Interpolación para que se vea suave (Lerp)
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