// Assets/Scripts/UI/GameHUD.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using CarDerby.Health;

namespace CarDerby.UI
{
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        private Label _speedValue;

        private VisualElement _healthFill;
        private Label         _healthValue;

        private VisualElement _wsOverlay;

        private Car.CarController _carController;
        private HealthSystem      _healthSystem;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>()?.rootVisualElement;
            if (root == null) return;

            _speedValue  = root.Q<Label>("speed-value");
            _healthFill  = root.Q<VisualElement>("health-bar-fill");
            _healthValue = root.Q<Label>("health-value");
            _wsOverlay   = root.Q<VisualElement>("ws-overlay");
        }

        public VisualElement RegisterEnemyBar(out VisualElement fill)
        {
            var bar = new VisualElement();
            bar.AddToClassList("ws-bar");

            // Track clips the fill; name label is inserted above it by WorldSpaceHealthBar
            var track = new VisualElement();
            track.AddToClassList("ws-bar-track");

            fill = new VisualElement();
            fill.AddToClassList("ws-bar-fill");
            track.Add(fill);
            bar.Add(track);

            _wsOverlay?.Add(bar);
            return bar;
        }

        public void Bind(Car.CarController controller, HealthSystem health)
        {
            _carController = controller;
            _healthSystem  = health;

            if (_healthSystem != null)
            {
                _healthSystem.OnHealthChanged += OnHealthChanged;
                StartCoroutine(RefreshHealthNextFrame());
            }
        }

        private IEnumerator RefreshHealthNextFrame()
        {
            yield return null;
            if (_healthSystem != null)
                RefreshHealth(_healthSystem.CurrentHealth, _healthSystem.MaxHealth);
        }

        private void OnDisable()
        {
            if (_healthSystem != null)
                _healthSystem.OnHealthChanged -= OnHealthChanged;
        }

        private void Update()
        {
            if (_speedValue == null || _carController == null) return;
            _speedValue.text = Mathf.RoundToInt(_carController.SpeedKmh * 2f).ToString();
        }

        private void OnHealthChanged(float current, float max)
            => RefreshHealth(current, max);

        private void RefreshHealth(float current, float max)
        {
            if (_healthValue != null)
                _healthValue.text = Mathf.CeilToInt(current).ToString();

            if (_healthFill != null && max > 0f)
            {
                float pct = current / max;
                _healthFill.style.width = Length.Percent(pct * 100f);

                _healthFill.style.backgroundColor = pct > 0.5f
                    ? Color.Lerp(new Color(1f, 0.75f, 0f), new Color(0.2f, 0.78f, 0.27f), (pct - 0.5f) * 2f)
                    : Color.Lerp(new Color(0.78f, 0.1f, 0.1f), new Color(1f, 0.75f, 0f), pct * 2f);
            }
        }
    }
}
