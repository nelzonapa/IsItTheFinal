using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

namespace ImmersiveGraph.Data
{
    // --- ESTRUCTURAS DE DATOS (JSON) ---
    [Serializable]
    public class LocalDataWrapper
    {
        public List<TokenSaveData> savedTokens = new List<TokenSaveData>();
    }

    [Serializable]
    public class TokenSaveData
    {
        public string content;
        public Vector3 position;
        public Quaternion rotation;
    }
    // -----------------------------------

    public class LocalWorkspaceSaver : MonoBehaviour
    {
        public static LocalWorkspaceSaver Instance;

        [Header("Configuración")]
        public GameObject localTokenPrefab; // Arrastra tu Prefab aquí
        public float autoSaveInterval = 5.0f; // Cada cuántos segundos guarda

        private string _savePath;
        private float _timer;

        void Awake()
        {
            if (Instance == null) Instance = this;

            // Definimos la ruta segura (Funciona en PC y Quest)
            _savePath = Path.Combine(Application.persistentDataPath, "LocalWorkspace_Backup.json");
        }

        void Start()
        {
            // Al iniciar la app, intentamos restaurar si hubo un crash previo
            LoadWorkspace();
        }

        void Update()
        {
            // Lógica de Auto-Save
            _timer += Time.deltaTime;
            if (_timer >= autoSaveInterval)
            {
                SaveWorkspace();
                _timer = 0;
            }
        }

        // --- GUARDAR ---
        public void SaveWorkspace()
        {
            LocalDataWrapper data = new LocalDataWrapper();

            // 1. Buscamos todos los tokens activos en la escena
            // Usamos FindObjectsByType que es más rápido y seguro en Unity modernos
            var tokens = FindObjectsByType<LocalTokenBehavior>(FindObjectsSortMode.None);

            // 2. Extraemos sus datos
            foreach (var t in tokens)
            {
                TokenSaveData tData = new TokenSaveData();
                tData.content = t.GetContent();
                tData.position = t.transform.position;
                tData.rotation = t.transform.rotation;

                data.savedTokens.Add(tData);
            }

            // 3. Escribimos al disco (JSON)
            try
            {
                string json = JsonUtility.ToJson(data, true); // True para que sea legible
                File.WriteAllText(_savePath, json);
                // Debug.Log("[AutoSave] Progreso local guardado."); 
            }
            catch (Exception e)
            {
                Debug.LogError($"[AutoSave Error] No se pudo guardar: {e.Message}");
            }
        }

        // --- CARGAR / RESTAURAR ---
        public void LoadWorkspace()
        {
            if (!File.Exists(_savePath))
            {
                Debug.Log("[Load] No existe backup previo. Iniciando mesa limpia.");
                return;
            }

            try
            {
                // 1. Leer archivo
                string json = File.ReadAllText(_savePath);
                LocalDataWrapper data = JsonUtility.FromJson<LocalDataWrapper>(json);

                if (data == null || data.savedTokens.Count == 0) return;

                Debug.Log($"[Load] Restaurando {data.savedTokens.Count} tokens...");

                // 2. Limpiar la escena actual para no duplicar (Opcional, pero recomendado)
                ClearCurrentTokens();

                // 3. Recrear los tokens
                foreach (var tData in data.savedTokens)
                {
                    if (localTokenPrefab != null)
                    {
                        GameObject newToken = Instantiate(localTokenPrefab, tData.position, tData.rotation);

                        // Restaurar el texto
                        var behavior = newToken.GetComponent<LocalTokenBehavior>();
                        if (behavior != null)
                        {
                            behavior.SetContent(tData.content);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Load Error] Archivo corrupto o error de lectura: {e.Message}");
            }
        }

        // --- LIMPIEZA ---

        // Borra los tokens que hay en pantalla antes de cargar el backup
        private void ClearCurrentTokens()
        {
            var tokens = FindObjectsByType<LocalTokenBehavior>(FindObjectsSortMode.None);
            foreach (var t in tokens)
            {
                Destroy(t.gameObject);
            }
        }

        // Esta función PÚBLICA la llamaremos desde el botón "Finalizar"
        public void DeleteBackupFile()
        {
            if (File.Exists(_savePath))
            {
                File.Delete(_savePath);
                Debug.Log("[System] Backup local eliminado (Sesión finalizada correctamente).");
            }
        }
    }
}