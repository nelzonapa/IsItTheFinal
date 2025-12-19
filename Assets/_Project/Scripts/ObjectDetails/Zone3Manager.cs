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
            summaryText.text = data.summary;
            typeLabel.text = data.type.ToUpper();

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
        }

        void ShowRootDetails(NodeData data)
        {
            rootPanel.SetActive(true);

            // Usamos data.details directamente
            if (data.details != null)
            {
                string focosStr = (data.details.focos != null) ? string.Join("\n• ", data.details.focos) : "Ninguno";
                rootFocosText.text = "Focos: \n• " + focosStr;

                rootConclusionText.text = "Conclusión: \n" + data.details.conclusion ?? "Sin conclusión";
            }
        }

        void ShowCommunityDetails(NodeData data)
        {
            communityPanel.SetActive(true);

            if (data.details != null)
            {
                // Entidades
                string entStr = (data.details.entidades != null) ? string.Join(", ", data.details.entidades) : "-";
                commEntidadesText.text = "Entidades: \n" + entStr;

                // Fechas (Ahora es string directo, no array)
                commFechasText.text = "Fechas: \n" +  data.details.fechas ?? "-";

                // Amenaza
                commAmenazaText.text = "Nivel posible de amenaza: \n" + data.details.amenaza ?? "Desconocida";

                // Color condicional
                if (commAmenazaText.text == "Alta") commAmenazaText.color = Color.red;
                else if (commAmenazaText.text == "Medio") commAmenazaText.color = Color.yellow;
                else commAmenazaText.color = Color.white;
            }
        }

        void ShowFileDetails(NodeData data)
        {
            filePanel.SetActive(true);

            fileRiskText.text = "Riesgo: " + (data.risk_level ?? "N/A");

            // Color Riesgo
            if (data.risk_level == "Alto") fileRiskText.color = Color.red;
            else if (data.risk_level == "Medio") fileRiskText.color = Color.yellow;
            else fileRiskText.color = Color.green;

            if (data.data != null)
            {
                fileFullText.text = data.data.full_text;

                if (data.data.images != null && data.data.images.Length > 0)
                {
                    StartCoroutine(LoadImageFromDisk(data.data.images[0]));
                }
                else
                {
                    fileImageViewer.sprite = null;
                    fileImageViewer.color = Color.red;
                }
            }
        }

        IEnumerator LoadImageFromDisk(string jsonPath)
        {
            if (imageLoadingSpinner != null) imageLoadingSpinner.SetActive(true);

            string fileName = Path.GetFileName(jsonPath);
            string folderName = "Images_Processed";

            // Detección simple de carpeta
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
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    fileImageViewer.sprite = sprite;
                    fileImageViewer.color = Color.white;
                }
                else
                {
                    // Si falla, quizás la imagen no está o la ruta falló. 
                    // Debug.LogWarning("Imagen no encontrada: " + url);
                    fileImageViewer.color = new Color(0, 0, 0, 0.5f);
                }
            }

            if (imageLoadingSpinner != null) imageLoadingSpinner.SetActive(false);
        }
    }
}