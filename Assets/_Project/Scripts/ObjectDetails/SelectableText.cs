using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;
using Fusion; 
using ImmersiveGraph.Network; 

namespace ImmersiveGraph.Interaction
{
    [RequireComponent(typeof(TextMeshProUGUI))]

    [RequireComponent(typeof(AudioSource))] // para audio
    public class SelectableText : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Configuración Local")]
        public Color highlightColor = Color.yellow; // Amarillo se ve mejor en VR
        public GameObject tokenPrefab; // Prefab LOCAL (ExtractedToken)

        [Header("Configuración Red (Fase 3)")]
        public bool isNetworkMode = false; // ¿Estoy en el panel flotante?
        public NetworkObject netTokenPrefab; // Prefab de RED (Network_Token)

        [Header("Audio Feedback")] // 
        public AudioClip highlightLoopSound; // Sonido continuo (tipo lápiz escribiendo o zumbido suave)
        public AudioClip tokenSpawnSound;    // Sonido "Pop" o "Ding" de éxito

        // El ID del nodo origen (se llena automáticamente por Zone3Manager o NetworkDocViewer)
        public string currentContextNodeID = "";

        // Clase interna
        [System.Serializable]
        private class HighlightData
        {
            public string id;
            public int start;
            public int end;
        }

        private TextMeshProUGUI _tmp;
        private string _cleanText;
        private List<HighlightData> _activeHighlights = new List<HighlightData>();

        private int _currentDragStart = -1;
        private int _currentDragEnd = -1;
        private bool _isSelecting = false;


        private AudioSource _audioSource; // audiooo

        private void Awake()
        {
            _tmp = GetComponent<TextMeshProUGUI>();
            _audioSource = GetComponent<AudioSource>(); // Inicializar audio
            _tmp.ForceMeshUpdate(); // Vital para el cálculo geométrico
            UpdateOriginalText();
        }

        public void UpdateOriginalText()
        {
            if (_tmp == null) _tmp = GetComponent<TextMeshProUGUI>();

            _cleanText = System.Text.RegularExpressions.Regex.Replace(_tmp.text, "<.*?>", string.Empty);
            _activeHighlights.Clear();
            _tmp.text = _cleanText;
            _tmp.ForceMeshUpdate();
        }

        private void RefreshVisuals()
        {
            List<HighlightData> allHighlights = new List<HighlightData>(_activeHighlights);

            if (_isSelecting && _currentDragStart != -1 && _currentDragEnd != -1)
            {
                allHighlights.Add(new HighlightData
                {
                    start = Mathf.Min(_currentDragStart, _currentDragEnd),
                    end = Mathf.Max(_currentDragStart, _currentDragEnd)
                });
            }

            allHighlights = allHighlights.OrderByDescending(h => h.start).ToList();

            System.Text.StringBuilder sb = new System.Text.StringBuilder(_cleanText);
            string colorHex = ColorUtility.ToHtmlStringRGB(highlightColor);

            foreach (var h in allHighlights)
            {
                if (h.start < 0 || h.end >= _cleanText.Length) continue;
                if (h.end + 1 < sb.Length) sb.Insert(h.end + 1, "</color>");
                else sb.Append("</color>");
                sb.Insert(h.start, $"<color=#{colorHex}>");
            }

            _tmp.text = sb.ToString();
        }

        public void RemoveHighlight(string id)
        {
            var item = _activeHighlights.Find(h => h.id == id);
            if (item != null)
            {
                _activeHighlights.Remove(item);
                RefreshVisuals();
            }
        }

