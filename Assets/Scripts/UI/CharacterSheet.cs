using UnityEngine;

namespace Orlo.UI
{
    public class CharacterSheet : MonoBehaviour
    {
        public static CharacterSheet Instance { get; private set; }

        private bool _visible;
        private Vector2 _scrollPos;

        // Mock data — replaced by server ProgressionSnapshot
        private int _level = 1;
        private ulong _currentXp = 0;
        private ulong _xpToNext = 283;
        private int _statPointsAvailable = 5;
        private int[] _stats = { 5, 5, 5, 5, 5 }; // STR, AGI, VIT, INT, PER
        private readonly string[] _statNames = { "Strength", "Agility", "Vitality", "Intelligence", "Perception" };

        private Rect _windowRect;

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
            _windowRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 225, 300, 450);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P)) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            GUI.skin.window.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.15f, 0.92f));
            _windowRect = GUI.Window(201, _windowRect, DrawWindow, "Character");
        }

        private void DrawWindow(int id)
        {
            float y = 22;

            // Level + XP
            GUI.Label(new Rect(10, y, 280, 20), $"Level {_level}");
            y += 22;
            float xpPct = _xpToNext > 0 ? (float)_currentXp / _xpToNext : 0;
            GUI.Box(new Rect(10, y, 280, 18), "");
            GUI.DrawTexture(new Rect(11, y + 1, 278 * xpPct, 16), MakeTex(1, 1, new Color(0.2f, 0.6f, 1f)));
            GUI.Label(new Rect(10, y, 280, 18), $"  XP: {_currentXp} / {_xpToNext}");
            y += 26;

            // Stat points
            var goldStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
            GUI.Label(new Rect(10, y, 280, 20), $"Available Stat Points: {_statPointsAvailable}", goldStyle);
            y += 26;

            // Stats
            for (int i = 0; i < 5; i++)
            {
                GUI.Label(new Rect(10, y, 140, 22), $"{_statNames[i]}: {_stats[i]}");
                if (_statPointsAvailable > 0 && GUI.Button(new Rect(200, y, 30, 20), "+"))
                {
                    _stats[i]++;
                    _statPointsAvailable--;
                }
                if (_stats[i] > 1 && GUI.Button(new Rect(235, y, 30, 20), "-"))
                {
                    _stats[i]--;
                    _statPointsAvailable++;
                }
                y += 24;
            }

            y += 10;
            GUI.Label(new Rect(10, y, 280, 20), "--- Derived Stats ---");
            y += 22;

            float maxHp = 100 + _stats[2] * 20;
            float dps = _stats[0] * 2.5f;
            float defense = _stats[2] * 1.5f + _stats[0] * 0.5f;
            float moveSpeed = 5f + _stats[1] * 0.2f;
            float gatherSpeed = 1f + _stats[3] * 0.1f;

            GUI.Label(new Rect(10, y, 280, 20), $"Max HP: {maxHp:F0}"); y += 20;
            GUI.Label(new Rect(10, y, 280, 20), $"Base DPS: {dps:F1}"); y += 20;
            GUI.Label(new Rect(10, y, 280, 20), $"Defense: {defense:F1}"); y += 20;
            GUI.Label(new Rect(10, y, 280, 20), $"Move Speed: {moveSpeed:F1}"); y += 20;
            GUI.Label(new Rect(10, y, 280, 20), $"Gather Speed: {gatherSpeed:F1}x"); y += 20;

            if (GUI.Button(new Rect(240, 2, 50, 18), "Close")) _visible = false;
            GUI.DragWindow(new Rect(0, 0, 240, 20));
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w * h; i++) tex.SetPixel(i % w, i / w, col);
            tex.Apply();
            return tex;
        }

        public void UpdateFromServer(int level, ulong xp, ulong xpNext, int statPts, int[] stats)
        {
            _level = level;
            _currentXp = xp;
            _xpToNext = xpNext;
            _statPointsAvailable = statPts;
            if (stats.Length == 5) _stats = stats;
        }
    }
}
