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

    // DICCIONARIO: Relaciona Objeto Local (Key) con ID de Red (Value)
    private Dictionary<GameObject, NetworkId> _migrationMap = new Dictionary<GameObject, NetworkId>();

    public void ExecuteMigration()
    {
        _runner = FindFirstObjectByType<NetworkRunner>();
        if (_runner == null || !_runner.IsRunning)
        {
            Debug.LogError("No se puede migrar: Sin conexión a Fusion.");
            return;
        }

        Debug.Log("--- SINCRONIZANDO CON MESA GRUPAL ---");

        // 1. OBTENER DESTINO (Mesa Grupal)
        Transform targetZone = GroupTableManager.Instance.GetReceptionZoneForPlayer(_runner.LocalPlayer);
        Vector3 targetCenter = targetZone.position;

        // 2. ESCANEAR OBJETOS ACTUALES EN MESA INDIVIDUAL
        Collider[] hits = Physics.OverlapBox(zoneCenter.position, zoneSize / 2, zoneCenter.rotation, scanLayer);

        // Creamos una lista limpia de los objetos que ESTÁN AHORA MISMO en la zona
        HashSet<GameObject> currentLocalObjects = new HashSet<GameObject>();
        List<ConnectionLine> currentLines = new List<ConnectionLine>();

        foreach (var hit in hits)
        {
            GameObject obj = hit.gameObject;

            // Filtramos líneas aparte
            var line = obj.GetComponent<ConnectionLine>() ?? obj.GetComponentInParent<ConnectionLine>();
            if (line != null)
            {
                if (!currentLines.Contains(line)) currentLines.Add(line);
                continue;
            }

            // Filtramos Nodos (PostIts y Tokens)
            if (obj.CompareTag("Connectable"))
            {
                currentLocalObjects.Add(obj);
            }
        }

        // 3. LIMPIEZA: Eliminar de la Red lo que ya no existe en Local
        // (Si el usuario borró el PostIt de su mesa individual, se borra de la grupal)
        List<GameObject> toRemoveFromMap = new List<GameObject>();

        foreach (var localObj in _migrationMap.Keys)
        {
            // Si el objeto estaba en el mapa, pero ya NO está en la mesa actual
            if (localObj == null || !currentLocalObjects.Contains(localObj))
            {
                // Destruir el objeto de red correspondiente
                NetworkId netId = _migrationMap[localObj];
                NetworkObject netObj = _runner.FindObject(netId);

                if (netObj != null && netObj.HasStateAuthority)
                {
                    _runner.Despawn(netObj);
                }
                toRemoveFromMap.Add(localObj);
            }
        }

        // Limpiamos el diccionario
        foreach (var deadObj in toRemoveFromMap)
        {
            _migrationMap.Remove(deadObj);
        }

        // 4. CREAR O ACTUALIZAR NODOS
        foreach (var localObj in currentLocalObjects)
        {
            MigrateOrUpdateNode(localObj, targetCenter);
        }

        // 5. RECONSTRUIR LÍNEAS (Las líneas son complejas de actualizar, mejor recrearlas si faltan)
        foreach (var line in currentLines)
        {
            MigrateLine(line);
        }
    }

    void MigrateOrUpdateNode(GameObject localObj, Vector3 targetCenter)
    {
        // Calculamos dónde debería estar
        Vector3 relativePos = localObj.transform.position - zoneCenter.position;
        Vector3 targetPos = targetCenter + relativePos;
        Quaternion targetRot = localObj.transform.rotation;

        // CASO A: Ya lo habíamos migrado antes
        if (_migrationMap.ContainsKey(localObj))
        {
            NetworkId netId = _migrationMap[localObj];
            NetworkObject existingNetObj = _runner.FindObject(netId);

            // Verificamos si sigue existiendo en la red (nadie lo borró allá)
            if (existingNetObj != null)
            {
                // --- SINCRONIZACIÓN (ACTUALIZAR) ---
                // Si el objeto existe, le forzamos la nueva posición/rotación del local
                existingNetObj.transform.position = targetPos;
                existingNetObj.transform.rotation = targetRot;

                // Opcional: Actualizar texto si cambió
                UpdateNetworkData(localObj, existingNetObj);

                return; // Trabajo terminado con este objeto
            }
            else
            {
                // Si existía en el mapa pero es null en la red, significa que alguien lo borró en el grupo.
                // Decisión de Diseño: ¿Lo resucitamos? 
                // RESPUESTA: SÍ. Tu mesa individual es la "Fuente de la Verdad".
                _migrationMap.Remove(localObj); // Lo sacamos del mapa para volver a crearlo abajo
            }
        }

        // CASO B: Es nuevo o hay que recrearlo
        NetworkObject newNetObj = null;

        // Detectar tipo y spawnear
        EditablePostIt localPostIt = localObj.GetComponent<EditablePostIt>() ?? localObj.GetComponentInParent<EditablePostIt>();
        ExtractedToken localToken = localObj.GetComponent<ExtractedToken>() ?? localObj.GetComponentInParent<ExtractedToken>();

        if (localPostIt != null)
        {
            newNetObj = _runner.Spawn(netPostItPrefab, targetPos, targetRot, _runner.LocalPlayer);
            // Pasar datos iniciales
            string content = localPostIt.inputField.text;
            var syncComp = newNetObj.GetComponent<NetworkPostItSync>();
            if (syncComp) syncComp.NetworkContent = content;
        }
        else if (localToken != null)
        {
            newNetObj = _runner.Spawn(netTokenPrefab, targetPos, targetRot, _runner.LocalPlayer);
            // Pasar datos iniciales
            string label = localToken.labelText.text;
            string sourceID = string.IsNullOrEmpty(localToken.OriginNodeID) ? "unknown" : localToken.OriginNodeID;

            var syncComp = newNetObj.GetComponent<NetworkTokenSync>();
            if (syncComp) syncComp.InitializeToken(label, sourceID);
        }

        // Registrar en el mapa
        if (newNetObj != null)
        {
            _migrationMap.Add(localObj, newNetObj.Id);
        }
    }

    // Función auxiliar para actualizar texto en tiempo real
    void UpdateNetworkData(GameObject localObj, NetworkObject netObj)
    {
        // Actualizar PostIt
        EditablePostIt localPostIt = localObj.GetComponent<EditablePostIt>() ?? localObj.GetComponentInParent<EditablePostIt>();
        if (localPostIt != null)
        {
            var syncComp = netObj.GetComponent<NetworkPostItSync>();
            if (syncComp && syncComp.NetworkContent != localPostIt.inputField.text)
            {
                syncComp.NetworkContent = localPostIt.inputField.text;
            }
        }
        // Nota: Los Tokens usualmente no cambian de texto, así que no es necesario actualizarlos.
    }

    void MigrateLine(ConnectionLine localLine)
    {
        // Lógica simplificada para líneas:
        // Las líneas dependen de los IDs de los nodos. 
        // Si ya existe una línea conectando A y B en la red, no hacemos nada.
        // Si no existe, la creamos.

        if (localLine.startNode == null || localLine.endNode == null) return;

        NetworkId startNetID = FindNetworkIdFor(localLine.startNode.gameObject);
        NetworkId endNetID = FindNetworkIdFor(localLine.endNode.gameObject);

        if (startNetID.IsValid && endNetID.IsValid)
        {
            // Verificación simple para evitar duplicar líneas:
            // (Esta parte es costosa computacionalmente, idealmente las líneas también deberían ir al Mapa,
            // pero por ahora dejaremos que se creen. Fusion suele manejar bien objetos ligeros).

            // Si quisieras evitar duplicados estrictos, necesitarías un _linesMigrationMap similar al de nodos.
            // Por ahora, para esta fase, dejaremos que spawnee. 
            // MEJORA RECOMENDADA: Agregar lógica para no spawnear si ya existe.

            NetworkObject netLine = _runner.Spawn(netLinePrefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer);
            netLine.GetComponent<NetworkConnectionLine>().SetConnections(startNetID, endNetID);
        }
    }

    NetworkId FindNetworkIdFor(GameObject obj)
    {
        Transform current = obj.transform;
        while (current != null)
        {
            if (_migrationMap.ContainsKey(current.gameObject)) return _migrationMap[current.gameObject];
            current = current.parent;
        }
        return default(NetworkId);
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