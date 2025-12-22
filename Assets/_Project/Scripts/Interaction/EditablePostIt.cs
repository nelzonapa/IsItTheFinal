using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

namespace ImmersiveGraph.Interaction
{
    public class EditablePostIt : MonoBehaviour, ISelectHandler, IPointerClickHandler
    {
        [Header("Referencias UI")]
        public TMP_InputField inputField;
        public RectTransform canvasRect;
        public Transform physicalBackground;

        [Header("Configuración Eje Z")]
        public float minLength = 0.2f;
        public float padding = 0.05f;
        public float textToWorldScale = 0.001f;

        private TouchScreenKeyboard _keyboard;
        private Vector3 _initialScale;

        void Start()
        {
            if (physicalBackground != null)
                _initialScale = physicalBackground.localScale;

            if (inputField != null)
            {
                // CRUCIAL: Decirle al InputField que NO oculte el teclado soft
                inputField.shouldHideMobileInput = false;
                inputField.onValueChanged.AddListener(OnTextChanged);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // --- DEBUG NUEVO ---
            Debug.LogError("¡CLICK RECIBIDO EN EL POST-IT! INTENTANDO ABRIR TECLADO...");
            // -------------------

            // Lanzamos corrutina para asegurar el foco
            StartCoroutine(OpenKeyboardRoutine());
        }

        public void OnSelect(BaseEventData eventData)
        {
            StartCoroutine(OpenKeyboardRoutine());
        }

        IEnumerator OpenKeyboardRoutine()
        {
            yield return null; // Esperar un frame

            // Si ya está abierto, no hacer nada
            if (_keyboard != null && _keyboard.active) yield break;

            Debug.Log("Abriendo Teclado Quest...");

            // Abrir teclado del sistema
            _keyboard = TouchScreenKeyboard.Open(inputField.text, TouchScreenKeyboardType.Default);
        }

        void Update()
        {
            if (_keyboard != null && _keyboard.status == TouchScreenKeyboard.Status.Visible)
            {
                if (inputField.text != _keyboard.text)
                {
                    inputField.text = _keyboard.text;
                }
            }
            // Si el teclado se cerró (Done/Cancel), liberamos el foco
            else if (_keyboard != null && (_keyboard.status == TouchScreenKeyboard.Status.Done || _keyboard.status == TouchScreenKeyboard.Status.Canceled))
            {
                _keyboard = null;
            }
        }

        // ... (Tu función OnTextChanged SE MANTIENE IGUAL que la que me pasaste) ...
        void OnTextChanged(string text)
        {
            if (physicalBackground == null || canvasRect == null) return;

            Canvas.ForceUpdateCanvases();
            float textUIHeight = inputField.textComponent.preferredHeight;
            float worldHeight = textUIHeight * canvasRect.localScale.y;
            float targetZ = Mathf.Max(minLength, worldHeight + padding);
            Vector3 newScale = physicalBackground.localScale;
            float oldZ = newScale.z;
            newScale.z = targetZ;
            physicalBackground.localScale = newScale;
            float difference = targetZ - oldZ;
            physicalBackground.localPosition -= new Vector3(0, 0, difference * 0.5f);
        }
    }
}