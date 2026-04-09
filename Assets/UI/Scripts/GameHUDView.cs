using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Orlo.Player;

namespace Orlo.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameHUDView : MonoBehaviour
    {
        UIDocument      _doc;
        VisualElement   _root;

        Label           _connectionLabel, _rttLabel, _positionLabel;
        Label           _compassLabel, _headingLabel;
        VisualElement   _targetFrame;
        Label           _targetName, _targetLevel, _targetHealthLabel;
        VisualElement   _targetHealthFill;
        VisualElement   _playerBuffRow;
        VisualElement   _notificationContainer;
        Label           _currencyLabel;
        VisualElement   _xpFill;
        Label           _xpLabel;

        void Awake()
        {
            _doc  = GetComponent<UIDocument>();
            _root = _doc.rootVisualElement;

            _connectionLabel       = _root.Q<Label>("connection-label");
            _rttLabel              = _root.Q<Label>("rtt-label");
            _positionLabel         = _root.Q<Label>("position-label");
            _compassLabel          = _root.Q<Label>("compass-label");
            _headingLabel          = _root.Q<Label>("heading-label");
            _targetFrame           = _root.Q("target-frame");
            _targetName            = _root.Q<Label>("target-name");
            _targetLevel           = _root.Q<Label>("target-level");
            _targetHealthLabel     = _root.Q<Label>("target-health-label");
            _targetHealthFill      = _root.Q("target-health-fill");
            _playerBuffRow         = _root.Q("player-buff-row");
            _notificationContainer = _root.Q("notification-container");
            _currencyLabel         = _root.Q<Label>("currency-label");
            _xpFill                = _root.Q("xp-fill");
            _xpLabel               = _root.Q<Label>("xp-label");

            UIToolkitRoot.Register("GameHUD");
        }

        void OnDestroy() => UIToolkitRoot.Unregister("GameHUD");

        void Update()
        {
            var player = PlayerController.Instance;
            if (player == null) return;

            UpdateStatusStrip(player);
            UpdateCompass(player);
            UpdateTargetFrame(player);
            UpdateCurrency(player);
            UpdateXPBar(player);
        }

        void UpdateStatusStrip(PlayerController player)
        {
            var net       = NetworkManager.Instance;
            bool connected = net != null && net.IsConnected;
            _connectionLabel.EnableInClassList("connected",    connected);
            _connectionLabel.EnableInClassList("disconnected", !connected);
            _rttLabel.text      = net != null ? $"{net.RTT}ms" : "--";
            var p               = player.transform.position;
            _positionLabel.text = $"{p.x:F0}, {p.y:F0}, {p.z:F0}";
        }

        void UpdateCompass(PlayerController player)
        {
            float yaw         = player.transform.eulerAngles.y;
            _headingLabel.text = $"{(int)yaw}°";
            _compassLabel.text = YawToCardinal(yaw);
        }

        void UpdateTargetFrame(PlayerController player)
        {
            var target = player.CurrentTarget;
            if (target == null)
            {
                _targetFrame.style.display = DisplayStyle.None;
                return;
            }

            _targetFrame.style.display = DisplayStyle.Flex;
            _targetName.text  = target.DisplayName;
            _targetLevel.text = $"Lv {target.Level}";

            float hp = target.MaxHealth > 0 ? (float)target.CurrentHealth / target.MaxHealth : 0f;
            _targetHealthFill.style.width = Length.Percent(hp * 100f);
            _targetHealthLabel.text       = $"{(int)(hp * 100f)}%";

            _targetFrame.EnableInClassList("hostile",  target.Hostility == HostilityType.Hostile);
            _targetFrame.EnableInClassList("neutral",  target.Hostility == HostilityType.Neutral);
            _targetFrame.EnableInClassList("friendly", target.Hostility == HostilityType.Friendly);
        }

        void UpdateCurrency(PlayerController player)
        {
            _currencyLabel.text = $"{player.Currency:N0} cr";
        }

        void UpdateXPBar(PlayerController player)
        {
            float pct       = player.MaxXP > 0 ? (float)player.CurrentXP / player.MaxXP : 0f;
            _xpFill.style.width = Length.Percent(pct * 100f);
            _xpLabel.text   = $"{player.CurrentXP:N0} / {player.MaxXP:N0} XP";
        }

        public void ShowNotification(string message, float duration = 3f)
        {
            var label = new Label(message);
            label.AddToClassList("tmd-label");
            label.AddToClassList("notification-banner");
            _notificationContainer.Add(label);
            StartCoroutine(RemoveAfter(label, duration));
        }

        IEnumerator RemoveAfter(VisualElement el, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_notificationContainer.Contains(el))
                _notificationContainer.Remove(el);
        }

        static string YawToCardinal(float deg)
        {
            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return dirs[Mathf.RoundToInt(deg / 45f) % 8];
        }
    }
}
