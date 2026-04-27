// Assets/Scripts/UI/HealthBarUI.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace CarDerby.UI
{
    /// <summary>
    /// Drives HUD.uxml for the LOCAL player's HP bar.
    /// Also acts as the host for world-space enemy bars:
    /// WorldSpaceHealthBar components call RegisterEnemyBar() to inject their
    /// VisualElements into the ws-overlay container of this same UIDocument.
    ///
    /// Setup: attach to a persistent HUD GameObject with a UIDocument (HUD.uxml).
    /// Call Initialize() from PlayerNetwork.OnNetworkSpawn on the owning client.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HealthBarUI : MonoBehaviour
    {
        // Static reference so WorldSpaceHealthBar can self-register without a scene query.
        public static HealthBarUI Instance { get; private set; }

        private UIDocument    _doc;
        private VisualElement _barFill;
        private Label         _valueLabel;
        private VisualElement _wsOverlay;   // container for enemy world-space bars

        private Health.HealthSystem _healthSystem;

        private void Awake()
        {
            Instance = this;
            _doc     = GetComponent<UIDocument>();

            var root = _doc.rootVisualElement;
            _barFill    = root.Q("health-bar-fill");
            _valueLabel = root.Q<Label>("health-value");
            _wsOverlay  = root.Q("ws-overlay");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_healthSystem != null)
                _healthSystem.OnHealthChanged -= UpdateBar;
        }

        // ── Local player HP ──────────────────────────────────────────────────

        /// <summary>Called by PlayerNetwork.OnNetworkSpawn on the owning client.</summary>
        public void Initialize(Health.HealthSystem hs)
        {
            if (_healthSystem != null)
                _healthSystem.OnHealthChanged -= UpdateBar;

            _healthSystem = hs;
            _healthSystem.OnHealthChanged += UpdateBar;
            UpdateBar(hs.CurrentHealth, hs.MaxHealth);
        }

        private void UpdateBar(float current, float max)
        {
            float pct = max > 0f ? current / max : 0f;

            // Animate width via USS transition (defined in CarDerby.uss)
            _barFill.style.width = new StyleLength(new Length(pct * 100f, LengthUnit.Percent));

            // Green → yellow → red
            Color color = Color.Lerp(Color.red, Color.green, pct);
            _barFill.style.backgroundColor = new StyleColor(color);

            if (_valueLabel != null)
                _valueLabel.text = Mathf.CeilToInt(current).ToString();
        }

        // ── Enemy world-space bar registration ──────────────────────────────

        /// <summary>
        /// Creates a bar strip inside the ws-overlay and returns handles so the
        /// caller (WorldSpaceHealthBar) can update fill width and position it.
        /// </summary>
        public VisualElement RegisterEnemyBar(out VisualElement fill)
        {
            var bar = new VisualElement();
            bar.AddToClassList("ws-bar");

            fill = new VisualElement();
            fill.AddToClassList("ws-bar-fill");
            bar.Add(fill);

            _wsOverlay.Add(bar);
            return bar; // caller keeps reference for positioning
        }
    }
}
