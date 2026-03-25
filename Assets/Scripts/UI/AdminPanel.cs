using UnityEngine;
using Orlo.Network;

namespace Orlo.UI
{
    /// <summary>
    /// Admin control panel — toggled with F1.
    /// Shows speed/fly/tool controls when the logged-in account has admin privileges.
    /// </summary>
    public class AdminPanel : MonoBehaviour
    {
        public static AdminPanel Instance { get; private set; }

        private bool _isAdmin;
        private bool _visible;

        // Current state
        private float _runSpeed;
        private bool _flyEnabled;
        private float _toolPower = 1f;
        private bool _godMode;
        private string _statusMessage = "";

        // Input fields
        private string _speedInput = "50";
        private string _toolPowerInput = "10";
        private string _teleportX = "0";
        private string _teleportY = "100";
        private string _teleportZ = "0";
        private string _spawnToolId = "pick_master";
        private string _spawnToolQty = "1";

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (_isAdmin && Input.GetKeyDown(KeyCode.F1))
            {
                _visible = !_visible;
                Cursor.lockState = _visible ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = _visible;
            }
        }

        public void SetAdminState(bool isAdmin, float runSpeed, bool flyEnabled, float toolPower, bool godMode)
        {
            _isAdmin = isAdmin;
            _runSpeed = runSpeed;
            _flyEnabled = flyEnabled;
            _toolPower = toolPower;
            _godMode = godMode;

            if (_runSpeed > 0) _speedInput = _runSpeed.ToString("F0");
            if (_toolPower > 0) _toolPowerInput = _toolPower.ToString("F1");
        }

        public void SetStatus(string msg)
        {
            _statusMessage = msg;
        }

        public bool IsAdmin => _isAdmin;
        public float RunSpeed => _runSpeed;
        public bool FlyEnabled => _flyEnabled;
        public float ToolPower => _toolPower;

        private void OnGUI()
        {
            if (!_isAdmin || !_visible) return;

            float w = 340, h = 480;
            var rect = new Rect(10, 10, w, h);
            GUI.Box(rect, "");
            GUILayout.BeginArea(rect);
            GUILayout.Space(10);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Admin Panel (F1)", titleStyle);
            GUILayout.Space(10);

            // ─── Speed ────────────────────────────────────────
            GUILayout.Label("─── Movement Speed ───");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Speed (m/s):", GUILayout.Width(90));
            _speedInput = GUILayout.TextField(_speedInput, GUILayout.Width(60));
            if (GUILayout.Button("Set", GUILayout.Width(50)))
            {
                if (float.TryParse(_speedInput, out float spd))
                    SendSetSpeed(spd);
            }
            if (GUILayout.Button("Reset", GUILayout.Width(60)))
            {
                SendSetSpeed(0);
            }
            GUILayout.EndHorizontal();

            string speedLabel = _runSpeed > 0 ? $"Current: {_runSpeed:F1} m/s" : "Current: default";
            GUILayout.Label(speedLabel);
            GUILayout.Space(5);

            // ─── Fly ──────────────────────────────────────────
            GUILayout.Label("─── Fly Mode ───");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_flyEnabled ? "Disable Fly" : "Enable Fly", GUILayout.Height(30)))
            {
                SendSetFly(!_flyEnabled);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            // ─── Tool Power ───────────────────────────────────
            GUILayout.Label("─── Tool Power ───");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Power:", GUILayout.Width(50));
            _toolPowerInput = GUILayout.TextField(_toolPowerInput, GUILayout.Width(60));
            if (GUILayout.Button("Set", GUILayout.Width(50)))
            {
                if (float.TryParse(_toolPowerInput, out float pwr))
                    SendSetToolPower(pwr);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Current: {_toolPower:F1}x");
            GUILayout.Space(5);

            // ─── God Mode ─────────────────────────────────────
            GUILayout.Label("─── God Mode ───");
            if (GUILayout.Button(_godMode ? "Disable God Mode" : "Enable God Mode", GUILayout.Height(30)))
            {
                SendGodMode(!_godMode);
            }
            GUILayout.Space(5);

            // ─── Teleport ─────────────────────────────────────
            GUILayout.Label("─── Teleport ───");
            GUILayout.BeginHorizontal();
            GUILayout.Label("X:", GUILayout.Width(15));
            _teleportX = GUILayout.TextField(_teleportX, GUILayout.Width(60));
            GUILayout.Label("Y:", GUILayout.Width(15));
            _teleportY = GUILayout.TextField(_teleportY, GUILayout.Width(60));
            GUILayout.Label("Z:", GUILayout.Width(15));
            _teleportZ = GUILayout.TextField(_teleportZ, GUILayout.Width(60));
            if (GUILayout.Button("Go", GUILayout.Width(35)))
            {
                if (float.TryParse(_teleportX, out float tx) &&
                    float.TryParse(_teleportY, out float ty) &&
                    float.TryParse(_teleportZ, out float tz))
                {
                    SendTeleport(tx, ty, tz);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            // ─── Spawn Tool ───────────────────────────────────
            GUILayout.Label("─── Spawn Tool ───");
            GUILayout.BeginHorizontal();
            GUILayout.Label("ID:", GUILayout.Width(20));
            _spawnToolId = GUILayout.TextField(_spawnToolId, GUILayout.Width(140));
            GUILayout.Label("Qty:", GUILayout.Width(25));
            _spawnToolQty = GUILayout.TextField(_spawnToolQty, GUILayout.Width(40));
            if (GUILayout.Button("Spawn", GUILayout.Width(55)))
            {
                if (uint.TryParse(_spawnToolQty, out uint qty))
                    SendSpawnTool(_spawnToolId, qty);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // ─── Status ───────────────────────────────────────
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                var statusStyle = new GUIStyle(GUI.skin.label)
                {
                    normal = { textColor = Color.green },
                    alignment = TextAnchor.MiddleCenter
                };
                GUILayout.Label(_statusMessage, statusStyle);
            }

            GUILayout.EndArea();
        }

        private void SendSetSpeed(float speed)
        {
            var data = PacketBuilder.AdminSetSpeed(speed);
            NetworkManager.Instance.Send(data);
        }

        private void SendSetFly(bool enabled)
        {
            var data = PacketBuilder.AdminSetFly(enabled);
            NetworkManager.Instance.Send(data);
        }

        private void SendSetToolPower(float power)
        {
            var data = PacketBuilder.AdminSetToolPower(power);
            NetworkManager.Instance.Send(data);
        }

        private void SendGodMode(bool enabled)
        {
            var data = PacketBuilder.AdminGodMode(enabled);
            NetworkManager.Instance.Send(data);
        }

        private void SendTeleport(float x, float y, float z)
        {
            var data = PacketBuilder.AdminTeleport(x, y, z);
            NetworkManager.Instance.Send(data);
        }

        private void SendSpawnTool(string toolId, uint qty)
        {
            var data = PacketBuilder.AdminSpawnTool(toolId, qty);
            NetworkManager.Instance.Send(data);
        }
    }
}
