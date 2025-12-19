using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;
using UnityEngine.Networking; // Para cargar imágenes
using ImmersiveGraph.Data;

namespace ImmersiveGraph.Core
{
    public class Zone3Manager : MonoBehaviour
    {
        //public static Zone3Manager Instance; #destruia mi panel

        [Header("UI Común (Cabecera)")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI summaryText;
        public TextMeshProUGUI typeLabel; // Para decir "CASO", "COMUNIDAD" o "EVIDENCIA"

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
        public TextMeshProUGUI fileFullText; // Texto largo (debe estar en un ScrollView)
        public TextMeshProUGUI fileRiskText;
        public Image fileImageViewer;        // Donde se mostrará la foto
        public GameObject imageLoadingSpinner; // Opcional: Icono de carga

        private void Awake()
        {
            // Singleton simple para que el GraphNode lo encuentre rápido
            //if (Instance == null) Instance = this;
            //else Destroy(gameObject);

            // Ocultar todo al inicio
            ClearZone();
        }

        public void ClearZone()
        {
            titleText.text = "Seleccione un Nodo";
            summaryText.text = "Mantenga presionado un nodo del grafo para ver detalles.";
            typeLabel.text = "...";

            rootPanel.SetActive(false);
            communityPanel.SetActive(false);
            filePanel.SetActive(false);
        }

        // --- FUNCIÓN PRINCIPAL LLAMADA DESDE EL GRAFO ---
        public void ShowNodeDetails(NodeData data)
        {
            // 1. Datos Comunes
            titleText.text = data.title;
            summaryText.text = data.summary;
            typeLabel.text = data.type.ToUpper();

            // 2. Apagar todos los paneles primero
            rootPanel.SetActive(false);
            communityPanel.SetActive(false);
            filePanel.SetActive(false);

            // 3. Activar el panel correcto según tipo
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

        // --- LÓGICA DE DETALLE POR TIPO ---

        void ShowRootDetails(NodeData data)
        {
            rootPanel.SetActive(true);

            if (data.root_details != null)
            {
                // Convertir Array a String con saltos de línea
                string focosStr = (data.root_details.focos != null) ? string.Join("\n• ", data.root_details.focos) : "Ninguno";
                rootFocosText.text = "• " + focosStr;

                rootConclusionText.text = data.root_details.conclusion ?? "Sin conclusión";
            }
        }

        void ShowCommunityDetails(NodeData data)
        {
            communityPanel.SetActive(true);

            if (data.comm_details != null)
            {
                string entStr = (data.comm_details.entidades != null) ? string.Join(", ", data.comm_details.entidades) : "-";
                commEntidadesText.text = entStr;

                string fechasStr = (data.comm_details.fechas != null) ? string.Join(" | ", data.comm_details.fechas) : "-";
                commFechasText.text = fechasStr;

                commAmenazaText.text = data.comm_details.amenaza ?? "Desconocida";

                // Color de amenaza (Opcional)
                commAmenazaText.color = (commAmenazaText.text == "Alta") ? Color.red : Color.white;
            }
        }

        void ShowFileDetails(NodeData data)
        {
            filePanel.SetActive(true);

            fileRiskText.text = "Riesgo: " + (data.risk_level ?? "N/A");

            // Pintar texto de riesgo
            if (data.risk_level == "Alto") fileRiskText.color = Color.red;
            else if (data.risk_level == "Medio") fileRiskText.color = Color.yellow;
            else fileRiskText.color = Color.green;

            if (data.data != null)
            {
                fileFullText.text = data.data.full_text;

                // Cargar Imagen
                if (data.data.images != null && data.data.images.Length > 0)
                {
                    // Tomamos la primera imagen
                    string rawPath = data.data.images[0];
                    StartCoroutine(LoadImageFromDisk(rawPath));
                }
                else
                {
                    // Poner imagen por defecto o ocultar
                    fileImageViewer.sprite = null;
                    fileImageViewer.color = new Color(0, 0, 0, 0.5f); // Gris transparente
                }
            }
        }

        // --- CARGADOR DE IMÁGENES ---
        IEnumerator LoadImageFromDisk(string jsonPath)
        {
            if (imageLoadingSpinner != null) imageLoadingSpinner.SetActive(true);

            // 1. Limpiar ruta (El JSON viene como /content/drive/My Drive/...)
            // Buscamos la parte después del último '/' o ajustamos según tus carpetas.
            // Asumiré que tus carpetas en StreamingAssets coinciden con el nombre de carpeta final.

            // EJEMPLO: jsonPath = "/content/drive/My Drive/Research/Images_Processed/foto.jpg"
            // META: Application.streamingAssetsPath + "/Images_Processed/foto.jpg"

            string fileName = Path.GetFileName(jsonPath);
            string folderName = "Images_Processed"; // Por defecto, o lógica para detectar carpeta padre

            // Lógica para detectar si es News, Blogs, etc.
            if (jsonPath.Contains("News")) folderName = "News_Cleaned";
            else if (jsonPath.Contains("Blogs")) folderName = "Blogs_Cleaned";
            else if (jsonPath.Contains("Databases")) folderName = "Databases_Cleaned";
            // etc...

            string localPath = Path.Combine(Application.streamingAssetsPath, folderName, fileName);

            // Necesario para Android/PC local
            string url = "file://" + localPath;

            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Error cargando imagen: " + uwr.error + " | Ruta: " + localPath);
                }
                else
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(uwr);

                    // Crear Sprite y asignarlo
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    fileImageViewer.sprite = sprite;
                    fileImageViewer.color = Color.white; // Hacerla visible
                }
            }

            if (imageLoadingSpinner != null) imageLoadingSpinner.SetActive(false);
        }
    }
}