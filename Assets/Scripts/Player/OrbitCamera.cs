using UnityEngine;

namespace Orlo.Player
{
    /// <summary>
    /// Third-person orbit camera that rotates around the player.
    /// - RMB hold: orbit freely (also turns character on horizontal axis)
    /// - Scroll wheel: zoom in/out
    /// - Camera collides with terrain/buildings to avoid clipping
    /// - Smooth follow with configurable damping
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0, 1.6f, 0); // shoulder height

        [Header("Orbit")]
        [SerializeField] private float distance = 6f;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float zoomSmoothing = 8f;

        [Header("Rotation")]
        [SerializeField] private float mouseSensitivity = 2.5f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 75f;
        [SerializeField] private float rotationSmoothing = 12f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private LayerMask collisionMask = ~0; // everything
        [SerializeField] private float collisionSmoothing = 10f;

        private float _yaw;
        private float _pitch = 15f;
        private float _targetDistance;
        private float _currentDistance;
        private bool _lmbHeld;
        private bool _rmbHeld;

        private void Start()
        {
            _targetDistance = distance;
            _currentDistance = distance;

            if (target != null)
            {
                // Initialize yaw from target's current facing
                _yaw = target.eulerAngles.y;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleInput();
            UpdatePosition();
        }

        /// <summary>Set true to block camera mouse input (UI overlays, menus, welcome screen).</summary>
        public static bool BlockInput;

        private void HandleInput()
        {
            bool wasLmb = _lmbHeld;
            bool wasRmb = _rmbHeld;

            // Don't capture mouse when UI overlays are active
            if (BlockInput)
            {
                _lmbHeld = false;
                _rmbHeld = false;
                if (wasLmb || wasRmb)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return;
            }

            _lmbHeld = Input.GetMouseButton(0);
            _rmbHeld = Input.GetMouseButton(1);

            bool anyMouseHeld = _lmbHeld || _rmbHeld;
            bool anyMouseWasHeld = wasLmb || wasRmb;

            // Lock/unlock cursor when any mouse button is held for camera control
            if (anyMouseHeld && !anyMouseWasHeld)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (!anyMouseHeld && anyMouseWasHeld)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // LMB: freelook (orbit camera without turning character)
            if (_lmbHeld)
            {
                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

                _yaw += mouseX;
                _pitch -= mouseY;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
                // Character does NOT rotate — camera orbits freely
            }

            // RMB: orbit + turn character to match camera
            if (_rmbHeld)
            {
                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

                _yaw += mouseX;
                _pitch -= mouseY;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

                // Turn character to match camera horizontal rotation
                target.rotation = Quaternion.Euler(0, _yaw, 0);
            }

            // LMB+RMB: auto-run forward
            if (_lmbHeld && _rmbHeld)
            {
                target.rotation = Quaternion.Euler(0, _yaw, 0);
            }

            // Scroll zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _targetDistance -= scroll * zoomSpeed;
                _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
            }

            // Smooth zoom
            _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, Time.deltaTime * zoomSmoothing);
        }

        /// <summary>
        /// Whether LMB is held (freelook without character turn).
        /// </summary>
        public bool IsFreelooking => _lmbHeld && !_rmbHeld;

        private void UpdatePosition()
        {
            Vector3 pivot = target.position + targetOffset;

            // Calculate desired camera position from orbit angles
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);
            Vector3 desiredPosition = pivot - rotation * Vector3.forward * _currentDistance;

            // Collision check — pull camera forward if it would clip through geometry
            float adjustedDistance = _currentDistance;
            Vector3 direction = (desiredPosition - pivot).normalized;
            if (Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit,
                _currentDistance, collisionMask))
            {
                adjustedDistance = hit.distance - collisionRadius * 0.5f;
                adjustedDistance = Mathf.Max(adjustedDistance, minDistance * 0.5f);
            }

            Vector3 finalPosition = pivot - rotation * Vector3.forward * adjustedDistance;

            // Smooth follow
            transform.position = Vector3.Lerp(transform.position, finalPosition,
                Time.deltaTime * collisionSmoothing);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation,
                Time.deltaTime * rotationSmoothing);

            // Always look at the pivot point
            transform.LookAt(pivot);
        }

        /// <summary>
        /// Set the orbit target (called from GameBootstrap when player spawns).
        /// </summary>
        public void SetTarget(Transform t)
        {
            target = t;
            if (t != null)
                _yaw = t.eulerAngles.y;
        }

        /// <summary>
        /// Get current yaw for movement direction reference.
        /// </summary>
        public float Yaw => _yaw;

        /// <summary>
        /// Whether RMB is currently held (mouselook active).
        /// </summary>
        public bool IsOrbiting => _rmbHeld;
    }
}
