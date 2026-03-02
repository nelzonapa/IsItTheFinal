using UnityEngine;
using Fusion;
using System.Collections.Generic;
using ImmersiveGraph.Interaction;
using ImmersiveGraph.Network;

public class MigrationManager : MonoBehaviour
{
    [Header("Referencias de Zona Local")]
    public Transform zoneCenter;   // Centro de Zone5_Shared (Individual)
    public Vector3 zoneSize = new Vector3(0.5f, 0.5f, 0.5f);
    public LayerMask scanLayer;    // Capa de los objetos

    [Header("Prefabs de Red")]
    public NetworkObject netPostItPrefab;
    public NetworkObject netTokenPrefab;
    public NetworkObject netLinePrefab;

    private NetworkRunner _runner;

    public void ExecuteMigration()
    {
        _runner = FindFirstObjectByType<NetworkRunner>();
        if (_runner == null || !_runner.IsRunning)
        {
            Debug.LogError("No se puede migrar: Sin conexión a Fusion.");
            return;
        }

        Debug.Log("--- TRANSFIRIENDO OBJETOS A LA MESA GRUPAL (CORTAR Y PEGAR) ---");

        // 1. OBTENER DESTINO (Mesa Grupal)
        Transform targetZone = GroupTableManager.Instance.GetReceptionZoneForPlayer(_runner.LocalPlayer);
        Vector3 targetCenter = targetZone.position;

        // 2. ESCANEAR OBJETOS ACTUALES EN MESA INDIVIDUAL
        Collider[] hits = Physics.OverlapBox(zoneCenter.position, zoneSize / 2, zoneCenter.rotation, scanLayer);

        HashSet<GameObject> localNodes = new HashSet<GameObject>();
        List<ConnectionLine> localLines = new List<ConnectionLine>();

        foreach (var hit in hits)
        {
            GameObject obj = hit.gameObject;

            // Filtramos líneas aparte
            var line = obj.GetComponent<ConnectionLine>() ?? obj.GetComponentInParent<ConnectionLine>();
            if (line != null)
            {
                if (!localLines.Contains(line)) localLines.Add(line);
                continue;
            }

            // Filtramos Nodos (PostIts y Tokens)
            if (obj.CompareTag("Connectable"))
            {
                localNodes.Add(obj);
            }
        }

        if (localNodes.Count == 0 && localLines.Count == 0)
        {
            Debug.Log("Mesa limpia. No hay nada que transferir.");
            return; // No hay nada en la bandeja
        }

        // Usamos un mapa TEMPORAL solo para saber cómo conectar las líneas que estamos a punto de crear
        Dictionary<GameObject, NetworkId> tempTransferMap = new Dictionary<GameObject, NetworkId>();

        // 3. TRANSFERIR NODOS A LA RED
        foreach (var localObj in localNodes)
        {
            // Calculamos dónde debería estar en el grupo
            Vector3 relativePos = localObj.transform.position - zoneCenter.position;
            Vector3 targetPos = targetCenter + relativePos;
            Quaternion targetRot = localObj.transform.rotation;

            NetworkObject newNetObj = null;

            EditablePostIt localPostIt = localObj.GetComponent<EditablePostIt>() ?? localObj.GetComponentInParent<EditablePostIt>();
            ExtractedToken localToken = localObj.GetComponent<ExtractedToken>() ?? localObj.GetComponentInParent<ExtractedToken>();

            if (localPostIt != null)
            {
                newNetObj = _runner.Spawn(netPostItPrefab, targetPos, targetRot, _runner.LocalPlayer);
                string content = localPostIt.inputField.text;
                var syncComp = newNetObj.GetComponent<NetworkPostItSync>();
                if (syncComp) syncComp.NetworkContent = content;
            }
            else if (localToken != null)
            {
                newNetObj = _runner.Spawn(netTokenPrefab, targetPos, targetRot, _runner.LocalPlayer);
                string label = localToken.labelText.text;
                string sourceID = string.IsNullOrEmpty(localToken.OriginNodeID) ? "unknown" : localToken.OriginNodeID;

                var syncComp = newNetObj.GetComponent<NetworkTokenSync>();
                if (syncComp) syncComp.InitializeToken(label, sourceID);
            }

            // Guardamos el nuevo ID de red para poder conectar sus líneas
            if (newNetObj != null)
            {
                tempTransferMap.Add(localObj, newNetObj.Id);
            }
        }

        // 4. TRANSFERIR LÍNEAS A LA RED
        foreach (var localLine in localLines)
        {
            if (localLine.startNode == null || localLine.endNode == null) continue;

            GameObject startObj = localLine.startNode.gameObject;
            GameObject endObj = localLine.endNode.gameObject;

            // Solo creamos la línea de red si ambos nodos lograron transferirse
            if (tempTransferMap.ContainsKey(startObj) && tempTransferMap.ContainsKey(endObj))
            {
                NetworkId startNetID = tempTransferMap[startObj];
                NetworkId endNetID = tempTransferMap[endObj];

                NetworkObject netLine = _runner.Spawn(netLinePrefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer);
                netLine.GetComponent<NetworkConnectionLine>().SetConnections(startNetID, endNetID);
            }

            // Destruimos la línea física local para que no quede flotando
            Destroy(localLine.gameObject);
        }

        // 5. DESTRUIR NODOS LOCALES
        // Se hace al final para no romper las referencias de las líneas en el paso 4
        foreach (var localObj in localNodes)
        {
            Destroy(localObj);
        }

        Debug.Log($"¡Viaje exitoso! {localNodes.Count} anotes y {localLines.Count} líneas fueron dejadas en la sala colaborativa.");
    }

    void OnDrawGizmosSelected()
    {
        if (zoneCenter != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(zoneCenter.position, zoneSize);
        }
    }
}