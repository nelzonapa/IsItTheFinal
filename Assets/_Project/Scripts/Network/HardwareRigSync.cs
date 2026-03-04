using UnityEngine;
using Fusion;
using Unity.XR.CoreUtils;
using ImmersiveGraph.Core;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ImmersiveGraph.Network
{
    public class HardwareRigSync : NetworkBehaviour
    {
        [Header("Partes del Avatar")]
        public Transform headTransform;
        public Transform leftHandTransform;
        public Transform rightHandTransform;

        [Header("Visuales para Ocultar localmente")]
        public Renderer[] bodyRenderers;

        // --- VARIABLES DE RED ESPACIALES ---
        [Networked] public Vector3 HeadPos { get; set; }
        [Networked] public Quaternion HeadRot { get; set; }

        [Networked] public Vector3 LeftHandPos { get; set; }
        [Networked] public Quaternion LeftHandRot { get; set; }

        [Networked] public Vector3 RightHandPos { get; set; }
        [Networked] public Quaternion RightHandRot { get; set; }

        // --- NUEVO: VARIABLE DE RED PARA ATENCIÓN (FASE 4) ---
        // NetworkString<_64> permite guardar un texto (el ID del nodo) sincronizado en red
        [Networked] public NetworkString<_64> SelectedNodeId { get; set; }

        // Acceso rápido a MÍ propio avatar local
        public static HardwareRigSync Local { get; private set; }

        // Referencias al Hardware Local
        private Transform _hardwareHead;
        private Transform _hardwareLeftHand;
        private Transform _hardwareRightHand;

        public override void Spawned()
        {
            ImmersiveGraph.Collaboration.SharedWorkspaceTracker.RegisteredAvatars.Add(this); // <- NUEVO
            // Si yo soy el dueño de este avatar, me asigno como el "Local"
            if (Object.HasStateAuthority || Object.HasInputAuthority)
            {
                Local = this;

                foreach (var r in bodyRenderers)
                    if (r != null) r.enabled = false;

                FindLocalHardware();
            }
        }

        // --- NUEVO ---
        private void OnDestroy()
        {
            ImmersiveGraph.Collaboration.SharedWorkspaceTracker.RegisteredAvatars.Remove(this);
        }

        // --- NUEVO: FUNCIÓN PARA CAMBIAR EL NODO SELECCIONADO ---
        public void SetSelectedNode(string nodeId)
        {
            if (Object.HasStateAuthority || Object.HasInputAuthority)
            {
                SelectedNodeId = nodeId;
            }
        }

        void FindLocalHardware()
        {
            GameObject activeRig = null;

            if (PlatformManager.Instance != null && PlatformManager.Instance.ActiveRig != null)
                activeRig = PlatformManager.Instance.ActiveRig;
            else
            {
                var xrOrig = FindFirstObjectByType<XROrigin>();
                if (xrOrig) activeRig = xrOrig.gameObject;
            }

            if (activeRig == null) return;

            if (activeRig.GetComponent<SimpleFPSController>() != null)
            {
                _hardwareHead = activeRig.GetComponentInChildren<Camera>().transform;
                var rayInteractor = activeRig.GetComponentInChildren<XRRayInteractor>();
                if (rayInteractor != null) _hardwareRightHand = rayInteractor.transform;
                _hardwareLeftHand = null;
            }
            else
            {
                XROrigin xrOrigin = activeRig.GetComponent<XROrigin>();
                if (xrOrigin == null) xrOrigin = activeRig.GetComponentInChildren<XROrigin>();

                if (xrOrigin != null)
                {
                    _hardwareHead = xrOrigin.Camera.transform;
                    foreach (var t in xrOrigin.GetComponentsInChildren<Transform>())
                    {
                        if (t.name.Contains("Left") && t.name.Contains("Controller")) _hardwareLeftHand = t;
                        if (t.name.Contains("Right") && t.name.Contains("Controller")) _hardwareRightHand = t;
                    }
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if ((Object.HasStateAuthority || Object.HasInputAuthority) && _hardwareHead != null)
            {
                HeadPos = _hardwareHead.position;
                HeadRot = _hardwareHead.rotation;

                if (_hardwareRightHand != null)
                {
                    RightHandPos = _hardwareRightHand.position;
                    RightHandRot = _hardwareRightHand.rotation;
                }
                else
                {
                    RightHandPos = HeadPos + new Vector3(0.2f, -0.2f, 0.2f);
                    RightHandRot = Quaternion.identity;
                }

                if (_hardwareLeftHand != null)
                {
                    LeftHandPos = _hardwareLeftHand.position;
                    LeftHandRot = _hardwareLeftHand.rotation;
                }
                else
                {
                    LeftHandPos = HeadPos + new Vector3(-0.2f, -0.5f, 0);
                    LeftHandRot = Quaternion.identity;
                }
            }
        }

        public override void Render()
        {
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