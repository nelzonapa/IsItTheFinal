using UnityEngine;
using System.Collections.Generic;
using ImmersiveGraph.Network;

namespace ImmersiveGraph.Collaboration
{
    public class SharedWorkspaceTracker : MonoBehaviour
    {
        public static SharedWorkspaceTracker Instance { get; private set; }

        [Header("Configuración de Detección")]
        public float collaborativeRadius = 15f;
        public float updateRateHz = 2f;

        public struct TrackedToken
        {
            public string id;
            public Vector3 position;
            public Color color;
        }

        public List<TrackedToken> ActiveTokens { get; private set; } = new List<TrackedToken>();
        public Vector2 BoundingBoxCenter { get; private set; }
        public Vector2 BoundingBoxSize { get; private set; }

        private float _timer = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= (1f / updateRateHz))
            {
                _timer = 0f;
                CalculateWorkspaceData();
            }
        }

        private void CalculateWorkspaceData()
        {
            ActiveTokens.Clear();

            var allNetworkObjects = FindObjectsByType<NetworkObjectColor>(FindObjectsSortMode.None);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            bool hasObjects = false;

            int validObjectsCount = 0;

            foreach (var netObj in allNetworkObjects)
            {
                if (netObj == null) continue;

                // --- SOLUCIÓN 1: FILTRO EXACTO ---
                // Ignoramos todo lo que NO sea Token o PostIt (ej. Lápices, Bloques base)
                string objName = netObj.gameObject.name;
                if (!objName.Contains("Token") && !objName.Contains("PostIt"))
                {
                    continue; // Saltar al siguiente objeto
                }

                float dist = Vector3.Distance(netObj.transform.position, transform.position);

                if (dist <= collaborativeRadius)
                {
                    hasObjects = true;
                    validObjectsCount++;
                    Vector3 pos = netObj.transform.position;

                    if (pos.x < minX) minX = pos.x;
                    if (pos.x > maxX) maxX = pos.x;
                    if (pos.z < minZ) minZ = pos.z;
                    if (pos.z > maxZ) maxZ = pos.z;

                    Color objColor = Color.white;
                    Renderer r = netObj.GetComponent<Renderer>();
                    if (r != null && r.material != null) objColor = r.material.color;

                    ActiveTokens.Add(new TrackedToken
                    {
                        id = netObj.gameObject.GetInstanceID().ToString(),
                        position = pos,
                        color = objColor
                    });
                }
            }

            Debug.Log($"[Tracker 3D] Escaneo limpio: {validObjectsCount} Anotes/Tokens detectados en el área.");

            if (hasObjects)
            {
                BoundingBoxCenter = new Vector2((minX + maxX) / 2f, (minZ + maxZ) / 2f);
                BoundingBoxSize = new Vector2(Mathf.Max(maxX - minX, 2f), Mathf.Max(maxZ - minZ, 2f));
            }
            else
            {
                BoundingBoxCenter = new Vector2(transform.position.x, transform.position.z);
                BoundingBoxSize = new Vector2(2f, 2f);
            }
        }
    }
}