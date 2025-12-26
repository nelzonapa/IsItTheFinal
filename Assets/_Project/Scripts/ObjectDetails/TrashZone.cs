using UnityEngine;
using System.Collections; // Necesario para Corrutinas

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(AudioSource))] // <--- Nuevo
    public class TrashZone : MonoBehaviour
    {
        [Header("Feedback")]
        public AudioClip deleteSound; // Arrastra el sonido aquí
        public Color flashColor = Color.red; // Color del parpadeo
        public float flashDuration = 0.2f;

        private AudioSource _audioSource;
        private Renderer _renderer;
        private Color _originalColor;

        private void Start()
        {
            _audioSource = GetComponent<AudioSource>();
            _renderer = GetComponent<Renderer>();
            if (_renderer != null) _originalColor = _renderer.material.color;
        }

        private void OnTriggerEnter(Collider other)
        {
            bool itemDeleted = false;

            // CASO 1: TOKENS
            ExtractedToken token = other.GetComponentInParent<ExtractedToken>();
            if (token != null)
            {
                token.DestroyAndRevert();
                itemDeleted = true;
            }

            // CASO 2: POST-ITS
            if (!itemDeleted)
            {
                EditablePostIt postIt = other.GetComponentInParent<EditablePostIt>();
                if (postIt != null)
                {
                    Destroy(postIt.gameObject);
                    itemDeleted = true;
                }
            }

            // CASO 3: LÍNEAS DE CONEXIÓN (El Handle rojo)
            if (!itemDeleted && other.name == "DeleteHandle")
            {
                // Si el usuario mete el cubito rojo de la línea a la basura
                Destroy(other.transform.parent.gameObject); // Borramos la línea completa (padre del handle)
                itemDeleted = true;
            }

            // --- FEEDBACK ---
            if (itemDeleted)
            {
                Debug.Log("Objeto eliminado en TrashZone.");
                PlayFeedback();
            }
        }

        void PlayFeedback()
        {
            // Sonido
            if (_audioSource != null && deleteSound != null)
            {
                _audioSource.PlayOneShot(deleteSound);
            }

            // Parpadeo Visual
            if (_renderer != null)
            {
                StopAllCoroutines();
                StartCoroutine(FlashRoutine());
            }
        }

        IEnumerator FlashRoutine()
        {
            _renderer.material.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            _renderer.material.color = _originalColor;
        }
    }
}