using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI
{
    public class QuestLogUI : MonoBehaviour
    {
        private bool _visible;
        private Rect _windowRect;
        private int _selectedIndex = -1;
        private Vector2 _listScroll;
        private bool _showCompleted;

        public struct QuestEntry
        {
            public string Id, Name, Description;
            public List<ObjectiveEntry> Objectives;
            public int XpReward;
            public bool Complete, Tracked;
        }

        public struct ObjectiveEntry
        {
            public string Description;
            public int Current, Required;
        }

        private readonly List<QuestEntry> _activeQuests = new();
        private readonly List<string> _completedIds = new();

        private void Awake()
        {
            _windowRect = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 175, 500, 350);

            // Mock data for testing
            _activeQuests.Add(new QuestEntry
            {
                Id = "quest_first_steps", Name = "First Steps",
                Description = "Explore the area around the landing site. The world is vast — take your first steps.",
                Objectives = new List<ObjectiveEntry>
                {
                    new ObjectiveEntry { Description = "Visit 3 different cells", Current = 1, Required = 3 }
                },
                XpReward = 500, Complete = false, Tracked = true
            });
            _activeQuests.Add(new QuestEntry
            {
                Id = "quest_resource_survey", Name = "Resource Survey",
                Description = "The surrounding terrain is rich with raw materials. Survey the deposits.",
                Objectives = new List<ObjectiveEntry>
                {
                    new ObjectiveEntry { Description = "Gather Crystal Shards", Current = 3, Required = 10 },
                    new ObjectiveEntry { Description = "Gather Iron Ore", Current = 0, Required = 5 }
                },
                XpReward = 750, Complete = false, Tracked = false
            });
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.J)) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            GUI.skin.window.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.15f, 0.92f));
            _windowRect = GUI.Window(203, _windowRect, DrawWindow, "Quest Log");
        }

        private void DrawWindow(int id)
        {
            // Tab: Active / Completed
            if (GUI.Button(new Rect(10, 22, 80, 22), _showCompleted ? "Active" : "[Active]"))
                _showCompleted = false;
            if (GUI.Button(new Rect(95, 22, 80, 22), _showCompleted ? "[Completed]" : "Completed"))
                _showCompleted = true;

            // Left panel — quest list
            _listScroll = GUI.BeginScrollView(new Rect(10, 50, 150, 280), _listScroll, new Rect(0, 0, 130, _activeQuests.Count * 28));
            for (int i = 0; i < _activeQuests.Count; i++)
            {
                var q = _activeQuests[i];
                bool selected = _selectedIndex == i;
                var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = 11 };
                if (selected) style.normal.textColor = Color.yellow;

                string prefix = q.Complete ? "[Done] " : q.Tracked ? "> " : "  ";
                if (GUI.Button(new Rect(0, i * 26, 130, 24), prefix + q.Name, style))
                    _selectedIndex = i;
            }
            GUI.EndScrollView();

            // Right panel — quest detail
            if (_selectedIndex >= 0 && _selectedIndex < _activeQuests.Count)
            {
                var q = _activeQuests[_selectedIndex];
                float y = 50;

                var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(170, y, 320, 22), q.Name, titleStyle);
                y += 24;

                GUI.Label(new Rect(170, y, 320, 40), q.Description, new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 11 });
                y += 46;

                GUI.Label(new Rect(170, y, 320, 20), "Objectives:");
                y += 20;

                foreach (var obj in q.Objectives)
                {
                    bool done = obj.Current >= obj.Required;
                    string check = done ? "[x]" : "[ ]";
                    var objStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 11,
                        normal = { textColor = done ? Color.green : Color.white }
                    };
                    GUI.Label(new Rect(180, y, 300, 18), $"{check} {obj.Description} ({obj.Current}/{obj.Required})", objStyle);
                    y += 18;
                }

                y += 10;
                GUI.Label(new Rect(170, y, 320, 20), $"Rewards: {q.XpReward} XP",
                    new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.85f, 0.2f) } });
                y += 24;

                string trackLabel = q.Tracked ? "Untrack" : "Track";
                if (GUI.Button(new Rect(170, y, 70, 22), trackLabel))
                {
                    var updated = q;
                    updated.Tracked = !q.Tracked;
                    _activeQuests[_selectedIndex] = updated;
                }
            }

            if (GUI.Button(new Rect(440, 2, 50, 18), "Close")) _visible = false;
            GUI.DragWindow(new Rect(0, 0, 440, 20));
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w * h; i++) tex.SetPixel(i % w, i / w, col);
            tex.Apply();
            return tex;
        }
    }
}
