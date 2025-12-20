using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions; // Para limpiar etiquetas HTML

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class SelectableText : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Configuración")]
        public Color highlightColor = Color.red;

        private TextMeshProUGUI _tmp;
        private string _originalText; // Texto puro sin colores

        // Estado
        private int _startIndex = -1;
        private int _currentIndex = -1;
        private bool _isSelecting = false;

        private void Awake()
        {
            _tmp = GetComponent<TextMeshProUGUI>();
            // Inicialización segura
            UpdateOriginalText();
        }

        // Llamar esto cuando se cambia el contenido del texto desde fuera
        public void UpdateOriginalText()
        {
            if (_tmp == null) _tmp = GetComponent<TextMeshProUGUI>();

            // Guardamos el texto. Si ya tiene colores, se los quitamos para tener la versión limpia.
            _originalText = CleanTags(_tmp.text);

            _startIndex = -1;
            _currentIndex = -1;
            _isSelecting = false;
        }

        // Función para limpiar etiquetas <color> viejas si las hubiera
        private string CleanTags(string input)
        {
            // Expresión regular para quitar tags de unity rich text
            return Regex.Replace(input, "<.*?>", string.Empty);
        }

        private int GetCharIndex(PointerEventData eventData)
        {
            return TMP_TextUtilities.FindIntersectingCharacter(_tmp, eventData.position, Camera.main, true);
        }

        // 1. CLICK
        public void OnPointerDown(PointerEventData eventData)
        {
            // AUTOSYNC: Si el texto en pantalla es diferente al que recuerdo (por longitud), me actualizo
            // Usamos una comparación simple de longitud para ser rápidos
            if (CleanTags(_tmp.text).Length != _originalText.Length)
            {
                Debug.LogWarning("[SelectableText] Detecté cambio de texto no notificado. Actualizando...");
                UpdateOriginalText();
            }

            int index = GetCharIndex(eventData);

            if (index != -1)
            {
                // PROTECCIÓN: Si el índice es mayor que el texto (raro pero posible en TMP), cortamos
                if (index >= _originalText.Length) index = _originalText.Length - 1;

                _startIndex = index;
                _currentIndex = index;
                _isSelecting = true;

                Debug.Log($"[SelectableText] Click en letra: {index} (Texto len: {_originalText.Length})");
                ApplyVisualHighlight(_startIndex, _currentIndex);
            }
        }

        // 2. ARRASTRAR
        public void OnDrag(PointerEventData eventData)
        {
            if (!_isSelecting) return;

            int index = GetCharIndex(eventData);

            // Solo actualizamos si el índice es válido y diferente al anterior
            if (index != -1 && index != _currentIndex)
            {
                // PROTECCIÓN
                if (index >= _originalText.Length) index = _originalText.Length - 1;

                _currentIndex = index;
                ApplyVisualHighlight(_startIndex, _currentIndex);
            }
        }

        // 3. SOLTAR
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isSelecting) return;
            _isSelecting = false;

            if (_startIndex != -1 && _currentIndex != -1)
            {
                int start = Mathf.Min(_startIndex, _currentIndex);
                int end = Mathf.Max(_startIndex, _currentIndex);

                // PROTECCIÓN FINAL PARA EXTRACCIÓN
                if (start < _originalText.Length && end < _originalText.Length)
                {
                    string selectedString = _originalText.Substring(start, (end - start) + 1);
                    Debug.Log($"SELECCIÓN TERMINADA: '{selectedString}'");
                    // Aquí llamaremos a CreateToken(selectedString)
                }
            }
        }

        // PINTAR (Con Try-Catch para que nunca rompa el juego)
        void ApplyVisualHighlight(int start, int end)
        {
            try
            {
                int min = Mathf.Min(start, end);
                int max = Mathf.Max(start, end);

                // 1. Validación de Límites Estricta
                if (min < 0) min = 0;
                if (max >= _originalText.Length) max = _originalText.Length - 1;
                if (min > max) return; // Nada que pintar

                System.Text.StringBuilder sb = new System.Text.StringBuilder(_originalText);

                // 2. Insertamos etiquetas de color
                // Etiqueta de cierre </color>
                // Insertamos DESPUÉS del carácter 'max'
                if (max + 1 < sb.Length)
                    sb.Insert(max + 1, "</color>");
                else
                    sb.Append("</color>");

                // Etiqueta de apertura
                sb.Insert(min, $"<color=#{ColorUtility.ToHtmlStringRGB(highlightColor)}>");

                _tmp.text = sb.ToString();
            }
            catch (System.Exception e)
            {
                // Si falla el pintado, no crasheamos el juego, solo avisamos
                Debug.LogError($"Error pintando texto: {e.Message}");
                // Restauramos texto limpio por si acaso
                _tmp.text = _originalText;
            }
        }
    }
}