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
    public InputActionReference leftMenuButton;  
    public InputActionReference rightMenuButton; 

    [Header("Ajustes de UX")]
    [Tooltip("Velocidad con la que la tableta sigue a la mano. Valores bajos = más suavidad.")]
    public float followSpeed = 12f;

    private bool isTabletActive = false;
    private Transform currentTargetSocket;

    private void OnEnable()
    {
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
        tabletCanvas.SetActive(false);
    }


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
            isTabletActive = true;
            currentTargetSocket = targetSocket;
            tabletCanvas.SetActive(true);

            tabletCanvas.transform.position = currentTargetSocket.position;
            tabletCanvas.transform.rotation = currentTargetSocket.rotation;
        }
        else
        {
            if (currentTargetSocket == targetSocket)
            {
                isTabletActive = false;
                tabletCanvas.SetActive(false);
            }
            else
            {
                currentTargetSocket = targetSocket;
            }
        }
    }


    private void Update()
    {
        if (isTabletActive && currentTargetSocket != null)
        {
            tabletCanvas.transform.position = Vector3.Lerp(tabletCanvas.transform.position, currentTargetSocket.position, Time.deltaTime * followSpeed);
            tabletCanvas.transform.rotation = Quaternion.Slerp(tabletCanvas.transform.rotation, currentTargetSocket.rotation, Time.deltaTime * followSpeed);
        }
    }
}