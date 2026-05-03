// Assets/Scripts/UI/GameHUD.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using CarDerby.Health;

namespace CarDerby.UI
{
    /// <summary>
    /// HUD игровой сцены — скорость и HP локального игрока.
    /// Прикрепи к GameObject с UIDocument (HUD.uxml).
    /// PlayerNetwork.OnNetworkSpawn вызывает Bind() для локального игрока.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        // Speed
        private Label _speedValue;

        // Health
        private VisualElement _healthFill;
        private Label         _healthValue;

        // World-space enemy bars overlay
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

        /// <summary>
        /// Создаёт полоску HP врага в ws-overlay.
        /// Вызывается WorldSpaceHealthBar для каждого чужого игрока.
        /// </summary>
        public VisualElement RegisterEnemyBar(out VisualElement fill)
        {
            var bar = new VisualElement();
            bar.AddToClassList("ws-bar");

            fill = new VisualElement();
            fill.AddToClassList("ws-bar-fill");
            bar.Add(fill);

            _wsOverlay?.Add(bar);
            return bar;
        }

        /// <summary>
        /// Вызывается из PlayerNetwork.OnNetworkSpawn для локального игрока.
        /// </summary>
        public void Bind(Car.CarController controller, HealthSystem health)
        {
            _carController = controller;
            _healthSystem  = health;

            if (_healthSystem != null)
            {
                _healthSystem.OnHealthChanged += OnHealthChanged;
                // Ждём кадр — NetworkVariable может ещё не синхронизироваться
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

                // Цвет: зелёный → жёлтый → красный
                _healthFill.style.backgroundColor = pct > 0.5f
                    ? Color.Lerp(new Color(1f, 0.75f, 0f), new Color(0.2f, 0.78f, 0.27f), (pct - 0.5f) * 2f)
                    : Color.Lerp(new Color(0.78f, 0.1f, 0.1f), new Color(1f, 0.75f, 0f), pct * 2f);
            }
        }
    }
}
