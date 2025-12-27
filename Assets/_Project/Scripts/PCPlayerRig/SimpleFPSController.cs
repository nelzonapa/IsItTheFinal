using UnityEngine;
using UnityEngine.InputSystem; 
namespace ImmersiveGraph.Core
{
    [RequireComponent(typeof(CharacterController))]
    public class SimpleFPSController : MonoBehaviour
    {
        public float speed = 5.0f;
        public float mouseSensitivity = 0.5f; // Sensibilidad ajustada para el nuevo sistema
        public Transform playerCamera;

        private CharacterController _controller;
        private float _verticalRotation = 0f;

        void Start()
        {
            _controller = GetComponent<CharacterController>();
            // Bloquear cursor al centro
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            // --- 1. MOVIMIENTO (WASD) con New Input System ---
            Vector2 moveInput = Vector2.zero;

            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;

            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            _controller.Move(move * speed * Time.deltaTime);

            // Gravedad simple
            _controller.Move(Vector3.down * 9.81f * Time.deltaTime);

            // --- 2. MIRADA (MOUSE) con New Input System ---
            // Leemos el delta del mouse directamente del hardware
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            float mouseX = mouseDelta.x * mouseSensitivity;
            float mouseY = mouseDelta.y * mouseSensitivity;

            _verticalRotation -= mouseY;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -90f, 90f);

            if (playerCamera != null)
                playerCamera.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);

            transform.Rotate(Vector3.up * mouseX);

            // Desbloquear cursor con ESC
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            // Re-bloquear con click
            if (Mouse.current.leftButton.wasPressedThisFrame && Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}