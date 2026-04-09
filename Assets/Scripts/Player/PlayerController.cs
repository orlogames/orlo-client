using UnityEngine;
using Orlo.Animation;
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

        [Header("Network")]
        [SerializeField] private float correctionLerpSpeed = 0.3f;

        private CharacterController _cc;
        private OrbitCamera _orbitCamera;
        private CharacterAnimator _animator;
        private Vector3 _velocity;
        private bool _isSprinting;
        private bool _isJumping;
        private bool _rmbHeld;
        private bool _lmbHeld;

        /// <summary>Current velocity from CharacterController.</summary>
        public Vector3 Velocity => _cc != null ? _cc.velocity : Vector3.zero;
        /// <summary>Whether the CharacterController is touching the ground.</summary>
        public bool IsGrounded => _cc != null && _cc.isGrounded;
        /// <summary>Whether the player is currently sprinting.</summary>
        public bool IsSprinting => _isSprinting;

        // Send rate: 10 times per second
        private float _sendTimer;
        private const float SendInterval = 0.1f;

        // Server correction target
        private Vector3? _serverPosition;
        private Quaternion? _serverRotation;

        private void Start()
        {
            _cc = GetComponent<CharacterController>();
            _orbitCamera = FindFirstObjectByType<OrbitCamera>();
            _animator = GetComponent<CharacterAnimator>();
        }

        private void Update()
        {
            HandleMovement();
            ApplyServerCorrections();
            SendMovementInput();

            // Drive animation from movement state
            // Try Mecanim first (rigged models), fall back to procedural (legacy)
            var mecanim = GetComponent<Orlo.Character.MecanimCharacterController>();
            if (mecanim != null)
            {
                mecanim.SetMovementState(_cc.velocity, _cc.isGrounded, _isSprinting);
            }
            else
            {
                if (_animator == null) _animator = GetComponent<CharacterAnimator>();
                if (_animator != null)
                    _animator.SetMovementState(_cc.velocity, _cc.isGrounded, _isSprinting);
            }
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
            bool overUI = GUIUtility.hotControl != 0
                || (HUDLayout.Instance != null && HUDLayout.Instance.IsMouseOverAnyWindow());

            _rmbHeld = Input.GetMouseButton(1) && !overUI;
            _lmbHeld = Input.GetMouseButton(0) && !overUI;
            if (_lmbHeld && _rmbHeld && v == 0)
                v = 1f;

            // Movement is relative to camera facing direction
            float cameraYaw = _orbitCamera != null ? _orbitCamera.Yaw : transform.eulerAngles.y;
            Quaternion cameraRotation = Quaternion.Euler(0, cameraYaw, 0);
            Vector3 move = cameraRotation * new Vector3(h, 0, v);

            // Turn character to face movement direction when moving
            if (move.sqrMagnitude > 0.01f && (_rmbHeld || move.sqrMagnitude > 0.1f))
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(move), Time.deltaTime * 10f);
            }
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
