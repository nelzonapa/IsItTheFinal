using UnityEngine;
using Fusion;
using ImmersiveGraph.Network; // Para acceder a NetworkTokenSync, etc.

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class NetworkTrashZone : NetworkBehaviour
    {
        // Colores de feedback visual (opcional)
        private Renderer _renderer;
        private Color _normalColor;
        public Color deleteColor = Color.red;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null) _normalColor = _renderer.material.color;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Buscamos si el objeto que entró tiene un NetworkObject
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();

            if (netObj != null)
            {
                // Verificamos si es algo "borrable" (Token o Post-It)
                bool isToken = netObj.GetComponent<NetworkTokenSync>() != null;
                bool isPostIt = netObj.GetComponent<NetworkPostItSync>() != null;

                if (isToken || isPostIt)
                {
                    Debug.Log($"[NetworkTrash] Eliminando objeto: {netObj.name}");

                    // IMPORTANTE: En Fusion, solo la Autoridad de Estado (quien lo tiene agarrado o lo creó)
                    // debería pedir el Despawn. Como el usuario lo está agarrando para meterlo
                    // a la basura, él tiene la autoridad.
                    if (netObj.HasStateAuthority)
                    {
                        Runner.Despawn(netObj);

                        // Feedback visual breve
                        if (_renderer != null) StartCoroutine(FlashColor());
                    }
                    else
                    {
                        // Si por alguna razón extraña no tiene autoridad (ej. lo empujó con física),
                        // forzamos la destrucción si somos el Host/Servidor.
                        if (Runner.IsServer) Runner.Despawn(netObj);
                    }
                }
            }
        }

        System.Collections.IEnumerator FlashColor()
        {
            _renderer.material.color = deleteColor;
            yield return new WaitForSeconds(0.2f);
            _renderer.material.color = _normalColor;
        }
    }
}