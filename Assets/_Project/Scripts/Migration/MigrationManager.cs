using UnityEngine;
using Fusion;
using System.Collections.Generic;
using ImmersiveGraph.Interaction; // Donde están tus scripts locales
using ImmersiveGraph.Network;     // Donde están tus scripts de red

public class MigrationManager : MonoBehaviour
{
    [Header("Referencias de Zona Local")]
    public Transform zoneCenter;   // El centro de tu Zone5 (para calcular offset)
    public Vector3 zoneSize = new Vector3(0.5f, 0.5f, 0.5f); // Tamaño del área a escanear
    public LayerMask scanLayer;    // Layer donde están los PostIts/Tokens (Everything o Default)

    [Header("Prefabs de Red (Arrástralos aquí)")]
    public NetworkObject netPostItPrefab;
    public NetworkObject netTokenPrefab;
    public NetworkObject netLinePrefab;

    private NetworkRunner _runner;

    // EL DICCIONARIO MÁGICO
    // Clave: El GameObject Local (El PostIt en tu mesa)
    // Valor: El NetworkId del objeto nuevo en la mesa grupal
    private Dictionary<GameObject, NetworkId> _migrationMap = new Dictionary<GameObject, NetworkId>();

    public void ExecuteMigration()
    {
        _runner = FindFirstObjectByType<NetworkRunner>();
        if (_runner == null || !_runner.IsRunning)
        {
            Debug.LogError("No se puede migrar: No estás conectado a Fusion.");
            return;
        }

        Debug.Log("--- INICIANDO MIGRACIÓN A MESA GRUPAL ---");
        _migrationMap.Clear();

        // 1. Obtener destino (ReceptionZone)
        Transform targetZone = GroupTableManager.Instance.GetReceptionZoneForPlayer(_runner.LocalPlayer);
        Vector3 targetCenter = targetZone.position;
        Quaternion targetRotation = targetZone.rotation; // Por si la mesa está rotada

        // 2. ESCANEAR NODOS (PostIts y Tokens)
        // Usamos OverlapBox para encontrar todo lo que hay en tu zona 5
        Collider[] hits = Physics.OverlapBox(zoneCenter.position, zoneSize / 2, zoneCenter.rotation, scanLayer);

        List<ConnectionLine> linesToMigrate = new List<ConnectionLine>();

        foreach (var hit in hits)
        {
            GameObject localObj = hit.gameObject;

            // A. Si es una LÍNEA, la guardamos para después (necesitamos que existan los nodos primero)
            ConnectionLine lineScript = localObj.GetComponent<ConnectionLine>();
            if (lineScript == null) lineScript = localObj.GetComponentInParent<ConnectionLine>();

            if (lineScript != null)
            {
                if (!linesToMigrate.Contains(lineScript))
                    linesToMigrate.Add(lineScript);
                continue; // Pasamos al siguiente
            }

            // B. Si es un NODO (PostIt o Token) y tiene el Tag "Connectable"
            if (localObj.CompareTag("Connectable"))
            {
                // Evitar duplicados (por si el collider es compuesto)
                if (_migrationMap.ContainsKey(localObj)) continue;

                MigrateNode(localObj, targetCenter, targetRotation);
            }
        }

        // 3. RECONSTRUIR CONEXIONES
        foreach (var localLine in linesToMigrate)
        {
            MigrateLine(localLine);
        }

        Debug.Log($"--- MIGRACIÓN COMPLETA: {_migrationMap.Count} Nodos y {linesToMigrate.Count} Líneas ---");

        // Opcional: Limpiar la zona local (Borrar los objetos viejos)
        // CleanLocalZone(hits);
    }

    void MigrateNode(GameObject localObj, Vector3 targetCenter, Quaternion targetRotation)
    {
        // 1. Calcular Posición Relativa
        // (Posición Local - Centro Local) = Offset
        Vector3 relativePos = localObj.transform.position - zoneCenter.position;

        // Nueva Posición = Centro Destino + Offset
        // Nota: Si la mesa grupal rota, deberíamos rotar el offset, pero asumiremos alineación simple por ahora.
        Vector3 spawnPos = targetCenter + relativePos;
        Quaternion spawnRot = localObj.transform.rotation;

        NetworkObject newNetObj = null;

        // 2. Detectar Tipo y Spawnear
        // TIPO POST-IT
        EditablePostIt localPostIt = localObj.GetComponent<EditablePostIt>();
        if (localPostIt == null) localPostIt = localObj.GetComponentInParent<EditablePostIt>();

        if (localPostIt != null)
        {
            newNetObj = _runner.Spawn(netPostItPrefab, spawnPos, spawnRot, _runner.LocalPlayer);

            // Sincronizar Datos Iniciales
            string content = localPostIt.inputField.text;
            newNetObj.GetComponent<NetworkPostItSync>().NetworkContent = content; // Asignación directa antes del primer update
        }

        // TIPO TOKEN
        ExtractedToken localToken = localObj.GetComponent<ExtractedToken>();
        if (localToken == null) localToken = localObj.GetComponentInParent<ExtractedToken>();

        if (localToken != null)
        {
            newNetObj = _runner.Spawn(netTokenPrefab, spawnPos, spawnRot, _runner.LocalPlayer);

            // Sincronizar Datos
            string label = localToken.labelText.text;
            // Pasamos ID vacío por ahora o el que tenía
            newNetObj.GetComponent<NetworkTokenSync>().InitializeToken(label, "migrated_token");
        }

        // 3. REGISTRAR EN EL MAPA
        if (newNetObj != null)
        {
            // Mapeamos el GameObject raíz local -> NetworkId del nuevo
            _migrationMap.Add(localObj, newNetObj.Id);
        }
    }

    void MigrateLine(ConnectionLine localLine)
    {
        // Validación de seguridad
        if (localLine.startNode == null || localLine.endNode == null) return;

        GameObject startObj = localLine.startNode.gameObject;
        GameObject endObj = localLine.endNode.gameObject;

        // Debug para ver qué está intentando conectar
        // Debug.Log($"Intentando migrar línea entre {startObj.name} y {endObj.name}...");

        NetworkId startNetID = FindNetworkIdFor(startObj);
        NetworkId endNetID = FindNetworkIdFor(endObj);

        if (startNetID.IsValid && endNetID.IsValid)
        {
            // Debug.Log($"¡IDs encontrados! Start: {startNetID} | End: {endNetID}. Spawneando línea...");

            // Crear la línea de red
            NetworkObject netLine = _runner.Spawn(netLinePrefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer);

            // Asignar conexiones
            netLine.GetComponent<NetworkConnectionLine>().SetConnections(startNetID, endNetID);
        }
        else
        {
            Debug.LogWarning($"[MIGRATION ERROR] No pude encontrar los IDs de red para la línea.\n" +
                             $"Local Start: {startObj.name} -> NetID Found: {startNetID}\n" +
                             $"Local End: {endObj.name} -> NetID Found: {endNetID}");
        }
    }

    // Ayuda a encontrar el ID si el ConnectionLine apuntaba a un hijo (ej. un Canvas) en lugar de la raíz
    NetworkId FindNetworkIdFor(GameObject obj)
    {
        Transform current = obj.transform;
        while (current != null)
        {
            if (_migrationMap.ContainsKey(current.gameObject))
            {
                return _migrationMap[current.gameObject];
            }
            current = current.parent;
        }
        return default(NetworkId);
    }

    // Opcional: Dibujar el cubo de escaneo en el editor para ver qué abarca
    void OnDrawGizmosSelected()
    {
        if (zoneCenter != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(zoneCenter.position, zoneSize);
        }
    }
}