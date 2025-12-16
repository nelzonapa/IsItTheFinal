using Fusion;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ImmersiveGraph.Network
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class TeleportToGroup : MonoBehaviour
    {
        private XROrigin _xrOrigin;
        private NetworkRunner _runner;

        void Start()
        {
            _xrOrigin = FindFirstObjectByType<XROrigin>();
            var interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnButtonPressed);
        }

        public void OnButtonPressed(SelectEnterEventArgs args)
        {
            if (_runner == null) _runner = FindFirstObjectByType<NetworkRunner>();

            if (GroupTableManager.Instance == null || _runner == null)
            {
                Debug.LogError("Error: Falta Manager o Conexión.");
                return;
            }

            Debug.Log("Teletransportando al punto de spawn asignado...");

            // 1. Pedir el punto exacto al Manager
            Transform targetSpawn = GroupTableManager.Instance.GetSpawnPointForPlayer(_runner.LocalPlayer);

            if (_xrOrigin != null)
            {
                // 2. Moverse a la posición del SpawnPoint
                // TRUCO: Sumamos 0.05 en Y para asegurar que no quedes enterrado en el suelo
                _xrOrigin.transform.position = targetSpawn.position + new Vector3(0, 0.05f, 0);

                // 3. Copiar la rotación (para que mires hacia donde mira el SpawnPoint)
                // Solo rotamos en Y para no inclinar la cámara si el punto está chueco
                Vector3 targetRotation = targetSpawn.rotation.eulerAngles;
                _xrOrigin.transform.rotation = Quaternion.Euler(0, targetRotation.y, 0);
            }
        }
    }
}