        // --- CÁLCULO GEOMÉTRICO ROBUSTO PARA VR (Mantenemos esto porque es lo que funcionó) ---
        private int GetCharIndex(PointerEventData eventData)
        {
            // 1. Punto de impacto 3D
            Vector3 worldPoint = eventData.pointerCurrentRaycast.worldPosition;
            if (worldPoint == Vector3.zero) return -1;

            // 2. Convertir a Local
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

            // 3. Buscar en la malla de texto
            TMP_TextInfo textInfo = _tmp.textInfo;
            if (textInfo == null || textInfo.characterCount == 0) return -1;

            float minDistance = float.MaxValue;
            int closestIndex = -1;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                float minX = charInfo.bottomLeft.x; float maxX = charInfo.topRight.x;
                float minY = charInfo.bottomLeft.y; float maxY = charInfo.topRight.y;
                float padding = 5f; // Margen para dedos gordos

                if (localPoint.x >= minX - padding && localPoint.x <= maxX + padding &&
                    localPoint.y >= minY - padding && localPoint.y <= maxY + padding)
                {
                    return i;
                }

                float dist = Vector3.Distance(localPoint, (charInfo.bottomLeft + charInfo.topRight) / 2);
                if (dist < minDistance) { minDistance = dist; closestIndex = i; }
            }

            if (minDistance < 20f) return closestIndex;
            return -1;
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

                // START AUDIO LOOP
                if (_audioSource != null && highlightLoopSound != null)
                {
                    _audioSource.clip = highlightLoopSound;
                    _audioSource.loop = true;
                    _audioSource.Play();
                }
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

            // STOP AUDIO LOOP SIEMPRE
            if (_audioSource != null)
            {
                _audioSource.Stop();
                _audioSource.loop = false; // Reset
            }


            if (!_isSelecting) return;
            _isSelecting = false;

            if (_currentDragStart != -1 && _currentDragEnd != -1)
            {
                int start = Mathf.Min(_currentDragStart, _currentDragEnd);
                int end = Mathf.Max(_currentDragStart, _currentDragEnd);
                int length = (end - start) + 1;

                if (length > 0 && start + length <= _cleanText.Length)
                {
                    string selectedText = _cleanText.Substring(start, length);
                    string newID = System.Guid.NewGuid().ToString();
                    _activeHighlights.Add(new HighlightData { id = newID, start = start, end = end });

                    // LLAMAMOS AL SPAWN HÍBRIDO
                    SpawnToken(selectedText, newID, eventData);
                }
            }

            _currentDragStart = -1;
            _currentDragEnd = -1;
            RefreshVisuals();
        }

        // --- LÓGICA DE SPAWN FASE 3 (HÍBRIDA) ---
        void SpawnToken(string text, string id, PointerEventData eventData)
        {

            // --- SONIDO DE ÉXITO (POP) ---
            if (_audioSource != null && tokenSpawnSound != null)
            {
                _audioSource.PlayOneShot(tokenSpawnSound);
            }
            // -----------------------------


            // Cálculo de posición visual
            Vector3 spawnPos = eventData.pointerCurrentRaycast.worldPosition;
            if (spawnPos == Vector3.zero) spawnPos = transform.position;
            spawnPos += (Camera.main.transform.position - spawnPos).normalized * 0.1f;

            // --- BIFURCACIÓN ---
            if (isNetworkMode)
            {
                // MODO RED: Estamos en el Panel Flotante
                if (netTokenPrefab != null)
                {
                    // Buscamos el Runner de Fusion
                    NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();

                    if (runner != null && runner.IsRunning)
                    {
                        // 1. Crear Objeto en la Red
                        NetworkObject netObj = runner.Spawn(netTokenPrefab, spawnPos, Quaternion.identity, runner.LocalPlayer);

                        // 2. Inicializar Datos Sincronizados
                        var netSync = netObj.GetComponent<NetworkTokenSync>();
                        if (netSync != null)
                        {
                            // Inyectamos el Texto y el ID del Documento Origen
                            netSync.InitializeToken(text, currentContextNodeID);
                        }

                        Debug.Log("Token de RED creado desde Panel Flotante");
                    }
                }
                else
                {
                    Debug.LogError("Error: Estás en Modo Red pero 'netTokenPrefab' está vacío en SelectableText.");
                }
            }
            else
            {
                // MODO LOCAL: Estamos en SmartDesk (Comportamiento Original)
                if (tokenPrefab != null)
                {
                    GameObject token = Instantiate(tokenPrefab, spawnPos, Quaternion.identity);
                    var tokenScript = token.GetComponent<ExtractedToken>();
                    if (tokenScript != null)
                    {
                        tokenScript.SetupToken(text, this, id, currentContextNodeID);
                    }
                }
            }
        }
    }
}