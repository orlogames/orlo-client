using UnityEngine;
using Orlo.Network;

namespace Orlo.UI
{
    /// <summary>
    /// Simple debug HUD showing connection status, position, and RTT.
    /// Uses OnGUI for rapid prototyping — will be replaced with proper UI later.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        private float _rtt;
        private string _lastNotification;
        private float _notificationTimer;

        private void OnEnable()
        {
            if (PacketHandler.Instance != null)
                PacketHandler.Instance.OnPong += OnPong;
        }

        private void OnDisable()
        {
            if (PacketHandler.Instance != null)
                PacketHandler.Instance.OnPong -= OnPong;
        }

        private void Update()
        {
            if (_notificationTimer > 0)
                _notificationTimer -= Time.deltaTime;
        }

        private void OnPong(Auth.Pong pong)
        {
            _rtt = (float)(Time.realtimeSinceStartup * 1000 - pong.ClientTime.Ms);
        }

        public void ShowNotification(string text, float duration = 5f)
        {
            _lastNotification = text;
            _notificationTimer = duration;
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };

            float y = 10;

            // Connection status
            bool connected = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
            GUI.Label(new Rect(10, y, 300, 20),
                connected ? $"<color=lime>Connected</color> | RTT: {_rtt:F1}ms" : "<color=red>Disconnected</color>",
                style);
            y += 22;

            // Player position
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var pos = player.transform.position;
                GUI.Label(new Rect(10, y, 400, 20),
                    $"Position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})", style);
                y += 22;
            }

            // Content reveal notification
            if (_notificationTimer > 0 && !string.IsNullOrEmpty(_lastNotification))
            {
                var notifStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.yellow }
                };
                float alpha = Mathf.Min(1f, _notificationTimer);
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.Box(new Rect(Screen.width / 2 - 200, 50, 400, 50), _lastNotification, notifStyle);
                GUI.color = Color.white;
            }
        }
    }
}
