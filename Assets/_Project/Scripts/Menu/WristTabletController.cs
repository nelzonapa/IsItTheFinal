using UnityEngine;
using UnityEngine.InputSystem;

public class WristTabletController : MonoBehaviour
{
    [Header("Elementos UI")]
    [Tooltip("El Canvas World Space de la tableta")]
    public GameObject tabletCanvas;

    [Header("Anclajes (Sockets)")]
    public Transform leftSocket;
    public Transform rightSocket;

    [Header("Controles (Botones X y A)")]
    public InputActionReference leftMenuButton;  // Ej: Controlador Izquierdo -> X o Menu
    public InputActionReference rightMenuButton; // Ej: Controlador Derecho -> A o Menu

    [Header("Ajustes de UX")]
    [Tooltip("Velocidad con la que la tableta sigue a la mano. Valores bajos = más suavidad.")]
    public float followSpeed = 12f;

    // Variables de Estado Interno
    private bool isTabletActive = false;
    private Transform currentTargetSocket;

    private void OnEnable()
    {
        // Nos suscribimos a los eventos y ACTIVAMOS las acciones para que Unity las escuche
        if (leftMenuButton != null)
        {
            leftMenuButton.action.Enable();
            leftMenuButton.action.performed += OnLeftButtonPressed;
        }
        if (rightMenuButton != null)
        {
            rightMenuButton.action.Enable();
            rightMenuButton.action.performed += OnRightButtonPressed;
        }
    }

    private void OnDisable()
    {
        // Nos desuscribimos al desactivar
        if (leftMenuButton != null)
        {
            leftMenuButton.action.performed -= OnLeftButtonPressed;
            leftMenuButton.action.Disable();
        }
        if (rightMenuButton != null)
        {
            rightMenuButton.action.performed -= OnRightButtonPressed;
            rightMenuButton.action.Disable();
        }
    }

    private void Start()
    {
        // Al iniciar, la tableta siempre empieza apagada
        tabletCanvas.SetActive(false);
    }

    // --- MÁQUINA DE ESTADOS DE BOTONES ---

    private void OnLeftButtonPressed(InputAction.CallbackContext context)
    {
        ProcessMenuToggle(leftSocket);
    }

    private void OnRightButtonPressed(InputAction.CallbackContext context)
    {
        ProcessMenuToggle(rightSocket);
    }

    private void ProcessMenuToggle(Transform targetSocket)
    {
        if (!isTabletActive)
        {
            // ESTADO 1: Estaba apagada. La encendemos en la mano que llamó.
            isTabletActive = true;
            currentTargetSocket = targetSocket;
            tabletCanvas.SetActive(true);

            // Teletransporte inmediato para que no se vea volando desde lejos al abrir
            tabletCanvas.transform.position = currentTargetSocket.position;
            tabletCanvas.transform.rotation = currentTargetSocket.rotation;
        }
        else
        {
            if (currentTargetSocket == targetSocket)
            {
                // ESTADO 2: Estaba encendida en mi mano y apreté el botón de ESA MISMA mano. Se cierra.
                isTabletActive = false;
                tabletCanvas.SetActive(false);
            }
            else
            {
                // ESTADO 3: Estaba encendida en la OTRA mano. Pasa a esta mano sin cerrarse.
                currentTargetSocket = targetSocket;
            }
        }
    }

    // --- FÍSICA DE SEGUIMIENTO ---

    private void Update()
    {
        // Si está activa, la movemos suavemente hacia el enchufe (Socket) objetivo
        if (isTabletActive && currentTargetSocket != null)
        {
            tabletCanvas.transform.position = Vector3.Lerp(tabletCanvas.transform.position, currentTargetSocket.position, Time.deltaTime * followSpeed);
            tabletCanvas.transform.rotation = Quaternion.Slerp(tabletCanvas.transform.rotation, currentTargetSocket.rotation, Time.deltaTime * followSpeed);
        }
    }
}