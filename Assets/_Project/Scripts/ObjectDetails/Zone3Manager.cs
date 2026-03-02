using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using ImmersiveGraph.Data;

namespace ImmersiveGraph.Core
{
    public class Zone3Manager : MonoBehaviour
    {
        [Header("UI Común (Cabecera)")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI summaryText;
        public TextMeshProUGUI typeLabel;

        [Header("Paneles de Contenido")]
        public GameObject rootPanel;
        public GameObject communityPanel;
        public GameObject filePanel;

        [Header("UI Específica: ROOT")]
        public TextMeshProUGUI rootFocosText;
        public TextMeshProUGUI rootConclusionText;

        [Header("UI Específica: COMMUNITY")]
        public TextMeshProUGUI commEntidadesText;
        public TextMeshProUGUI commFechasText;
        public TextMeshProUGUI commAmenazaText;

        [Header("UI Específica: FILE")]
        public TextMeshProUGUI fileFullText;
        public TextMeshProUGUI fileRiskText;
        // SE ELIMINARON LAS REFERENCIAS A IMAGEVIEWER Y SPINNER

        private void Awake()
        {
            ClearZone();
        }

        public void ClearZone()
        {
            if (titleText) titleText.text = "Seleccione un Nodo";
            if (summaryText) summaryText.text = "Mantenga presionado para ver detalles.";
            if (typeLabel) typeLabel.text = "...";

            if (rootPanel) rootPanel.SetActive(false);
            if (communityPanel) communityPanel.SetActive(false);
            if (filePanel) filePanel.SetActive(false);
        }

        public void ShowNodeDetails(NodeData data)
        {
            titleText.text = data.title;
            typeLabel.text = data.type.ToUpper();

            if (data.type == "community" && !string.IsNullOrEmpty(data.keywords))
            {
                summaryText.text = $"<color=yellow>Keywords:</color> {data.keywords}\n\n{data.summary}";
            }
            else
            {
                summaryText.text = data.summary;
            }

            rootPanel.SetActive(false);
            communityPanel.SetActive(false);
            filePanel.SetActive(false);

            switch (data.type)
            {
                case "root": ShowRootDetails(data); break;
                case "community": ShowCommunityDetails(data); break;
                case "file": ShowFileDetails(data); break;
            }

            if (ExperimentDataLogger.Instance != null)
            {
                ExperimentDataLogger.Instance.LogEvent("STRATEGY", "Individual Analysis", $"Reading: {data.title}", Vector3.zero);
            }
        }

        void UpdateSelectableContext(GameObject panel, string nodeID)
        {
            var selectables = panel.GetComponentsInChildren<Interaction.SelectableText>(true);
            foreach (var sel in selectables) sel.currentContextNodeID = nodeID;
        }

        void ShowRootDetails(NodeData data)
        {
            rootPanel.SetActive(true);
            if (data.details != null)
            {
                string focosStr = (data.details.focos != null) ? string.Join("\n• ", data.details.focos) : "Ninguno";
                rootFocosText.text = "Focos: \n• " + focosStr;
                rootConclusionText.text = "Conclusión: \n" + (data.details.conclusion ?? "Sin conclusión");
            }
            UpdateSelectableContext(rootPanel, data.id);
        }

        void ShowCommunityDetails(NodeData data)
        {
            communityPanel.SetActive(true);
            if (data.details != null)
            {
                string entStr = (data.details.entidades_frecuentes != null) ? string.Join(", ", data.details.entidades_frecuentes) : "-";
                commEntidadesText.text = "Entidades Frecuentes: \n" + entStr;
                commFechasText.text = "Fechas: \n" + (data.details.fechas ?? "-");
                commAmenazaText.text = "Amenaza: " + (data.details.amenaza ?? "Desconocida");

                if (commAmenazaText.text.Contains("Alto")) commAmenazaText.color = Color.red;
                else if (commAmenazaText.text.Contains("Medio")) commAmenazaText.color = Color.yellow;
                else commAmenazaText.color = Color.white;
            }
            UpdateSelectableContext(communityPanel, data.id);
        }

        void ShowFileDetails(NodeData data)
        {
            filePanel.SetActive(true);
            string metaInfo = "";

            if (data.data != null)
            {
                if (!string.IsNullOrEmpty(data.data.source)) metaInfo += $"Fuente: {data.data.source} | ";
                if (!string.IsNullOrEmpty(data.data.date)) metaInfo += $"Fecha: {data.data.date}";
            }

            fileRiskText.text = $"Riesgo: {data.risk_level ?? "N/A"}\n<size=70%>{metaInfo}</size>";

            if (data.risk_level == "Alto") fileRiskText.color = Color.red;
            else if (data.risk_level == "Medio") fileRiskText.color = Color.yellow;
            else fileRiskText.color = Color.green;

            if (data.data != null)
            {
                string fullContent = data.data.texto_full;

                if (data.entidades != null && data.entidades.Length > 0)
                {
                    string entStr = string.Join(", ", data.entidades);
                    fullContent = $"<color=#ADD8E6><b>Entidades Mencionadas:</b> {entStr}</color>\n\n" + fullContent;
                }

                fileFullText.text = fullContent;
                Canvas.ForceUpdateCanvases();

                var selectable = fileFullText.GetComponent<Interaction.SelectableText>();
                if (selectable != null) selectable.UpdateOriginalText();

                // SE ELIMINÓ LA CARGA DE IMÁGENES AQUÍ
            }
            UpdateSelectableContext(filePanel, data.id);
        }
    }
}