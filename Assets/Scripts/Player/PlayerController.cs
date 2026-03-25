using UnityEngine;
using Orlo.Network;

namespace Orlo.Player
{
    /// <summary>
    /// Third-person player controller with jump support.
    /// Sends movement input to server, receives authoritative position corrections.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 10f;
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravity = -20f;

        [Header("Camera")]
        [SerializeField] private Transform cameraRig;
        [SerializeField] private float mouseSensitivity = 2f;

        private CharacterController _cc;
        private Vector3 _velocity;
        private float _cameraPitch;
        private bool _isSprinting;

        // Send rate: 10 times per second (every other frame at 20 tick)
        private float _sendTimer;
        private const float SendInterval = 0.1f;

        private void Start()
        {
            _cc = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            HandleMouseLook();
            HandleMovement();
            SendMovementInput();
        }

        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up * mouseX);
            _cameraPitch -= mouseY;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -80f, 80f);

            if (cameraRig != null)
                cameraRig.localRotation = Quaternion.Euler(_cameraPitch, 0, 0);
        }

        private void HandleMovement()
        {
            _isSprinting = Input.GetKey(KeyCode.LeftShift);
            float speed = _isSprinting ? sprintSpeed : walkSpeed;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 move = transform.right * h + transform.forward * v;
            move = Vector3.ClampMagnitude(move, 1f) * speed;

            if (_cc.isGrounded)
            {
                _velocity.y = -2f; // Small downward force to stay grounded
                if (Input.GetButtonDown("Jump"))
                {
                    _velocity.y = jumpForce;
                }
            }

            _velocity.y += gravity * Time.deltaTime;
            _cc.Move((move + Vector3.up * _velocity.y) * Time.deltaTime);
        }

        private void SendMovementInput()
        {
            _sendTimer -= Time.deltaTime;
            if (_sendTimer > 0) return;
            _sendTimer = SendInterval;

            if (!NetworkManager.Instance.IsConnected) return;

            // TODO: Serialize PlayerMoveInput protobuf and send
            // For now this is a placeholder for the network integration
        }

        /// <summary>
        /// Called when server sends authoritative position correction.
        /// </summary>
        public void ApplyServerCorrection(Vector3 position, Quaternion rotation)
        {
            // Smooth interpolation to server position
            transform.position = Vector3.Lerp(transform.position, position, 0.3f);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 0.3f);
        }
    }
}
