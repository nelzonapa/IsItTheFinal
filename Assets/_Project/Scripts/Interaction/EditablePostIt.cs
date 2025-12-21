using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

namespace ImmersiveGraph.Interaction
{
    public class EditablePostIt : MonoBehaviour, ISelectHandler, IPointerClickHandler
    {
        [Header("Referencias UI")]
        public TMP_InputField inputField;
        public RectTransform canvasRect;
        public Transform physicalBackground; // El Cubo

        [Header("Configuración Eje Z")]
        public float minLength = 0.2f;   // Largo mínimo en Z
        public float padding = 0.05f;    // Margen extra al final
        public float textToWorldScale = 0.001f; // Factor de conversión (ajústalo a ojo)

        private TouchScreenKeyboard _keyboard;
        private Vector3 _initialScale;
        private Vector3 _initialPosition;

        void Start()
        {
            if (physicalBackground != null)
            {
                _initialScale = physicalBackground.localScale;
                _initialPosition = physicalBackground.localPosition;
            }

            if (inputField != null)
            {
                // Configuración para forzar teclado
                inputField.shouldHideMobileInput = false;
                inputField.onValueChanged.AddListener(OnTextChanged);
            }
        }

        // --- FUERZA BRUTA PARA EL TECLADO ---
        public void OnPointerClick(PointerEventData eventData)
        {
            OpenKeyboard();
        }

        public void OnSelect(BaseEventData eventData)
        {
            OpenKeyboard();
        }

        void OpenKeyboard()
        {
            // Esto le grita a Android/Oculus que abra el teclado
            if (_keyboard == null || !_keyboard.active)
            {
                // TouchScreenKeyboardType.MultiLine NO existe — hay que pasar el enum y el bool multiline = true
                _keyboard = TouchScreenKeyboard.Open(
                    inputField.text,
                    TouchScreenKeyboardType.Default,  // tipo de teclado válido
                    /*autocorrection*/ false,
                    /*multiline*/ true,
                    /*secure*/ false,
                    /*alert*/ false,
                    /*textPlaceholder*/ ""
                );

                Debug.Log("Intentando forzar teclado VR...");
            }
        }

        // ------------------------------------

        void Update()
        {
            // Sincronización bidireccional Teclado <-> InputField
            if (_keyboard != null && _keyboard.status == TouchScreenKeyboard.Status.Visible)
            {
                if (inputField.text != _keyboard.text)
                {
                    inputField.text = _keyboard.text;
                }
            }
        }

        void OnTextChanged(string text)
        {
            if (physicalBackground == null || canvasRect == null) return;

            Canvas.ForceUpdateCanvases();

            // 1. Obtener altura del texto (wrapping automático)
            float textUIHeight = inputField.textComponent.preferredHeight;

            // 2. Convertir a metros (World Space)
            // Usamos la escala del Canvas en Y porque el texto crece en Y local del Canvas
            float worldHeight = textUIHeight * canvasRect.localScale.y;

            // 3. Calcular nuevo tamaño en Z (Largo del PostIt)
            float targetZ = Mathf.Max(minLength, worldHeight + padding);

            // 4. Aplicar Escala en Z
            Vector3 newScale = physicalBackground.localScale;
            float oldZ = newScale.z;
            newScale.z = targetZ;
            physicalBackground.localScale = newScale;

            // 5. CORRECCIÓN DE POSICIÓN (El truco del Pivote)
            // Como el cubo crece desde el centro, al crecer hacia abajo (Z-), 
            // también crece hacia arriba (Z+). Hay que moverlo para compensar.
            // Asumiendo que el PostIt mira hacia Y+ y crece hacia Z- (o Z+ según tu modelo)

            float difference = targetZ - oldZ;

            // Movemos el centro hacia "abajo" la mitad de lo que creció
            // Nota: Si crece hacia el lado incorrecto, cambia el signo '-' por '+'
            physicalBackground.localPosition -= new Vector3(0, 0, difference * 0.5f);
        }
    }
}