using UnityEngine;
using UnityEngine.Events;

public class TabletMenuActions : MonoBehaviour
{
    [Header("Eventos Locales (No Sincronizados)")]
    [Tooltip("Se dispara al pulsar 'Abrir Detalles'. Úsalo para activar el panel 2D en tu UI local.")]
    public UnityEvent onOpenDetailsPanel;

    [Tooltip("Se dispara al pulsar 'Abrir Grafo'. Úsalo para activar el panel de conocimiento local.")]
    public UnityEvent onOpenGraphPanel;

    [Header("Eventos de Red (Requieren Manager)")]
    [Tooltip("Se dispara al pulsar 'Llamar Compañero'. Un script externo (ej. NetworkManager) debe escuchar esto y hacer el RPC.")]
    public UnityEvent onCallTeammate;

    public void Click_OpenDetails()
    {
        Debug.Log("[Tablet] Solicitud para abrir el panel de Detalles.");
        onOpenDetailsPanel?.Invoke();
    }

    public void Click_OpenGraph()
    {
        Debug.Log("[Tablet] Solicitud para abrir el panel de Grafo de Conocimiento.");
        onOpenGraphPanel?.Invoke();
    }

    public void Click_CallTeammate()
    {
        Debug.Log("[Tablet] Solicitud para llamar al compañero.");
        onCallTeammate?.Invoke();
    }
}