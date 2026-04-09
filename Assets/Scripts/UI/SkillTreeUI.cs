using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI
{
    public class SkillTreeUI : MonoBehaviour
    {
        private bool _visible;
        private int _activeTab; // 0=Combat, 1=Survival, 2=Exploration
        private readonly string[] _tabNames = { "Combat", "Survival", "Exploration" };
        private Rect _windowRect;
        private int _skillPoints = 3;

        private struct SkillData
        {
            public uint Id;
            public string Name;
            public int Rank;
            public int MaxRank;
            public int Tab;
            public uint[] Prereqs;
            public string Bonus;
        }

        private readonly List<SkillData> _skills = new();

        private void Awake()
        {
            _windowRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 175, 400, 350);
            InitSkills();
        }

        private void InitSkills()
        {
            // Combat
            _skills.Add(new SkillData { Id = 100, Name = "Melee Mastery", Rank = 0, MaxRank = 5, Tab = 0, Prereqs = new uint[0], Bonus = "+5% melee dmg/rank" });
            _skills.Add(new SkillData { Id = 101, Name = "Ranged Prof.", Rank = 0, MaxRank = 5, Tab = 0, Prereqs = new uint[0], Bonus = "+5% ranged dmg/rank" });
            _skills.Add(new SkillData { Id = 102, Name = "Heavy Armor", Rank = 0, MaxRank = 5, Tab = 0, Prereqs = new uint[] { 100 }, Bonus = "+4% phys resist/rank" });
            _skills.Add(new SkillData { Id = 103, Name = "Shield Block", Rank = 0, MaxRank = 5, Tab = 0, Prereqs = new uint[] { 102 }, Bonus = "+3% block/rank" });
            _skills.Add(new SkillData { Id = 104, Name = "Critical Strike", Rank = 0, MaxRank = 5, Tab = 0, Prereqs = new uint[] { 101 }, Bonus = "+2% crit/rank" });
            _skills.Add(new SkillData { Id = 105, Name = "Combat Reflexes", Rank = 0, MaxRank = 5, Tab = 0, Prereqs = new uint[] { 100, 101 }, Bonus = "+2% dodge/rank" });
            // Survival
            _skills.Add(new SkillData { Id = 200, Name = "Eff. Gathering", Rank = 0, MaxRank = 5, Tab = 1, Prereqs = new uint[0], Bonus = "+10% gather spd/rank" });
            _skills.Add(new SkillData { Id = 201, Name = "Adv. Crafting", Rank = 0, MaxRank = 5, Tab = 1, Prereqs = new uint[0], Bonus = "+8% craft spd/rank" });
            _skills.Add(new SkillData { Id = 202, Name = "Cooking", Rank = 0, MaxRank = 5, Tab = 1, Prereqs = new uint[] { 201 }, Bonus = "+10% food dur/rank" });
            _skills.Add(new SkillData { Id = 203, Name = "Herbalism", Rank = 0, MaxRank = 5, Tab = 1, Prereqs = new uint[] { 200 }, Bonus = "+5% herb bonus/rank" });
            _skills.Add(new SkillData { Id = 204, Name = "Mining Expert", Rank = 0, MaxRank = 5, Tab = 1, Prereqs = new uint[] { 200 }, Bonus = "+5% ore bonus/rank" });
            _skills.Add(new SkillData { Id = 205, Name = "Salvaging", Rank = 0, MaxRank = 5, Tab = 1, Prereqs = new uint[] { 201 }, Bonus = "+6% salvage/rank" });
            // Exploration
            _skills.Add(new SkillData { Id = 300, Name = "Swift Feet", Rank = 0, MaxRank = 5, Tab = 2, Prereqs = new uint[0], Bonus = "+4% move spd/rank" });
            _skills.Add(new SkillData { Id = 301, Name = "Stealth", Rank = 0, MaxRank = 5, Tab = 2, Prereqs = new uint[0], Bonus = "-6% detect range/rank" });
            _skills.Add(new SkillData { Id = 302, Name = "Scanning", Rank = 0, MaxRank = 5, Tab = 2, Prereqs = new uint[] { 300 }, Bonus = "+8% reveal range/rank" });
            _skills.Add(new SkillData { Id = 303, Name = "Cartography", Rank = 0, MaxRank = 5, Tab = 2, Prereqs = new uint[] { 302 }, Bonus = "+10% explore XP/rank" });
            _skills.Add(new SkillData { Id = 304, Name = "Endurance", Rank = 0, MaxRank = 5, Tab = 2, Prereqs = new uint[] { 300 }, Bonus = "+5% stamina/rank" });
            _skills.Add(new SkillData { Id = 305, Name = "Danger Sense", Rank = 0, MaxRank = 5, Tab = 2, Prereqs = new uint[] { 301 }, Bonus = "Hostile warning/rank" });
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.K) && !ChatUI.Instance?.IsInputActive == true) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            GUI.skin.window.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.15f, 0.92f));
            _windowRect = GUI.Window(202, _windowRect, DrawWindow, "Skills");
        }

        private void DrawWindow(int id)
        {
            // Tabs
            for (int i = 0; i < 3; i++)
            {
                bool active = _activeTab == i;
                if (GUI.Button(new Rect(10 + i * 125, 22, 120, 24), active ? $"[{_tabNames[i]}]" : _tabNames[i]))
                    _activeTab = i;
            }

            var goldStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
            GUI.Label(new Rect(10, 50, 380, 20), $"Skill Points: {_skillPoints}", goldStyle);

            float y = 74;
            for (int i = 0; i < _skills.Count; i++)
            {
                var s = _skills[i];
                if (s.Tab != _activeTab) continue;

                bool meetsPrereqs = true;
                string prereqNames = "";
                foreach (var pid in s.Prereqs)
                {
                    var p = _skills.Find(x => x.Id == pid);
                    if (p.Rank == 0) { meetsPrereqs = false; prereqNames += p.Name + " "; }
                }

                var prevColor = GUI.color;
                if (!meetsPrereqs) GUI.color = Color.gray;

                GUI.Label(new Rect(10, y, 200, 20), $"{s.Name} [{s.Rank}/{s.MaxRank}]");
                GUI.Label(new Rect(10, y + 18, 250, 16), s.Bonus, new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.cyan } });

                if (meetsPrereqs && _skillPoints > 0 && s.Rank < s.MaxRank)
                {
                    if (GUI.Button(new Rect(320, y, 60, 20), "Rank Up"))
                    {
                        var updated = s;
                        updated.Rank++;
                        _skills[i] = updated;
                        _skillPoints--;
                    }
                }
                else if (!meetsPrereqs)
                {
                    GUI.Label(new Rect(270, y, 120, 20), $"Req: {prereqNames.Trim()}",
                        new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.red } });
                }

                GUI.color = prevColor;
                y += 42;
            }

            if (GUI.Button(new Rect(340, 2, 50, 18), "Close")) _visible = false;
            GUI.DragWindow(new Rect(0, 0, 340, 20));
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
