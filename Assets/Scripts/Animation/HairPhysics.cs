using UnityEngine;
using System.Collections.Generic;

namespace Orlo.Animation
{
    /// <summary>
    /// Lightweight spring-chain physics for hair, cloaks, and dangling equipment.
    /// Uses Verlet integration on a chain of virtual particles anchored to a bone.
    /// Much lighter than Unity Cloth — designed for many characters at once.
    /// </summary>
    public class HairPhysics : MonoBehaviour
    {
        [Header("Chain Settings")]
        [SerializeField] private int segmentCount = 5;
        [SerializeField] private float segmentLength = 0.08f;
        [SerializeField] private float damping = 0.92f;
        [SerializeField] private float stiffness = 0.3f;
        [SerializeField] private float gravityScale = 1.5f;

        [Header("Response")]
        [SerializeField] private float movementInfluence = 0.5f;
        [SerializeField] private float windInfluence = 0.1f;

        private Vector3[] _positions;
        private Vector3[] _prevPositions;
        private Transform _anchor; // The bone this chain hangs from
        private Vector3 _lastAnchorPos;

        private static float _globalWindTime;
        private static readonly Vector3 WindDirection = new Vector3(0.3f, 0, 0.7f).normalized;

        /// <summary>
        /// Initialize the chain hanging from a specific bone transform.
        /// </summary>
        public void Initialize(Transform anchorBone, int segments = 5, float segLen = 0.08f)
        {
            _anchor = anchorBone;
            segmentCount = segments;
            segmentLength = segLen;

            _positions = new Vector3[segmentCount];
            _prevPositions = new Vector3[segmentCount];

            Vector3 start = _anchor.position;
            for (int i = 0; i < segmentCount; i++)
            {
                _positions[i] = start + Vector3.down * segmentLength * (i + 1);
                _prevPositions[i] = _positions[i];
            }
            _lastAnchorPos = start;
        }

        private void LateUpdate()
        {
            if (_anchor == null || _positions == null) return;

            _globalWindTime += Time.deltaTime;
            Vector3 anchorPos = _anchor.position;

            // Movement velocity influence
            Vector3 anchorVelocity = (anchorPos - _lastAnchorPos) / Mathf.Max(Time.deltaTime, 0.001f);
            _lastAnchorPos = anchorPos;

            // Wind
            float windStrength = (Mathf.Sin(_globalWindTime * 1.2f) * 0.5f + 0.5f) * windInfluence;
            Vector3 wind = WindDirection * windStrength;

            // Verlet integration
            for (int i = 0; i < segmentCount; i++)
            {
                Vector3 current = _positions[i];
                Vector3 velocity = (current - _prevPositions[i]) * damping;

                // Forces
                velocity += Physics.gravity * gravityScale * Time.deltaTime * Time.deltaTime;
                velocity -= anchorVelocity * movementInfluence * Time.deltaTime;
                velocity += wind * Time.deltaTime;

                _prevPositions[i] = current;
                _positions[i] = current + velocity;
            }

            // Constraint solving (keep chain segments at fixed length)
            for (int iteration = 0; iteration < 3; iteration++)
            {
                // First segment anchored to bone
                Vector3 anchorDown = anchorPos + Vector3.down * segmentLength;
                Vector3 diff = _positions[0] - anchorDown;
                float dist = diff.magnitude;
                if (dist > 0.001f)
                {
                    _positions[0] = anchorDown + diff / dist * Mathf.Min(dist, segmentLength);
                }

                // Chain constraints
                for (int i = 1; i < segmentCount; i++)
                {
                    diff = _positions[i] - _positions[i - 1];
                    dist = diff.magnitude;
                    if (dist > segmentLength)
                    {
                        Vector3 correction = diff * (1f - segmentLength / dist) * 0.5f;
                        _positions[i] -= correction;
                        _positions[i - 1] += correction * (1f - stiffness);
                    }
                }
            }
        }

        /// <summary>
        /// Get chain positions for rendering (e.g., line renderer or mesh deformation).
        /// First position is the anchor bone, followed by chain segments.
        /// </summary>
        public Vector3[] GetChainPositions()
        {
            if (_positions == null) return null;
            var result = new Vector3[segmentCount + 1];
            result[0] = _anchor != null ? _anchor.position : Vector3.zero;
            System.Array.Copy(_positions, 0, result, 1, segmentCount);
            return result;
        }

        /// <summary>
        /// Attach a visual (LineRenderer or trail) to show the hair chain.
        /// </summary>
        public void AttachLineRenderer(Color color, float startWidth = 0.03f, float endWidth = 0.005f)
        {
            var lr = gameObject.AddComponent<LineRenderer>();
            lr.positionCount = segmentCount + 1;
            lr.startWidth = startWidth;
            lr.endWidth = endWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0.3f);
            lr.useWorldSpace = true;

            // Update line renderer in LateUpdate
            StartCoroutine(UpdateLineRenderer(lr));
        }

        private System.Collections.IEnumerator UpdateLineRenderer(LineRenderer lr)
        {
            while (lr != null)
            {
                var positions = GetChainPositions();
                if (positions != null)
                    lr.SetPositions(positions);
                yield return null;
            }
        }
    }
}
