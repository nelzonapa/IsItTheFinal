using ImmersiveGraph.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace ImmersiveGraph.Interaction
{
    // Cambiamos XRGrabInteractable por XRSimpleInteractable. 
    // Esto asegura que la base reciba interacción pero sea inamovible.
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
    public class InfiniteStackItem : MonoBehaviour
    {
        [Header("Prefab a generar")]
        [Tooltip("Prefab que se generará (Este SÍ debe tener XRGrabInteractable en el inspector)")]
        public GameObject prefabToSpawn;

        [Header("Configuración de Aparición")]
        [Tooltip("Distancia hacia arriba (en el eje Y) donde aparecerá el nuevo Post-it para evitar choques")]
        public float spawnOffsetY = 0.15f;

        [Tooltip("Segundos de espera entre cada creación para evitar generar demasiados por accidente")]
        public float cooldownTime = 0.5f;

        private float _lastSpawnTime = 0f;

        void Start()
        {
            // Obtenemos el componente Simple Interactable
            var simpleInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

            // Escuchamos el evento de cuando el usuario presiona el gatillo sobre el dispensador
            simpleInteractable.selectEntered.AddListener(OnDispensePostIt);
        }

        void OnDispensePostIt(SelectEnterEventArgs args)
        {
            // 1. Validar el tiempo de recarga (Evita spam de objetos)
            if (Time.time - _lastSpawnTime < cooldownTime) return;
            _lastSpawnTime = Time.time;

            if (prefabToSpawn != null)
            {
                // 2. Calcular la nueva posición sumando el Offset hacia arriba
                Vector3 spawnPosition = transform.position + (Vector3.up * spawnOffsetY);

                // 3. Crear el nuevo Post-it en el aire
                GameObject newPostIt = Instantiate(prefabToSpawn, spawnPosition, transform.rotation);
                newPostIt.name = prefabToSpawn.name;

                // 4. Pintar el NUEVO post-it con el color del usuario.
                // Lo aplicamos al nuevo porque la base dispensadora debería mantenerse estática/neutra.
                Renderer repRenderer = newPostIt.GetComponentInChildren<Renderer>();
                if (repRenderer != null)
                {
                    repRenderer.material.color = UserColorPalette.GetLocalPlayerColor();
                }
            }
            else
            {
                Debug.LogWarning("InfiniteStackItem: No se asignó prefabToSpawn en el Inspector.");
            }
        }
    }
}