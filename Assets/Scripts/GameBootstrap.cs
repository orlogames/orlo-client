using UnityEngine;
using Orlo.Network;
using Orlo.Player;
using Orlo.World;
using Orlo.Audio;
using Orlo.UI;
using Orlo.Animation;
using Orlo.Rendering;
using Orlo.VFX;
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

        /// <summary>The entity ID of the local player — used by combat handlers to identify owner events.</summary>
        public ulong PlayerEntityId => _characterEntityId;
        private CharacterCreationManager _charCreationManager;
        private LoginUI _loginUI;
        private ConnectionStatusUI _connectionUI;
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

            // Create connection status overlay
            if (ConnectionStatusUI.Instance == null)
            {
                var go = new GameObject("ConnectionStatusUI");
                _connectionUI = go.AddComponent<ConnectionStatusUI>();
            }
            else
            {
                _connectionUI = ConnectionStatusUI.Instance;
            }

            NetworkManager.Instance.OnConnected += OnConnected;
            NetworkManager.Instance.OnDisconnected += OnDisconnected;

            PacketHandler.Instance.OnLoginResponse += OnLoginResponse;
            PacketHandler.Instance.OnRegisterResponse += OnRegisterResponse;
            PacketHandler.Instance.OnCharacterSpawn += OnCharacterSpawn;
            PacketHandler.Instance.OnPong += OnPong;

            // If launched with a token, show status overlay and auto-connect
            if (!string.IsNullOrEmpty(_launcherToken))
            {
                Debug.Log("[Orlo] Launcher token detected — auto-connecting...");
                _connectionUI.Show("Connecting to server");
                _connectionUI.OnRetry = () =>
                {
                    _connectionUI.Show("Connecting to server");
                    NetworkManager.Instance.Connect();
                };
                _connectionUI.OnQuit = () =>
                {
                    Debug.Log("[Orlo] User chose to quit — returning to launcher");
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                };
                NetworkManager.Instance.Connect();
            }
            else
            {
                ShowLoginUI();
            }
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
            // Asset loading pipeline (must init before EntityManager/Factory)
            if (AssetLoader.Instance == null)
            {
                var go = new GameObject("AssetLoader");
                go.AddComponent<AssetLoader>();
            }

            // Networked entity lifecycle
            if (EntityManager.Instance == null)
            {
                var go = new GameObject("EntityManager");
                go.AddComponent<EntityManager>();
            }

            // Procedural entity factory (wires into EntityManager on Awake)
            if (ProceduralEntityFactory.Instance == null)
            {
                var go = new GameObject("ProceduralEntityFactory");
                go.AddComponent<ProceduralEntityFactory>();
            }

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

            // Cloud layer (dynamic volumetric clouds with shadows)
            if (FindFirstObjectByType<CloudRenderer>() == null)
            {
                var go = new GameObject("CloudRenderer");
                go.AddComponent<CloudRenderer>();
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

            if (CombatHUD.Instance == null)
            {
                var go = new GameObject("CombatHUD");
                go.AddComponent<CombatHUD>();
            }

            if (CombatFeedback.Instance == null)
            {
                var go = new GameObject("CombatFeedback");
                go.AddComponent<CombatFeedback>();
            }

            // Targeting system (click-to-target + loot pickup)
            if (Player.TargetingSystem.Instance == null)
            {
                var go = new GameObject("TargetingSystem");
                go.AddComponent<Player.TargetingSystem>();
            }

            if (ScreenEffects.Instance == null)
            {
                var go = new GameObject("ScreenEffects");
                go.AddComponent<ScreenEffects>();
            }

            if (ProgressiveDisclosure.Instance == null)
            {
                var go = new GameObject("ProgressiveDisclosure");
                go.AddComponent<ProgressiveDisclosure>();
            }

            if (PlayerProfileUI.Instance == null)
            {
                var go = new GameObject("PlayerProfileUI");
                go.AddComponent<PlayerProfileUI>();
            }

            if (LeaderboardUI.Instance == null)
            {
                var go = new GameObject("LeaderboardUI");
                go.AddComponent<LeaderboardUI>();
            }

            // Register UIs with progressive disclosure system
            // Level 1: movement + combat bar (always visible)
            // Level 3: inventory + minimap
            // Level 5: crafting + TMD
            // Level 8: vendor + trading
            // Level 10: guild + party + full social
            RegisterProgressiveDisclosureUIs();

            Debug.Log("[Orlo] Phase 3 world systems initialized");
        }

        private void RegisterProgressiveDisclosureUIs()
        {
            var pd = ProgressiveDisclosure.Instance;
            if (pd == null) return;

            // Level 3 unlocks
            if (InventoryUI.Instance != null)
                pd.Register(InventoryUI.Instance, "Inventory", 3);
            if (FindFirstObjectByType<MinimapUI>() is MinimapUI minimap)
                pd.Register(minimap, "Minimap", 3);

            // Level 5 unlocks
            if (FindFirstObjectByType<CraftingUI>() is CraftingUI crafting)
                pd.Register(crafting, "Crafting", 5);
            if (FindFirstObjectByType<TMDUI>() is TMDUI tmd)
                pd.Register(tmd, "Terrain Manipulator", 5);

            // Level 8 unlocks
            if (FindFirstObjectByType<ShopUI>() is ShopUI shop)
                pd.Register(shop, "Vendor", 8);

            // Level 10 unlocks
            if (FindFirstObjectByType<PartyUI>() is PartyUI party)
                pd.Register(party, "Party", 10);

            // LeaderboardUI and PlayerProfileUI are always available (info panels)
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

            _loginUI.Show();
        }

        private bool _pendingRegister = false;

        private void SendLogin()
        {
            _connectionUI?.SetStatus("Authenticating");
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
            _connectionUI?.SetStatus("Connected — logging in");

            if (_pendingRegister)
            {
                _pendingRegister = false;
                SendRegister();
            }
            else if (!string.IsNullOrEmpty(_launcherToken))
            {
                Debug.Log("[Orlo] Sending token-based login...");
                SendLogin();
            }
            else if (!string.IsNullOrEmpty(_pendingUsername))
            {
                SendLogin();
            }
            else
            {
                Debug.LogWarning("[Orlo] Connected but no credentials or token — showing login UI");
                _connectionUI?.Hide();
                ShowLoginUI();
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

                bool isTokenMode = !string.IsNullOrEmpty(_launcherToken);
                if (isTokenMode)
                {
                    // Token mode — show error with Quit button since manual login is not possible
                    _connectionUI?.Show("Authentication Failed");
                    _connectionUI?.ShowError($"Login failed: {resp.Error}\nPlease restart from the launcher.", showQuit: true);
                }
                else
                {
                    // Manual login mode — show error on login UI so user can retry with different credentials
                    _connectionUI?.Hide();
                    _loginUI?.SetError($"Login failed: {resp.Error}");
                }
                return;
            }

            _sessionId = resp.SessionId;
            _accountId = resp.AccountId;
            Debug.Log($"[Orlo] Logged in, session={_sessionId}, account={_accountId} — requesting character list...");

            _connectionUI?.SetStatus("Loading characters");
            _loginUI?.Hide();

            // Request character list — if empty, show creation screen
            NetworkManager.Instance.Send(PacketBuilder.CharacterListRequest(_sessionId));
        }

        private CharacterSelectUI _charSelectUI;

        /// <summary>
        /// Called by PacketHandler when character list arrives (full list version).
        /// Shows the character select lobby screen.
        /// </summary>
        public void OnCharacterListReceived(System.Collections.Generic.List<CharacterSelectUI.CharacterEntry> characters, int maxSlots)
        {
            _connectionUI?.Hide();
            Debug.Log($"[Orlo] Received {characters.Count} character(s), max={maxSlots}");

            if (characters.Count == 0)
            {
                // No characters — go straight to creation
                ShowCharacterCreation();
            }
            else if (characters.Count == 1)
            {
                // Single character — auto-select and enter world immediately
                var ch = characters[0];
                string fullName = $"{ch.firstName} {ch.lastName}";
                Debug.Log($"[Orlo] Single character detected — auto-selecting: {fullName} (ID {ch.id})");
                playerName = fullName;
                _connectionUI?.Show("Entering world");
                var selectData = PacketBuilder.CharacterSelect(_sessionId, playerName);
                NetworkManager.Instance.Send(selectData);
            }
            else
            {
                // Multiple characters — show character select / lobby screen
                ShowCharacterSelect(characters, maxSlots);
            }
        }

        private void ShowCharacterSelect(System.Collections.Generic.List<CharacterSelectUI.CharacterEntry> characters, int maxSlots)
        {
            // Hide other UIs
            _loginUI?.Hide();
            _charCreationManager?.Hide();

            if (_charSelectUI == null)
            {
                var go = new GameObject("CharacterSelectUI");
                _charSelectUI = go.AddComponent<CharacterSelectUI>();
            }

            _charSelectUI.SetCharacters(characters, maxSlots);

            _charSelectUI.OnCharacterSelected = (charId, fullName) =>
            {
                Debug.Log($"[Orlo] Selected character: {fullName} (ID {charId})");
                playerName = fullName;
                _connectionUI?.Show("Entering world");
                var selectData = PacketBuilder.CharacterSelect(_sessionId, playerName);
                NetworkManager.Instance.Send(selectData);
            };

            _charSelectUI.OnCreateNew = () =>
            {
                ShowCharacterCreation();
            };

            _charSelectUI.Show();
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
                _connectionUI?.Show("Creating character");
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
                _connectionUI?.SetStatus("Entering world");
                NetworkManager.Instance.Send(PacketBuilder.CharacterListRequest(_sessionId));
            }
            else
            {
                Debug.LogError($"[Orlo] Character creation failed: {error}");
                _connectionUI?.Hide();
                NotificationUI.Instance?.ShowError("Creation Failed", error);
                ShowCharacterCreation();
            }
        }

        private void OnCharacterSpawn(ProtoAuth.CharacterSpawnResponse spawn)
        {
            _connectionUI?.Hide();
            _charSelectUI?.Hide(); // Hide lobby if still visible
            _characterEntityId = spawn.EntityId.Id;
            _characterSpawned = true;

            // Ensure TerrainManager exists to receive terrain chunks from server
            if (FindFirstObjectByType<TerrainManager>() == null)
            {
                var tmGo = new GameObject("TerrainManager");
                tmGo.AddComponent<TerrainManager>();
                Debug.Log("[Orlo] TerrainManager created");
            }

            // Initialize skybox and atmosphere
            var skybox = FindFirstObjectByType<SkyboxController>();
            if (skybox != null)
            {
                skybox.ForceInitialize();
                Debug.Log("[Orlo] Skybox initialized");
            }
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

            // Instantiate player with real model
            GameObject player;
            if (playerPrefab != null)
            {
                player = Instantiate(playerPrefab, pos, rot);
            }
            else
            {
                player = new GameObject("PlayerCharacter");
                player.transform.SetPositionAndRotation(pos, rot);

                // Load the 3D character model
                var modelChar = player.AddComponent<ModelCharacter>();
                modelChar.LoadModel("human_male_base.glb");

                // Attach runtime skeleton for procedural animation
                if (modelChar.IsLoaded && modelChar.GetModelRoot() != null)
                    RuntimeRigBuilder.BuildHumanoidRig(modelChar.GetModelRoot(), modelChar.GetModelHeight());

                // Add controller components
                var cc = player.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.center = Vector3.up * 0.9f;
                cc.radius = 0.3f;
                player.AddComponent<PlayerController>();

                // Add procedural animation driver (reads movement state from PlayerController)
                player.AddComponent<CharacterAnimator>();
            }

            player.tag = "Player";
            player.name = $"Player_{playerName}";

            // --- Fallback starter environment ---
            // Creates visible ground + lighting while server terrain streams in
            EnsureStarterEnvironment();

            // Set up orbit camera
            var cam = Camera.main;
            if (cam != null)
            {
                // Remove any parent — orbit camera manages its own position
                cam.transform.SetParent(null);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.65f, 0.6f, 0.5f); // warm fog color (matches atmosphere)

                // Add OrbitCamera component
                var orbit = cam.gameObject.GetComponent<Orlo.Player.OrbitCamera>();
                if (orbit == null)
                    orbit = cam.gameObject.AddComponent<Orlo.Player.OrbitCamera>();
                orbit.SetTarget(player.transform);
            }

            // Wire Phase 3 systems to player
            var playerT = player.transform;
            FindFirstObjectByType<WeatherController>()?.SetPlayerTransform(playerT);
            FindFirstObjectByType<WaterPlane>()?.SetPlayerTransform(playerT);
            FindFirstObjectByType<CloudRenderer>()?.SetPlayerTransform(playerT);

            // Wire cloud + god ray cross-system updates
            WireCloudAndGodRays();
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
            if (!_characterSpawned)
            {
                bool isTokenMode = !string.IsNullOrEmpty(_launcherToken);
                _connectionUI?.Show("Disconnected");
                _connectionUI?.ShowError("Lost connection to the game server.", showQuit: isTokenMode);

                if (!isTokenMode)
                {
                    // In manual mode, also show login UI so user can reconnect
                    ShowLoginUI();
                }
            }
        }

        /// <summary>
        /// Creates a fallback visible environment (ground plane, sun, skybox)
        /// so the player has something to stand on while server terrain streams in.
        /// Server-streamed terrain chunks will replace this once they arrive.
        /// </summary>
        private void EnsureStarterEnvironment()
        {
            // Sun / directional light — golden hour angle for warm cinematic look
            if (FindFirstObjectByType<Light>() == null)
            {
                var sunGo = new GameObject("Sun");
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1.0f, 0.92f, 0.75f); // warm golden sun
                sun.intensity = 1.5f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
                sunGo.transform.rotation = Quaternion.Euler(35f, -30f, 0); // more frontal golden hour angle
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

            // Ambient lighting — trilight for warm golden hour atmosphere
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.4f, 0.5f, 0.7f);       // cooler blue shadows
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.48f, 0.35f); // warmer amber midtone
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.38f, 0.25f);  // warmer bounce light
            RenderSettings.ambientIntensity = 1.1f;

            // Atmospheric fog — warm haze for depth
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.70f, 0.62f, 0.48f); // warmer golden haze
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 300f;

            // Enable HDR on main camera + attach post-processing
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.allowHDR = true;
                mainCam.allowMSAA = true;

                // Add bloom + color grading post-processing
                if (mainCam.GetComponent<PostProcessSetup>() == null)
                    mainCam.gameObject.AddComponent<PostProcessSetup>();

                // Add god rays (volumetric light shafts through clouds)
                if (mainCam.GetComponent<GodRaysEffect>() == null)
                    mainCam.gameObject.AddComponent<GodRaysEffect>();

                // Add golden hour dust motes (floating particles in god ray shafts)
                if (mainCam.GetComponent<GoldenHourParticles>() == null)
                    mainCam.gameObject.AddComponent<GoldenHourParticles>();
            }

            Debug.Log("[Orlo] Fallback starter environment created");
        }

        // --- Cloud + God Ray wiring ---
        private CloudRenderer _cloudRenderer;
        private GodRaysEffect _godRaysEffect;
        private SkyboxController _skyboxController;
        private WeatherController _weatherController;

        private void WireCloudAndGodRays()
        {
            _cloudRenderer = FindFirstObjectByType<CloudRenderer>();
            _godRaysEffect = Camera.main?.GetComponent<GodRaysEffect>();
            _skyboxController = FindFirstObjectByType<SkyboxController>();
            _weatherController = FindFirstObjectByType<WeatherController>();

            Debug.Log($"[Orlo] Cloud/GodRay wiring: clouds={_cloudRenderer != null}, " +
                      $"rays={_godRaysEffect != null}, sky={_skyboxController != null}, " +
                      $"weather={_weatherController != null}");
        }

        private void LateUpdate()
        {
            // Drive cloud density and wind from weather state
            if (_cloudRenderer != null && _weatherController != null)
            {
                _cloudRenderer.SetCloudDensity(_weatherController.CloudDensity);
                _cloudRenderer.SetWind(_weatherController.WindDirection, _weatherController.WindSpeed);
            }

            // Drive cloud sun direction from skybox
            if (_cloudRenderer != null && _skyboxController != null)
            {
                _cloudRenderer.SetSunDirection(_skyboxController.SunDirection);
                _cloudRenderer.SetSunColor(_skyboxController.SunColor);
            }

            // Drive god rays from cloud coverage and sun state
            if (_godRaysEffect != null)
            {
                if (_cloudRenderer != null)
                    _godRaysEffect.SetGodRayFactor(_cloudRenderer.GodRayFactor);

                if (_skyboxController != null)
                    _godRaysEffect.SetSunAboveHorizon(_skyboxController.IsSunAboveHorizon);

                // Suppress god rays during heavy overcast
                if (_weatherController != null && _weatherController.IsOvercast)
                    _godRaysEffect.SetGodRayFactor(0f);
            }
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
