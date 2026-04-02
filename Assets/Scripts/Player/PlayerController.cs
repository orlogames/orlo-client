using UnityEngine;
using Orlo.Network;
using Orlo.UI;

namespace Orlo.Player
{
    /// <summary>
    /// Third-person player controller with server-authoritative movement.
    /// Sends PlayerMoveInput packets, receives position corrections.
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

        [Header("Network")]
        [SerializeField] private float correctionLerpSpeed = 0.3f;

        private CharacterController _cc;
        private Vector3 _velocity;
        private float _cameraPitch;
        private bool _isSprinting;
        private bool _isJumping;
        private bool _rmbHeld;   // right mouse button held — enables mouselook
        private bool _lmbHeld;   // left mouse button held (LMB+RMB = auto-forward)

        // Send rate: 10 times per second
        private float _sendTimer;
        private const float SendInterval = 0.1f;

        // Server correction target
        private Vector3? _serverPosition;
        private Quaternion? _serverRotation;

        private void Start()
        {
            _cc = GetComponent<CharacterController>();
            // Start with cursor visible — mouselook only activates on RMB hold
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            HandleMouseButtons();
            HandleMouseLook();
            HandleMovement();
            ApplyServerCorrections();
            SendMovementInput();
        }

        private void HandleMouseButtons()
        {
            // Track RMB/LMB state and manage cursor accordingly
            bool wasRmb = _rmbHeld;
            _rmbHeld = Input.GetMouseButton(1);
            _lmbHeld = Input.GetMouseButton(0);

            if (_rmbHeld && !wasRmb)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (!_rmbHeld && wasRmb)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleMouseLook()
        {
            // Mouselook only active while RMB is held
            if (!_rmbHeld) return;

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

            // Admin speed override
            var admin = AdminPanel.Instance;
            float speed;
            if (admin != null && admin.IsAdmin && admin.RunSpeed > 0)
                speed = admin.RunSpeed;
            else
                speed = _isSprinting ? sprintSpeed : walkSpeed;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            // LMB + RMB held = auto-run forward (WoW/SWG style)
            if (_lmbHeld && _rmbHeld && v == 0)
                v = 1f;

            Vector3 move = transform.right * h + transform.forward * v;
            move = Vector3.ClampMagnitude(move, 1f) * speed;

            // Admin fly mode — Space/Ctrl for vertical, no gravity
            bool flyMode = admin != null && admin.FlyEnabled;
            _isJumping = false;

            if (flyMode)
            {
                float vertical = 0;
                if (Input.GetKey(KeyCode.Space)) vertical = speed;
                if (Input.GetKey(KeyCode.LeftControl)) vertical = -speed;
                _velocity.y = vertical;
                _cc.Move((move + Vector3.up * _velocity.y) * Time.deltaTime);
            }
            else
            {
                if (_cc.isGrounded)
                {
                    _velocity.y = -2f;
                    if (Input.GetButtonDown("Jump"))
                    {
                        _velocity.y = jumpForce;
                        _isJumping = true;
                    }
                }

                _velocity.y += gravity * Time.deltaTime;
                _cc.Move((move + Vector3.up * _velocity.y) * Time.deltaTime);
            }
        }

        private void ApplyServerCorrections()
        {
            if (_serverPosition.HasValue)
            {
                float dist = Vector3.Distance(transform.position, _serverPosition.Value);
                // Only correct if drift exceeds threshold (avoids jitter on good connections)
                if (dist > 0.1f)
                {
                    transform.position = Vector3.Lerp(transform.position, _serverPosition.Value, correctionLerpSpeed);
                }
                if (dist < 0.05f)
                {
                    _serverPosition = null; // Close enough, stop correcting
                }
            }

            if (_serverRotation.HasValue)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, _serverRotation.Value, correctionLerpSpeed);
                if (Quaternion.Angle(transform.rotation, _serverRotation.Value) < 1f)
                {
                    _serverRotation = null;
                }
            }
        }

        private void SendMovementInput()
        {
            _sendTimer -= Time.deltaTime;
            if (_sendTimer > 0) return;
            _sendTimer = SendInterval;

            if (!NetworkManager.Instance.IsConnected) return;

            var data = PacketBuilder.PlayerMoveInput(
                transform.position,
                transform.rotation,
                _cc.velocity,
                _isJumping,
                _isSprinting
            );
            NetworkManager.Instance.Send(data);
        }

        /// <summary>
        /// Called when server sends authoritative position correction.
        /// </summary>
        public void ApplyServerCorrection(Vector3 position, Quaternion rotation)
        {
            _serverPosition = position;
            _serverRotation = rotation;
        }
    }
}
