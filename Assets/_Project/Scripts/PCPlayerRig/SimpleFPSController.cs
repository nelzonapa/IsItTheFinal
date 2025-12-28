using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // Necesario para TextMeshPro
using UnityEngine.EventSystems;

namespace ImmersiveGraph.Core
{
    [RequireComponent(typeof(CharacterController))]
    public class SimpleFPSController : MonoBehaviour
    {
        public float speed = 5.0f;
        public float mouseSensitivity = 0.5f;
        public Transform playerCamera;

        private CharacterController _controller;
        private float _verticalRotation = 0f;

        void Start()
        {
            _controller = GetComponent<CharacterController>();
            // Bloqueamos el cursor al inicio
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            // --- LÓGICA DE DETECCIÓN DE ESCRITURA ---
            if (IsTyping())
            {
                // 1. Si presionamos ENTER, dejamos de escribir (quitamos el foco)
                if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                {
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

                    // Re-bloqueamos el cursor inmediatamente para seguir jugando
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    return;
                }

                // 2. Si estamos escribiendo, liberamos el mouse y BLOQUEAMOS EL MOVIMIENTO
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return; // <--- AQUÍ SE DETIENE EL UPDATE (No te mueves)
            }
            // ----------------------------------------

            // 1. MOVIMIENTO (WASD)
            Vector2 moveInput = Vector2.zero;
            // Usamos verificaciones seguras
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
                if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
                if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
                if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
            }

            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            _controller.Move(move * speed * Time.deltaTime);
            _controller.Move(Vector3.down * 9.81f * Time.deltaTime);

            // 2. MIRADA (MOUSE)
            if (Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                float mouseX = mouseDelta.x * mouseSensitivity;
                float mouseY = mouseDelta.y * mouseSensitivity;

                _verticalRotation -= mouseY;
                _verticalRotation = Mathf.Clamp(_verticalRotation, -90f, 90f);

                if (playerCamera != null)
                    playerCamera.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);

                transform.Rotate(Vector3.up * mouseX);

                // Re-bloquear cursor si hacemos clic fuera (para volver a jugar)
                if (Mouse.current.leftButton.wasPressedThisFrame && !IsTyping())
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        // Función auxiliar mejorada
        bool IsTyping()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                // Usamos GetComponentInParent porque a veces el foco está en el "TextArea" hijo del InputField
                return EventSystem.current.currentSelectedGameObject.GetComponentInParent<TMP_InputField>() != null;
            }
            return false;
        }
    }
}