using UnityEngine;
using Fusion;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using ImmersiveGraph.Core;

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class ReturnToIndividual : MonoBehaviour
    {
        [Header("Sonido (Opcional)")]
        public AudioSource audioSource;
        public AudioClip returnSound;

        private NetworkRunner _runner;

        void Start()
        {
            _runner = FindFirstObjectByType<NetworkRunner>();
        }

        public void ReturnHome()
        {
            if (audioSource != null && returnSound != null)
            {
                audioSource.PlayOneShot(returnSound);
            }

            if (_runner == null) _runner = FindFirstObjectByType<NetworkRunner>();

            if (_runner == null || !_runner.IsRunning || GroupTableManager.Instance == null)
            {
                Debug.LogWarning("No hay conexión de red o Manager. No se puede calcular el escritorio de retorno.");
                return;
            }

            Transform myDesk = GroupTableManager.Instance.GetIndividualDeskForPlayer(_runner.LocalPlayer);

            if (myDesk != null)
            {
                TeleportPlayer(myDesk);
            }
            else
            {
                Debug.LogError("El GroupTableManager no devolvió un escritorio válido para este jugador.");
            }
        }

        private void TeleportPlayer(Transform target)
        {
            GameObject playerRig = null;

            if (PlatformManager.Instance != null && PlatformManager.Instance.ActiveRig != null)
            {
                playerRig = PlatformManager.Instance.ActiveRig;
            }
            else
            {
                var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (xrOrigin != null) playerRig = xrOrigin.gameObject;
            }

            if (playerRig != null)
            {
                // --- SOLUCIÓN: Desactivar CharacterController antes de teletransportar ---
                CharacterController cc = playerRig.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                // Mover al jugador
                playerRig.transform.position = target.position;

                // Rotar al jugador (solo eje Y)
                Vector3 rotacionDestino = new Vector3(0, target.rotation.eulerAngles.y, 0);
                playerRig.transform.rotation = Quaternion.Euler(rotacionDestino);

                // Sincronizar Físicas
                Physics.SyncTransforms();

                // --- SOLUCIÓN: Volver a activar el CharacterController ---
                if (cc != null) cc.enabled = true;

                Debug.Log($"Viaje de retorno completado hacia: {target.name}");
            }
        }
    }
}