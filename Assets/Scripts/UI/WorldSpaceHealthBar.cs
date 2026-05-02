// Assets/Scripts/UI/WorldSpaceHealthBar.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace CarDerby.UI
{
    /// <summary>
    /// Positions an enemy HP bar strip (VisualElement) in screen-space
    /// by projecting the car's world position every LateUpdate.
    ///
    /// UI Toolkit has no native world-space canvas, so we render everything
    /// in the shared HUD UIDocument (screen-space overlay) and manually move
    /// the element to match the projected position — same result, zero extra cameras.
    ///
    /// Setup: attach to each enemy player prefab.
    ///        PlayerNetwork.OnNetworkSpawn calls Initialize() for non-owner players.
    ///        HealthBarUI must exist in the scene (it provides the shared UIDocument).
    /// </summary>
    public class WorldSpaceHealthBar : MonoBehaviour
    {
        [SerializeField] private float   _visibilityRange = 20f;
        [SerializeField] private Vector3 _worldOffset     = new(0f, 2.4f, 0f);

        private Health.HealthSystem _healthSystem;
        private VisualElement       _bar;
        private VisualElement       _fill;
        private Camera              _cam;
        private bool                _initialized;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Call this once the enemy player's HealthSystem is ready.</summary>
        public void Initialize(Health.HealthSystem hs)
        {
            if (GameHUD.Instance == null)
            {
                Debug.LogWarning("[WorldSpaceHealthBar] GameHUD.Instance не найден — полоска HP врага не отобразится.");
                return;
            }

            _healthSystem = hs;
            _cam          = Camera.main;
            _bar          = GameHUD.Instance.RegisterEnemyBar(out _fill);

            _healthSystem.OnHealthChanged += UpdateFill;
            _healthSystem.OnDeath         += OnOwnerDied;

            UpdateFill(hs.CurrentHealth, hs.MaxHealth);
            _initialized = true;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_initialized || _bar == null || _cam == null) return;

            Vector3 worldPos = transform.position + _worldOffset;

            // Hide if behind camera
            Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0f)
            {
                _bar.style.display = DisplayStyle.None;
                return;
            }

            float dist = Vector3.Distance(worldPos, _cam.transform.position);
            if (dist > _visibilityRange)
            {
                _bar.style.display = DisplayStyle.None;
                return;
            }

            _bar.style.display = DisplayStyle.Flex;

            // Convert from Unity screen coords (bottom-left origin)
            // to UI Toolkit panel coords (top-left origin).
            float panelX = screenPos.x - _bar.resolvedStyle.width  * 0.5f;
            float panelY = Screen.height - screenPos.y - _bar.resolvedStyle.height * 0.5f;

            _bar.style.left = panelX;
            _bar.style.top  = panelY;
        }

        private void OnDestroy()
        {
            if (_healthSystem != null)
            {
                _healthSystem.OnHealthChanged -= UpdateFill;
                _healthSystem.OnDeath         -= OnOwnerDied;
            }

            _bar?.RemoveFromHierarchy();
        }

        // ── Internals ────────────────────────────────────────────────────────

        private void UpdateFill(float current, float max)
        {
            if (_fill == null) return;
            float pct = max > 0f ? current / max : 0f;
            _fill.style.width           = new StyleLength(new Length(pct * 100f, LengthUnit.Percent));
            _fill.style.backgroundColor = new StyleColor(Color.Lerp(Color.red, Color.green, pct));
        }

        private void OnOwnerDied(ulong _)
        {
            // Hide immediately on death; LateUpdate won't re-show (HealthSystem is dead)
            if (_bar != null) _bar.style.display = DisplayStyle.None;
        }
    }
}
