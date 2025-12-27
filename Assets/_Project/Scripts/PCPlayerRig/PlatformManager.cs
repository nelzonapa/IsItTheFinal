using UnityEngine;
using UnityEngine.XR.Management;

namespace ImmersiveGraph.Core
{
    public class PlatformManager : MonoBehaviour
    {
        public static PlatformManager Instance;

        public enum PlatformMode { AutoDetect, ForcePC, ForceVR }

        [Header("Configuración Editor")]
        public PlatformMode editorMode = PlatformMode.AutoDetect;

        [Header("Referencias Principales")]
        public GameObject vrRig;
        public GameObject pcRig;

        [Header("Objetos Exclusivos (Para Limpieza)")]
        public GameObject[] pcOnlyObjects; // Arrastra aquí el PC_HUD
        public GameObject[] vrOnlyObjects; // Si tuvieras algo exclusivo de VR

        public GameObject ActiveRig { get; private set; }

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            bool shouldUseVR = false;

#if UNITY_EDITOR
            switch (editorMode)
            {
                case PlatformMode.ForcePC: shouldUseVR = false; break;
                case PlatformMode.ForceVR: shouldUseVR = true; break;
                default: shouldUseVR = IsVRDevicePresent(); break;
            }
#else
            shouldUseVR = IsVRDevicePresent();
#endif

            if (shouldUseVR)
            {
                Debug.Log("[PlatformManager] Modo VR Activado.");
                SetMode(vrRig, pcRig, vrOnlyObjects, pcOnlyObjects);
            }
            else
            {
                Debug.Log("[PlatformManager] Modo PC Activado.");
                SetMode(pcRig, vrRig, pcOnlyObjects, vrOnlyObjects);
            }
        }

        void SetMode(GameObject activeRig, GameObject inactiveRig, GameObject[] activeExtras, GameObject[] inactiveExtras)
        {
            // 1. Rigs
            if (activeRig) activeRig.SetActive(true);
            if (inactiveRig) inactiveRig.SetActive(false);

            ActiveRig = activeRig;

            // 2. Objetos Extra (HUDs, etc)
            if (activeExtras != null)
                foreach (var obj in activeExtras) if (obj) obj.SetActive(true);

            if (inactiveExtras != null)
                foreach (var obj in inactiveExtras) if (obj) obj.SetActive(false);
        }

        bool IsVRDevicePresent()
        {
            var xrSettings = XRGeneralSettings.Instance;
            return xrSettings != null && xrSettings.Manager != null && xrSettings.Manager.isInitializationComplete;
        }
    }
}