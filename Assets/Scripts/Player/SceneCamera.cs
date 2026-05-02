// Assets/Scripts/Player/SceneCamera.cs
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarDerby.Player
{
    /// <summary>
    /// Орбитальная камера от третьего лица (как в GTA).
    /// Вешается на Main Camera.
    /// ПКМ (правая кнопка мыши) — вращать камеру вокруг машины.
    /// PlayerNetwork.OnNetworkSpawn вызывает Follow() чтобы назначить цель.
    /// </summary>
    public class SceneCamera : MonoBehaviour
    {
        public static SceneCamera Instance { get; private set; }

        [Header("Орбита")]
        [SerializeField] private float _distance     = 10f;
        [SerializeField] private float _targetHeight = 1.5f;  // смещение точки взгляда вверх от центра машины
        [SerializeField] private float _pitch        = 20f;   // фиксированный угол сверху (не меняется)

        [Header("Чувствительность мыши (только горизонталь)")]
        [SerializeField] private float _sensitivityX = 3f;

        [Header("Плавность следования")]
        [SerializeField] private float _followSmooth = 8f;

        private Transform _target;
        private float     _yaw;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>Вызывается из PlayerNetwork когда спавнится локальный игрок.</summary>
        public void Follow(Transform target)
        {
            _target = target;
            if (target == null) return;

            // Начинаем сзади машины
            _yaw = target.eulerAngles.y;
            SnapToTarget();

            // Прячем курсор — прицел по центру экрана
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        private void Update()
        {
            // Escape — разблокировать курсор (выход из игры / пауза)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            // ── Вращение камеры мышью — только по горизонтали (Y) ───────────
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float deltaX = mouse.delta.ReadValue().x;
                _yaw += deltaX * _sensitivityX;
            }

            // ── Позиция: орбита вокруг машины ────────────────────────────────
            Vector3    lookTarget = _target.position + Vector3.up * _targetHeight;
            Quaternion rotation   = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3    desiredPos = lookTarget + rotation * new Vector3(0f, 0f, -_distance);

            transform.position = Vector3.Lerp(transform.position, desiredPos,
                                              _followSmooth * Time.deltaTime);

            // ── Всегда смотрим на машину ──────────────────────────────────────
            transform.LookAt(lookTarget);
        }

        private void SnapToTarget()
        {
            Vector3    lookTarget = _target.position + Vector3.up * _targetHeight;
            Quaternion rotation   = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position   = lookTarget + rotation * new Vector3(0f, 0f, -_distance);
            transform.LookAt(lookTarget);
        }
    }
}
