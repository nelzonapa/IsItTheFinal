using UnityEngine;
using UnityEngine.XR.Management; // Necesario para detectar VR

namespace ImmersiveGraph.Core
{
    public class PlatformManager : MonoBehaviour
    {
        [Header("Rigs")]
        public GameObject vrRig; // Tu XR Origin actual
        public GameObject pcRig; // El PC_Player_Rig que acabamos de hacer

        void Awake()
        {
            // Detección automática: ¿Hay un subsistema XR activo?
            bool isVR = false;
            var xrSettings = XRGeneralSettings.Instance;
            if (xrSettings != null && xrSettings.Manager != null)
            {
                isVR = xrSettings.Manager.isInitializationComplete;
            }

            // OVERRIDE MANUAL PARA PRUEBAS EN EDITOR (Comenta/Descomenta)
            isVR = true; //Descomenta esto para forzar modo PC en el Editor

            if (isVR)
            {
                Debug.Log("[PlatformManager] Modo VR Detectado.");
                if (vrRig) vrRig.SetActive(true);
                if (pcRig) pcRig.SetActive(false);
            }
            else
            {
                Debug.Log("[PlatformManager] Modo PC Detectado.");
                if (vrRig) vrRig.SetActive(false);
                if (pcRig) pcRig.SetActive(true);
            }
        }
    }
}