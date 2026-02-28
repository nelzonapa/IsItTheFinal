using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ImmersiveGraph.Collaboration
{
    public class CollabDashboardManager : MonoBehaviour
    {
        [Header("Referencias de la Interfaz")]
        public RectTransform mapCenter; // El objeto central dentro de la máscara
        public TextMeshProUGUI statusText;
        public Image headerPanel;

        [Header("Prefabs 2D")]
        public GameObject uiNodePrefab;
        public GameObject uiLinePrefab;

        [Header("Configuración de Alertas")]
        public Color normalHeaderColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public Color alertHeaderColor = new Color(0.8f, 0.2f, 0.1f, 1f); // Rojo oscuro

        // Preparado para la FASE 2 y 3
        private bool _isSomeoneInCollab = false;

        void Start()
        {
            if (mapCenter == null) Debug.LogError("[Dashboard] Falta asignar el MapCenter.");
            ResetDashboard();
        }

        // Esta función la llamaremos más adelante desde el sistema de red
        public void SetCollabAlertState(bool isOccupied, string playerName = "")
        {
            _isSomeoneInCollab = isOccupied;

            if (_isSomeoneInCollab)
            {
                headerPanel.color = alertHeaderColor;
                statusText.text = $"MODIFICANDO: {playerName} está en la zona compartida";
                statusText.color = Color.white;
            }
            else
            {
                ResetDashboard();
            }
        }

        private void ResetDashboard()
        {
            headerPanel.color = normalHeaderColor;
            statusText.text = "Espacio Colaborativo: Despejado";
            statusText.color = Color.green;
        }

        // --- MÉTODOS PENDIENTES PARA LA FASE 2 (MATEMÁTICA Y ESCALADO) ---
        public void Update2DMap()
        {
            // Aquí irá el algoritmo Bounding Box
        }
    }
}