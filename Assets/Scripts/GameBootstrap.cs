using UnityEngine;
using Orlo.Network;
using Orlo.Player;
using Orlo.World;
using Orlo.Audio;
using Orlo.UI;
using Orlo.UI.CharacterCreation;
using Orlo.Proto;
using ProtoAuth = Orlo.Proto.Auth;
using Color = UnityEngine.Color;

namespace Orlo
{
    /// <summary>
    /// Entry point — connects to the server, logs in, selects character, and spawns the player.
    /// Attach to an empty GameObject in the boot scene.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private string serverHost = "play.orlo.games";
        [SerializeField] private int serverPort = 7777;

        [Header("Player")]
        [SerializeField] private string playerName = "Explorer";
        [SerializeField] private GameObject playerPrefab;

        private ulong _sessionId;
        private ulong _accountId;
        private ulong _characterEntityId;
        private CharacterCreationManager _charCreationManager;
        private LoginUI _loginUI;
        private bool _characterSpawned = false;
        private string _launcherToken;

        // Ping every 5 seconds for latency tracking
        private float _pingTimer;
        private const float PingInterval = 5f;

        private void Start()
        {
            Debug.Log("[Orlo] Bootstrapping...");

            ParseCommandLineArgs();

            // Initialize Phase 3 singleton systems
            InitializeWorldSystems();

            NetworkManager.Instance.OnConnected += OnConnected;
            NetworkManager.Instance.OnDisconnected += OnDisconnected;

            PacketHandler.Instance.OnLoginResponse += OnLoginResponse;
            PacketHandler.Instance.OnRegisterResponse += OnRegisterResponse;
            PacketHandler.Instance.OnCharacterSpawn += OnCharacterSpawn;
            PacketHandler.Instance.OnPong += OnPong;

            ShowLoginUI();
        }

