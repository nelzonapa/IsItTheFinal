using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;
using UnityEngine.Networking;
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
        public Image fileImageViewer;
        public GameObject imageLoadingSpinner;

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
            // 1. Datos Comunes
            titleText.text = data.title;
            typeLabel.text = data.type.ToUpper();

            // Lógica para mostrar Keywords o Resumen
            if (data.type == "community" && !string.IsNullOrEmpty(data.keywords))
            {
                // Agregamos las keywords al resumen visualmente
                summaryText.text = $"<color=yellow>Keywords:</color> {data.keywords}\n\n{data.summary}";
            }
            else
            {
                summaryText.text = data.summary;
            }

            // 2. Apagar todo
            rootPanel.SetActive(false);
            communityPanel.SetActive(false);
            filePanel.SetActive(false);

            // 3. Activar según tipo
            switch (data.type)
            {
                case "root":
                    ShowRootDetails(data);
                    break;
                case "community":
                    ShowCommunityDetails(data);
                    break;
                case "file":
                    ShowFileDetails(data);
                    break;
            }

            // --- METRICA ---
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
                // CORRECCIÓN: Usamos entidades_frecuentes (nuevo nombre en GraphData)
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

            // --- MOSTRAR METADATOS EXTRA (Source, Date, Entidades) ---
            // Como no tienes TextMeshPro dedicados para Source y Date, los agregamos al bloque de Riesgo o Título

            string metaInfo = "";
            if (data.data != null)
            {
                if (!string.IsNullOrEmpty(data.data.source)) metaInfo += $"Fuente: {data.data.source} | ";
                if (!string.IsNullOrEmpty(data.data.date)) metaInfo += $"Fecha: {data.data.date}";
            }

            // Riesgo + Metadatos
            fileRiskText.text = $"Riesgo: {data.risk_level ?? "N/A"}\n<size=70%>{metaInfo}</size>";

            if (data.risk_level == "Alto") fileRiskText.color = Color.red;
            else if (data.risk_level == "Medio") fileRiskText.color = Color.yellow;
            else fileRiskText.color = Color.green;

            // --- MOSTRAR TEXTO COMPLETO ---
            if (data.data != null)
            {
                // CORRECCIÓN: Usamos texto_full (nuevo nombre en GraphData)
                string fullContent = data.data.texto_full;

                // Si el archivo tiene Entidades (Nivel nodo), las mostramos al inicio del texto
                if (data.entidades != null && data.entidades.Length > 0)
                {
                    string entStr = string.Join(", ", data.entidades);
                    fullContent = $"<color=#ADD8E6><b>Entidades Mencionadas:</b> {entStr}</color>\n\n" + fullContent;
                }

                fileFullText.text = fullContent;

                Canvas.ForceUpdateCanvases();
                var selectable = fileFullText.GetComponent<Interaction.SelectableText>();
                if (selectable != null) selectable.UpdateOriginalText();

                // Imágenes
                if (data.data.images != null && data.data.images.Length > 0)
                {
                    StartCoroutine(LoadImageFromDisk(data.data.images[0]));
                }
                else
                {
                    fileImageViewer.sprite = null;
                    fileImageViewer.color = new Color(0, 0, 0, 0); // Transparente si no hay imagen
                }
            }
            UpdateSelectableContext(filePanel, data.id);
        }

        IEnumerator LoadImageFromDisk(string jsonPath)
        {
            if (imageLoadingSpinner != null) imageLoadingSpinner.SetActive(true);

            string fileName = Path.GetFileName(jsonPath);
            // Lógica de carpetas ajustada a tu estructura probable
            string folderName = "Images_Processed";
            if (jsonPath.Contains("News")) folderName = "News_Cleaned";
            else if (jsonPath.Contains("Blogs")) folderName = "Blogs_Cleaned";
            else if (jsonPath.Contains("Databases")) folderName = "Databases_Cleaned";

            string localPath = Path.Combine(Application.streamingAssetsPath, folderName, fileName);
            string url = "file://" + localPath;

            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                    if (texture != null)
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        fileImageViewer.sprite = sprite;
                        fileImageViewer.color = Color.white;
                    }
                }
                else
                {
                    fileImageViewer.color = new Color(0, 0, 0, 0.2f); // Gris oscuro si falla
                }
            }
            if (imageLoadingSpinner != null) imageLoadingSpinner.SetActive(false);
        }
    }
}