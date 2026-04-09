using System.Collections.Generic;
using UnityEngine;
using Orlo.UI.TMD;

namespace Orlo.UI
{
    /// <summary>
    /// On-screen quest objective tracker. Shows the currently tracked quest's name
    /// and up to 5 objectives with completion status in the top-right area.
    /// TMD-styled using DrawPanel + DrawScanlines. Registers with HUDLayout for dragging.
    /// </summary>
    public class QuestTrackerHUD : MonoBehaviour
    {
        public static QuestTrackerHUD Instance { get; private set; }

        public struct ObjectiveData
        {
            public string Description;
            public int Current;
            public int Target;
            public bool Completed;
        }

        private const float PanelWidth = 200f;
        private const float Padding = 8f;
        private const float TitleHeight = 22f;
        private const float ObjectiveHeight = 18f;
        private const int MaxObjectives = 5;
        private const string HUDKey = "QuestTracker";

        private string _questId;
        private string _questName;
        private readonly List<ObjectiveData> _objectives = new();
        private bool _tracking;
        private Vector2 _position;

        private RacePalette P => TMDTheme.Instance?.Palette ?? RacePalette.Solari;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Register with HUDLayout for draggable positioning (top-right, below minimap area)
            float defaultX = Screen.width - PanelWidth - 12f;
            float defaultY = 230f; // below minimap
            float height = TitleHeight + Padding * 2 + ObjectiveHeight * MaxObjectives;
            _position = HUDLayout.Instance != null
                ? HUDLayout.Instance.Register(HUDKey, "Quest Tracker", defaultX, defaultY, PanelWidth, height)
                : new Vector2(defaultX, defaultY);
        }

        /// <summary>Set the currently tracked quest and its objectives.</summary>
        public void SetTrackedQuest(string questId, string questName, ObjectiveData[] objectives)
        {
            _questId = questId;
            _questName = questName;
            _objectives.Clear();
            if (objectives != null)
            {
                for (int i = 0; i < objectives.Length && i < MaxObjectives; i++)
                    _objectives.Add(objectives[i]);
            }
            _tracking = true;
            UpdateHUDSize();
        }

        /// <summary>Update a specific objective's progress for the tracked quest.</summary>
        public void UpdateObjective(string questId, int index, int current, int target)
        {
            if (!_tracking || _questId != questId) return;
            if (index < 0 || index >= _objectives.Count) return;

            var obj = _objectives[index];
            obj.Current = current;
            obj.Target = target;
            obj.Completed = current >= target;
            _objectives[index] = obj;
        }

        /// <summary>Clear the tracked quest (nothing displayed).</summary>
        public void ClearTrackedQuest()
        {
            _tracking = false;
            _questId = null;
            _questName = null;
            _objectives.Clear();
        }

        private void UpdateHUDSize()
        {
            float height = TitleHeight + Padding * 2 + ObjectiveHeight * _objectives.Count;
            HUDLayout.Instance?.UpdateSize(HUDKey, PanelWidth, height);
        }

        private void OnGUI()
        {
            if (!GameBootstrap.InWorld) return;
            if (!_tracking || string.IsNullOrEmpty(_questName)) return;

            float s = UIScaler.Scale;
            var p = P;

            // Get position from HUDLayout if available
            if (HUDLayout.Instance != null)
                _position = HUDLayout.Instance.GetPosition(HUDKey);

            float w = PanelWidth * s;
            float objCount = Mathf.Min(_objectives.Count, MaxObjectives);
            float h = (TitleHeight + Padding * 2 + ObjectiveHeight * objCount) * s;

            Rect panelRect = new Rect(_position.x, _position.y, w, h);

            // TMD panel background
            TMDTheme.DrawPanel(panelRect);

            // Scanline overlay
            TMDTheme.DrawScanlines(panelRect);

            // Quest name (race Primary color)
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(11),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = p.Primary }
            };

            Rect titleRect = new Rect(
                panelRect.x + Padding * s,
                panelRect.y + Padding * 0.5f * s,
                w - Padding * 2 * s,
                TitleHeight * s);
            GUI.Label(titleRect, _questName, titleStyle);

            // Underline below title
            GUI.color = new Color(p.Primary.r, p.Primary.g, p.Primary.b, 0.3f);
            GUI.DrawTexture(new Rect(
                panelRect.x + Padding * 0.5f * s,
                titleRect.yMax,
                w - Padding * s,
                1), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Objectives
            var objStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(9),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                richText = false
            };

            float yOffset = titleRect.yMax + 4 * s;

            for (int i = 0; i < _objectives.Count && i < MaxObjectives; i++)
            {
                var obj = _objectives[i];

                string checkbox = obj.Completed ? "[x]" : "[ ]";
                string progress = obj.Target > 1 && !obj.Completed
                    ? $" ({obj.Current}/{obj.Target})"
                    : "";
                string line = $"{checkbox} {obj.Description}{progress}";

                objStyle.normal.textColor = obj.Completed
                    ? new Color(p.Text.r, p.Text.g, p.Text.b, 0.5f) // dimmed when done
                    : p.Text;

                Rect objRect = new Rect(
                    panelRect.x + Padding * s,
                    yOffset,
                    w - Padding * 2 * s,
                    ObjectiveHeight * s);

                GUI.Label(objRect, line, objStyle);
                yOffset += ObjectiveHeight * s;
            }
        }
    }
}
