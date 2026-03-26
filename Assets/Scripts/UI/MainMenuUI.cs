using UnityEngine;

namespace Orlo.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        private bool _visible;
        private Texture2D _overlay;
        private bool _settingsOpen;

        private void Awake()
        {
            _overlay = new Texture2D(1, 1);
            _overlay.SetPixel(0, 0, new Color(0, 0, 0, 0.7f));
            _overlay.Apply();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_settingsOpen)
                {
                    _settingsOpen = false;
                    var settings = FindFirstObjectByType<Settings.SettingsUI>();
                    settings?.Hide();
                }
                else
                {
                    _visible = !_visible;
                    Cursor.lockState = _visible ? CursorLockMode.None : CursorLockMode.Locked;
                    Cursor.visible = _visible;
                }
            }
        }

        private void OnGUI()
        {
            if (!_visible || _settingsOpen) return;

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _overlay);

            float bw = 200, bh = 40, gap = 8;
            float totalH = 9 * bh + 8 * gap;
            float x = Screen.width / 2 - bw / 2;
            float y = Screen.height / 2 - totalH / 2;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(x, y - 50, bw, 40), "ORLO", titleStyle);

            if (GUI.Button(new Rect(x, y, bw, bh), "Resume")) { _visible = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, bw, bh), "Character [P]")) { _visible = false; Cursor.lockState = CursorLockMode.Locked; }
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, bw, bh), "Inventory [I]")) { _visible = false; Cursor.lockState = CursorLockMode.Locked; }
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, bw, bh), "Skills [K]")) { _visible = false; Cursor.lockState = CursorLockMode.Locked; }
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, bw, bh), "Quests [J]")) { _visible = false; Cursor.lockState = CursorLockMode.Locked; }
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, bw, bh), "Crafting [C]")) { _visible = false; Cursor.lockState = CursorLockMode.Locked; }
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, bw, bh), "Settings"))
            {
                _settingsOpen = true;
                var settings = FindFirstObjectByType<Settings.SettingsUI>();
                if (settings == null)
                {
                    var go = new GameObject("SettingsUI");
                    settings = go.AddComponent<Settings.SettingsUI>();
                }
                settings.Show();
            }
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, bw, bh), "Logout"))
            {
                Orlo.Network.NetworkManager.Instance?.Disconnect();
                FindFirstObjectByType<GameBootstrap>()?.ShowLoginAfterLogout();
            }
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, bw, bh), "Quit"))
            {
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #else
                Application.Quit();
                #endif
            }
        }
    }
}
