using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq; // Para ordenar listas

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class SelectableText : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Configuración")]
        public Color highlightColor = Color.red;
        public GameObject tokenPrefab; // <--- ARRASTRA AQUÍ TU PREFAB DE LA FICHA

        // Clase interna para guardar datos de cada subrayado
        [System.Serializable]
        private class HighlightData
        {
            public string id;
            public int start;
            public int end;
        }

        private TextMeshProUGUI _tmp;
        private string _cleanText; // Texto virgen sin colores
        private List<HighlightData> _activeHighlights = new List<HighlightData>();

        // Estado de selección actual (temporal mientras arrastras)
        private int _currentDragStart = -1;
        private int _currentDragEnd = -1;
        private bool _isSelecting = false;

        private void Awake()
        {
            _tmp = GetComponent<TextMeshProUGUI>();
            UpdateOriginalText();
        }

        public void UpdateOriginalText()
        {
            if (_tmp == null) _tmp = GetComponent<TextMeshProUGUI>();

            // Limpiamos etiquetas viejas para tener la base limpia
            _cleanText = System.Text.RegularExpressions.Regex.Replace(_tmp.text, "<.*?>", string.Empty);
            _activeHighlights.Clear();
            _tmp.text = _cleanText;
        }

        // --- SISTEMA DE VISUALIZACIÓN ---
        // Esta función reconstruye todo el texto con TODOS los resaltados activos + el que estás arrastrando
        private void RefreshVisuals()
        {
            // 1. Hacemos una lista temporal con los ya confirmados
            List<HighlightData> allHighlights = new List<HighlightData>(_activeHighlights);

            // 2. Si estamos arrastrando, agregamos el temporal
            if (_isSelecting && _currentDragStart != -1 && _currentDragEnd != -1)
            {
                allHighlights.Add(new HighlightData
                {
                    start = Mathf.Min(_currentDragStart, _currentDragEnd),
                    end = Mathf.Max(_currentDragStart, _currentDragEnd)
                });
            }

            // 3. IMPORTANTE: Ordenar de ATRÁS hacia ADELANTE (descendente)
            // Si insertamos etiquetas al principio, los índices del final se corren.
            // Al hacerlo desde el final, los índices del principio siguen siendo válidos.
            allHighlights = allHighlights.OrderByDescending(h => h.start).ToList();

            System.Text.StringBuilder sb = new System.Text.StringBuilder(_cleanText);
            string colorHex = ColorUtility.ToHtmlStringRGB(highlightColor);

            foreach (var h in allHighlights)
            {
                // Validar rangos
                if (h.start < 0 || h.end >= _cleanText.Length) continue;

                // Insertar cierre </color>
                if (h.end + 1 < sb.Length) sb.Insert(h.end + 1, "</color>");
                else sb.Append("</color>");

                // Insertar apertura <color>
                sb.Insert(h.start, $"<color=#{colorHex}>");
            }

            _tmp.text = sb.ToString();
        }

        // --- GESTIÓN PÚBLICA (Para TrashZone) ---
        public void RemoveHighlight(string id)
        {
            var item = _activeHighlights.Find(h => h.id == id);
            if (item != null)
            {
                _activeHighlights.Remove(item);
                RefreshVisuals(); // Repintar sin el rojo eliminado
                Debug.Log($"Highlight {id} eliminado y texto restaurado.");
            }
        }

        // --- INTERACCIÓN VR MEJORADA ---
        private int GetCharIndex(PointerEventData eventData)
        {
            // MEJORA: En lugar de Camera.main, usamos la cámara que generó el evento.
            // En XR Toolkit, esto suele ser la cámara asociada al Canvas o al Controller.
            Camera targetCamera = eventData.enterEventCamera;

            if (targetCamera == null) targetCamera = Camera.main;

            return TMP_TextUtilities.FindIntersectingCharacter(_tmp, eventData.position, targetCamera, true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            int index = GetCharIndex(eventData);
            if (index != -1 && index < _cleanText.Length)
            {
                _currentDragStart = index;
                _currentDragEnd = index;
                _isSelecting = true;
                RefreshVisuals();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isSelecting) return;
            int index = GetCharIndex(eventData);

            if (index != -1 && index != _currentDragEnd && index < _cleanText.Length)
            {
                _currentDragEnd = index;
                RefreshVisuals();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isSelecting) return;
            _isSelecting = false;

            // Confirmar selección
            if (_currentDragStart != -1 && _currentDragEnd != -1)
            {
                int start = Mathf.Min(_currentDragStart, _currentDragEnd);
                int end = Mathf.Max(_currentDragStart, _currentDragEnd);
                int length = (end - start) + 1;

                // Evitar clicks vacíos o errores
                if (length > 0 && start + length <= _cleanText.Length)
                {
                    string selectedText = _cleanText.Substring(start, length);

                    // 1. Guardar Highlight Confirmado
                    string newID = System.Guid.NewGuid().ToString(); // ID Único
                    _activeHighlights.Add(new HighlightData { id = newID, start = start, end = end });

                    // 2. Crear Token Físico
                    SpawnToken(selectedText, newID, eventData);
                }
            }

            // Limpiamos variables temporales
            _currentDragStart = -1;
            _currentDragEnd = -1;
            RefreshVisuals(); // Repintar final con el nuevo guardado
        }

        void SpawnToken(string text, string id, PointerEventData eventData)
        {
            if (tokenPrefab == null) return;

            // Instanciar donde ocurrió el evento (cerca de la mano/puntero)
            // eventData.pointerCurrentRaycast.worldPosition es el punto exacto de impacto en el UI 3D
            Vector3 spawnPos = eventData.pointerCurrentRaycast.worldPosition;

            // Lo movemos un poquito hacia la cámara para que no nazca dentro del papel
            spawnPos += (Camera.main.transform.position - spawnPos).normalized * 0.1f;

            GameObject token = Instantiate(tokenPrefab, spawnPos, Quaternion.identity);

            // Configurar el token
            var tokenScript = token.GetComponent<ExtractedToken>();
            if (tokenScript != null)
            {
                tokenScript.SetupToken(text, this, id);
            }
        }
    }
}