        private void ParseCommandLineArgs()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--server" when i + 1 < args.Length:
                        var parts = args[++i].Split(':');
                        serverHost = parts[0];
                        if (parts.Length > 1 && int.TryParse(parts[1], out int port))
                            serverPort = port;
                        Debug.Log($"[Orlo] Server override: {serverHost}:{serverPort}");
                        break;
                    case "--port" when i + 1 < args.Length:
                        if (int.TryParse(args[++i], out int explicitPort))
                            serverPort = explicitPort;
                        Debug.Log($"[Orlo] Port override: {serverPort}");
                        break;
                    case "--token" when i + 1 < args.Length:
                        _launcherToken = args[++i];
                        Debug.Log("[Orlo] Launcher token received");
                        break;
                }
            }

            NetworkManager.Instance.SetServer(serverHost, serverPort);
        }

        private void InitializeWorldSystems()
        {
            // Skybox + day/night
            if (FindFirstObjectByType<SkyboxController>() == null)
            {
                var go = new GameObject("SkyboxController");
                go.AddComponent<SkyboxController>();
            }

            // Weather particles
            if (FindFirstObjectByType<WeatherController>() == null)
            {
                var go = new GameObject("WeatherController");
                go.AddComponent<WeatherController>();
            }

            // Water plane
            if (FindFirstObjectByType<WaterPlane>() == null)
            {
                var go = new GameObject("WaterPlane");
                go.AddComponent<WaterPlane>();
            }

            // Audio manager
            if (AudioManager.Instance == null)
            {
                var go = new GameObject("AudioManager");
                go.AddComponent<AudioManager>();
            }

            // UI systems
            if (TooltipSystem.Instance == null)
            {
                var go = new GameObject("TooltipSystem");
                go.AddComponent<TooltipSystem>();
            }

            if (NotificationUI.Instance == null)
            {
                var go = new GameObject("NotificationUI");
                go.AddComponent<NotificationUI>();
            }

            if (FindFirstObjectByType<MinimapUI>() == null)
            {
                var go = new GameObject("MinimapUI");
                go.AddComponent<MinimapUI>();
            }

            Debug.Log("[Orlo] Phase 3 world systems initialized");
        }

        private void Update()
        {
            if (!NetworkManager.Instance.IsConnected) return;

            _pingTimer -= Time.deltaTime;
            if (_pingTimer <= 0)
            {
                _pingTimer = PingInterval;
                NetworkManager.Instance.Send(PacketBuilder.Ping());
            }
        }

        private string _pendingUsername;
        private string _pendingPassword;

        private void ShowLoginUI()
        {
            if (_loginUI == null)
            {
                var go = new GameObject("LoginUI");
                _loginUI = go.AddComponent<LoginUI>();
            }

            _loginUI.OnLogin = (username, password) =>
            {
                _pendingUsername = username;
                _pendingPassword = password;
                playerName = username;

                if (!NetworkManager.Instance.IsConnected)
                    NetworkManager.Instance.Connect();
                else
                    SendLogin();
            };

            _loginUI.OnRegister = (username, password, email) =>
            {
                _pendingUsername = username;
                _pendingPassword = password;
                playerName = username;
                _pendingRegister = true;

                if (!NetworkManager.Instance.IsConnected)
                    NetworkManager.Instance.Connect();
                else
                    SendRegister();
            };

            // If launched with a token, auto-connect instead of showing login UI
            if (!string.IsNullOrEmpty(_launcherToken))
            {
                Debug.Log("[Orlo] Launcher token detected — auto-connecting...");
                _loginUI.Hide();
                NetworkManager.Instance.Connect();
                return;
            }

            _loginUI.Show();
        }

        private bool _pendingRegister = false;

        private void SendLogin()
        {
            var loginData = !string.IsNullOrEmpty(_launcherToken)
                ? PacketBuilder.LoginRequest("", "", _launcherToken)
                : PacketBuilder.LoginRequest(_pendingUsername, _pendingPassword);
            NetworkManager.Instance.Send(loginData);
        }

        private void SendRegister()
        {
            var data = PacketBuilder.RegisterRequest(_pendingUsername, _pendingPassword, "");
            NetworkManager.Instance.Send(data);
        }

        private void OnConnected()
        {
            Debug.Log("[Orlo] Connected to server");
            if (_pendingRegister)
            {
                _pendingRegister = false;
                SendRegister();
            }
            else if (!string.IsNullOrEmpty(_pendingUsername))
            {
                SendLogin();
            }
        }

        private void OnRegisterResponse(ProtoAuth.RegisterResponse resp)
        {
            if (resp.Success)
            {
                Debug.Log($"[Orlo] Registered account {resp.AccountId} — logging in...");
                _loginUI?.SetStatus("Account created! Logging in...");
                SendLogin();
            }
            else
            {
                Debug.LogError($"[Orlo] Registration failed: {resp.Error}");
                _loginUI?.SetError($"Registration failed: {resp.Error}");
            }
        }

        private void OnLoginResponse(ProtoAuth.LoginResponse resp)
        {
            if (!resp.Success)
            {
                Debug.LogError($"[Orlo] Login failed: {resp.Error}");
                _loginUI?.SetError($"Login failed: {resp.Error}");
                return;
            }

            _sessionId = resp.SessionId;
            _accountId = resp.AccountId;
            Debug.Log($"[Orlo] Logged in, session={_sessionId}, account={_accountId} — requesting character list...");

            _loginUI?.Hide();

            // Request character list — if empty, show creation screen
            NetworkManager.Instance.Send(PacketBuilder.CharacterListRequest(_sessionId));
        }

        /// <summary>
        /// Called by PacketHandler when character list arrives.
        /// If no characters exist, show the creation UI.
        /// If characters exist, auto-select the first one (or show selection UI later).
        /// </summary>
        public void OnCharacterListResponse(int characterCount, ulong firstCharacterId, string firstName, string lastName)
        {
            if (characterCount == 0)
            {
                Debug.Log("[Orlo] No characters found — showing creation screen");
                ShowCharacterCreation();
            }
            else
            {
                Debug.Log($"[Orlo] Found {characterCount} character(s) — selecting '{firstName} {lastName}'");
                playerName = $"{firstName} {lastName}";
                var selectData = PacketBuilder.CharacterSelect(_sessionId, playerName);
                NetworkManager.Instance.Send(selectData);
            }
        }

        private void ShowCharacterCreation()
        {
            if (_charCreationManager == null)
            {
                var go = new GameObject("CharacterCreationManager");
                _charCreationManager = go.AddComponent<CharacterCreationManager>();
            }

            _charCreationManager.OnCreateConfirmed = (data) =>
            {
                Debug.Log($"[Orlo] Creating character: {data.FirstName} {data.LastName}");
                NetworkManager.Instance.Send(PacketBuilder.CharacterCreate(_sessionId, data));
                _charCreationManager.Hide();
            };

            _charCreationManager.Show();
        }

        /// <summary>
        /// Called by PacketHandler when character creation succeeds.
        /// </summary>
        public void OnCharacterCreateResponse(bool success, string error, ulong characterId)
        {
            if (success)
            {
                Debug.Log($"[Orlo] Character created (ID {characterId}) — requesting list to spawn...");
                NetworkManager.Instance.Send(PacketBuilder.CharacterListRequest(_sessionId));
            }
            else
            {
                Debug.LogError($"[Orlo] Character creation failed: {error}");
                NotificationUI.Instance?.ShowError("Creation Failed", error);
                ShowCharacterCreation();
            }
        }

        private void OnCharacterSpawn(ProtoAuth.CharacterSpawnResponse spawn)
        {
            _characterEntityId = spawn.EntityId.Id;
            _characterSpawned = true;
            var pos = new Vector3(
                spawn.Transform.Position.X,
                spawn.Transform.Position.Y,
                spawn.Transform.Position.Z
            );
            var rot = Quaternion.identity;
            if (spawn.Transform.Rotation != null)
            {
                rot = new Quaternion(
                    spawn.Transform.Rotation.X,
                    spawn.Transform.Rotation.Y,
                    spawn.Transform.Rotation.Z,
                    spawn.Transform.Rotation.W
                );
            }

            Debug.Log($"[Orlo] Character spawned at {pos} — creating player object");

            // Show loading screen while terrain loads
            if (LoadingScreenUI.Instance == null)
            {
                var go = new GameObject("LoadingScreenUI");
                go.AddComponent<LoadingScreenUI>();
            }
            LoadingScreenUI.Instance.Show(16); // expect ~16 terrain chunks
            // Loading screen will be hidden by TerrainManager when enough chunks arrive

            // Instantiate player
            GameObject player;
            if (playerPrefab != null)
            {
                player = Instantiate(playerPrefab, pos, rot);
            }
            else
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.transform.SetPositionAndRotation(pos, rot);
                player.AddComponent<CharacterController>();
                player.AddComponent<PlayerController>();
            }

            player.tag = "Player";
            player.name = $"Player_{playerName}";

            // --- Fallback starter environment ---
            // Creates visible ground + lighting while server terrain streams in
            EnsureStarterEnvironment();

            // Set up camera to follow
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.SetParent(player.transform);
                cam.transform.localPosition = new Vector3(0, 2f, -5f);
                cam.transform.localRotation = Quaternion.Euler(15f, 0, 0);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.45f, 0.65f, 0.85f); // sky blue
            }

            // Wire Phase 3 systems to player
            var playerT = player.transform;
            FindFirstObjectByType<WeatherController>()?.SetPlayerTransform(playerT);
            FindFirstObjectByType<WaterPlane>()?.SetPlayerTransform(playerT);
        }

        private void OnPong(ProtoAuth.Pong pong)
        {
            float rtt = (float)(Time.realtimeSinceStartup * 1000 - pong.ClientTime.Ms);
            Debug.Log($"[Network] RTT: {rtt:F1}ms");
        }

        public void ShowLoginAfterLogout()
        {
            _sessionId = 0;
            _accountId = 0;
            _characterSpawned = false;
            _launcherToken = null;
            ShowLoginUI();
        }

        private void OnDisconnected()
        {
            Debug.Log("[Orlo] Disconnected from server");
        }

        /// <summary>
        /// Creates a fallback visible environment (ground plane, sun, skybox)
        /// so the player has something to stand on while server terrain streams in.
        /// Server-streamed terrain chunks will replace this once they arrive.
        /// </summary>
        private void EnsureStarterEnvironment()
        {
            // Sun / directional light
            if (FindFirstObjectByType<Light>() == null)
            {
                var sunGo = new GameObject("Sun");
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1.0f, 0.95f, 0.85f); // warm sunlight
                sun.intensity = 1.2f;
                sun.shadows = LightShadows.Soft;
                sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0);
            }

            // Ground plane (200x200 flat green surface)
            if (GameObject.Find("FallbackGround") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "FallbackGround";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(20f, 1f, 20f); // 200x200m

                var renderer = ground.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.35f, 0.55f, 0.25f); // earthy green
                    renderer.material = mat;
                }
            }

            // Ambient lighting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.6f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.6f, 0.7f, 0.85f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 300f;

            Debug.Log("[Orlo] Fallback starter environment created");
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnected -= OnConnected;
                NetworkManager.Instance.OnDisconnected -= OnDisconnected;
            }
            if (PacketHandler.Instance != null)
            {
                PacketHandler.Instance.OnLoginResponse -= OnLoginResponse;
                PacketHandler.Instance.OnRegisterResponse -= OnRegisterResponse;
                PacketHandler.Instance.OnCharacterSpawn -= OnCharacterSpawn;
                PacketHandler.Instance.OnPong -= OnPong;
            }
        }
    }
}
