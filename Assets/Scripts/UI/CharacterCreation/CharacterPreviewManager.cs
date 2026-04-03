using UnityEngine;
using Orlo.World;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// Manages a 3D character preview rendered to a RenderTexture.
    /// Creates a dedicated camera, 3-point lighting, and loads a real 3D model
    /// via ModelCharacter (GLB from StreamingAssets) on the CharacterPreview layer.
    /// Falls back to ProceduralCharacter if the model fails to load.
    /// </summary>
    public class CharacterPreviewManager : MonoBehaviour
    {
        // --- Constants ---
        private const int PreviewLayer = 10;
        private const int RenderWidth = 512;
        private const int RenderHeight = 768;

        // --- Camera orbit ---
        private float _orbitYaw = 180f;
        private float _orbitPitch = 10f;
        private float _orbitDistance = 3f;
        private Vector3 _orbitTarget = new Vector3(0f, 0.9f, 0f);
        private bool _isDragging = false;
        private Vector2 _lastMousePos;

        // --- Components ---
        private Camera _previewCamera;
        private RenderTexture _renderTexture;
        private GameObject _characterGO;
        private ModelCharacter _modelCharacter;
        private ProceduralCharacter _proceduralFallback;
        private Light _keyLight, _fillLight, _rimLight;
        private bool _usingModel = false;

        // --- Focus modes ---
        public enum FocusMode { FullBody, Face, UpperBody }
        private FocusMode _currentFocus = FocusMode.FullBody;

        // --- Public API ---

        public RenderTexture PreviewTexture => _renderTexture;

        public void Initialize()
        {
            _renderTexture = new RenderTexture(RenderWidth, RenderHeight, 24, RenderTextureFormat.ARGB32);
            _renderTexture.antiAliasing = 4;
            _renderTexture.Create();

            CreateCamera();
            CreateLighting();
            CreateCharacter();
        }

        /// <summary>
        /// Update the character preview with new appearance data.
        /// If using a real model: just updates material colors (instant, no GC).
        /// If using procedural fallback: destroys and rebuilds the mesh.
        /// </summary>
        public void UpdateAppearance(AppearanceData data)
        {
            if (_usingModel && _modelCharacter != null && _modelCharacter.IsLoaded)
            {
                // Real model path — just update material colors, instant
                _modelCharacter.UpdateAppearance(data);
                return;
            }

            // Procedural fallback path — destroy and rebuild
            if (_proceduralFallback != null)
            {
                var existingRenderer = _characterGO.GetComponent<SkinnedMeshRenderer>();
                if (existingRenderer != null) DestroyImmediate(existingRenderer);

                for (int i = _characterGO.transform.childCount - 1; i >= 0; i--)
                    DestroyImmediate(_characterGO.transform.GetChild(i).gameObject);

                var spec = BuildSpecFromData(data);
                _proceduralFallback = _characterGO.GetComponent<ProceduralCharacter>();
                if (_proceduralFallback == null)
                    _proceduralFallback = _characterGO.AddComponent<ProceduralCharacter>();
                _proceduralFallback.Build(spec);
                SetLayerRecursive(_characterGO, PreviewLayer);
            }
        }

        public void SetFocusMode(FocusMode mode)
        {
            _currentFocus = mode;
            switch (mode)
            {
                case FocusMode.FullBody:
                    _orbitTarget = new Vector3(0f, 0.9f, 0f);
                    _orbitDistance = 3f;
                    _orbitPitch = 10f;
                    break;
                case FocusMode.Face:
                    _orbitTarget = new Vector3(0f, 1.65f, 0f);
                    _orbitDistance = 1.2f;
                    _orbitPitch = 5f;
                    break;
                case FocusMode.UpperBody:
                    _orbitTarget = new Vector3(0f, 1.2f, 0f);
                    _orbitDistance = 2f;
                    _orbitPitch = 8f;
                    break;
            }
        }

        public void HandleOrbitInput(Rect previewRect)
        {
            var e = Event.current;
            if (e == null) return;

            Vector2 mousePos = e.mousePosition;

            if (e.type == EventType.MouseDown && e.button == 0 && previewRect.Contains(mousePos))
            {
                _isDragging = true;
                _lastMousePos = mousePos;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                _isDragging = false;
            }
            else if (e.type == EventType.MouseDrag && _isDragging)
            {
                Vector2 delta = mousePos - _lastMousePos;
                _orbitYaw += delta.x * 0.5f;
                _orbitPitch = Mathf.Clamp(_orbitPitch - delta.y * 0.3f, -30f, 60f);
                _lastMousePos = mousePos;
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && previewRect.Contains(mousePos))
            {
                _orbitDistance = Mathf.Clamp(_orbitDistance + e.delta.y * 0.1f, 0.8f, 6f);
                e.Use();
            }
        }

        public void Cleanup()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                DestroyImmediate(_renderTexture);
            }
            if (_previewCamera != null) DestroyImmediate(_previewCamera.gameObject);
            if (_characterGO != null) DestroyImmediate(_characterGO);
            if (_keyLight != null) DestroyImmediate(_keyLight.gameObject);
            if (_fillLight != null) DestroyImmediate(_fillLight.gameObject);
            if (_rimLight != null) DestroyImmediate(_rimLight.gameObject);
        }

        // --- MonoBehaviour ---

        private void LateUpdate()
        {
            if (_previewCamera == null) return;

            // If model loaded, adjust orbit target to model center
            if (_usingModel && _modelCharacter != null && _modelCharacter.IsLoaded &&
                _currentFocus == FocusMode.FullBody)
            {
                _orbitTarget = _modelCharacter.GetModelCenter();
            }

            float yawRad = _orbitYaw * Mathf.Deg2Rad;
            float pitchRad = _orbitPitch * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            ) * _orbitDistance;

            _previewCamera.transform.position = _orbitTarget + offset;
            _previewCamera.transform.LookAt(_orbitTarget);
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        // --- Setup ---

        private void CreateCamera()
        {
            var camGO = new GameObject("PreviewCamera");
            camGO.transform.SetParent(transform);
            camGO.layer = PreviewLayer;

            _previewCamera = camGO.AddComponent<Camera>();
            _previewCamera.targetTexture = _renderTexture;
            _previewCamera.cullingMask = 1 << PreviewLayer;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
            _previewCamera.fieldOfView = 30f;
            _previewCamera.nearClipPlane = 0.1f;
            _previewCamera.farClipPlane = 50f;
            _previewCamera.depth = -10;
            _previewCamera.enabled = true;
        }

        private void CreateLighting()
        {
            _keyLight = CreateLight("KeyLight",
                new Vector3(2f, 3f, 2f), Quaternion.Euler(45f, -135f, 0f),
                LightType.Directional, new Color(1f, 0.95f, 0.9f), 1.2f);

            _fillLight = CreateLight("FillLight",
                new Vector3(-3f, 1.5f, 1f), Quaternion.Euler(20f, 60f, 0f),
                LightType.Directional, new Color(0.6f, 0.65f, 0.8f), 0.5f);

            _rimLight = CreateLight("RimLight",
                new Vector3(0f, 2.5f, -3f), Quaternion.Euler(30f, 180f, 0f),
                LightType.Directional, new Color(0.7f, 0.75f, 1f), 0.8f);
        }

        private Light CreateLight(string name, Vector3 position, Quaternion rotation,
            LightType type, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.layer = PreviewLayer;

            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.None;
            return light;
        }

        private void CreateCharacter()
        {
            _characterGO = new GameObject("PreviewCharacter");
            _characterGO.transform.SetParent(transform);
            _characterGO.transform.position = Vector3.zero;
            SetLayerRecursive(_characterGO, PreviewLayer);

            // Try loading real model first
            _modelCharacter = _characterGO.AddComponent<ModelCharacter>();
            _modelCharacter.LoadModel("human_male_base.glb");

            if (_modelCharacter.IsLoaded)
            {
                _usingModel = true;
                _modelCharacter.SetLayer(PreviewLayer);

                // Adjust camera for real model dimensions
                float modelHeight = _modelCharacter.GetModelHeight();
                _orbitTarget = new Vector3(0f, modelHeight * 0.5f, 0f);
                _orbitDistance = modelHeight * 1.8f;

                Debug.Log($"[CharacterPreview] Real model loaded (height: {modelHeight:F2}m)");
            }
            else
            {
                // Fallback to procedural
                Debug.LogWarning("[CharacterPreview] Falling back to procedural character");
                _proceduralFallback = _characterGO.AddComponent<ProceduralCharacter>();
                _proceduralFallback.Build(new CharacterSpec());
                SetLayerRecursive(_characterGO, PreviewLayer);
            }
        }

        private CharacterSpec BuildSpecFromData(AppearanceData data)
        {
            float heightBase = 1.6f;
            float heightRange = 0.5f;
            switch (data.Race)
            {
                case 1: heightBase = 1.7f; heightRange = 0.5f; break;
                case 2: heightBase = 1.5f; heightRange = 0.4f; break;
                case 3: heightBase = 1.55f; heightRange = 0.45f; break;
            }

            string archetype = "humanoid";
            if (data.Build < 0.3f) archetype = "slender";
            else if (data.Build > 0.7f) archetype = "stocky";

            return new CharacterSpec
            {
                Height = heightBase + data.Height * heightRange,
                BodyWidth = 0.3f + data.ShoulderWidth * 0.25f,
                LimbThickness = 0.08f + data.ArmThickness * 0.1f,
                SkinColor = data.SkinColor,
                ShirtColor = new Color(0.3f, 0.3f, 0.6f),
                PantsColor = new Color(0.25f, 0.2f, 0.15f),
                Archetype = archetype
            };
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
