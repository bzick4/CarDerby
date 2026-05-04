// Assets/Scripts/Player/SceneCamera.cs
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarDerby.Player
{
    public class SceneCamera : MonoBehaviour
    {
        public static SceneCamera Instance { get; private set; }

        [Header("Орбита")]
        [SerializeField] private float _distance     = 10f;
        [SerializeField] private float _targetHeight = 1.5f;
        [SerializeField] private float _pitch        = 20f;

        [Header("Чувствительность мыши")]
        [SerializeField] private float _sensitivityX = 3f;

        [Header("Плавность следования")]
        [SerializeField] private float _followSmooth = 8f;

        [Header("Прицел-курсор")]
        [Tooltip("Текстура курсора-прицела. Если не назначена — генерируется автоматически.")]
        [SerializeField] private Texture2D _crosshairTexture;
        [Tooltip("Размер курсора на экране в пикселях.")]
        [SerializeField] private int _cursorSize = 48;

        private Transform  _target;
        private float      _yaw;
        private Texture2D  _activeCursorTex;

        private void Awake() => Instance = this;

        public void Follow(Transform target)
        {
            _target = target;
            if (target == null) return;

            _yaw = target.eulerAngles.y;
            SnapToTarget();

            ApplyCrosshairCursor();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            var mouse = Mouse.current;
            if (mouse != null)
                _yaw += mouse.delta.ReadValue().x * _sensitivityX;

            Vector3    lookTarget = _target.position + Vector3.up * _targetHeight;
            Quaternion rotation   = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3    desiredPos = lookTarget + rotation * new Vector3(0f, 0f, -_distance);

            transform.position = Vector3.Lerp(transform.position, desiredPos,
                                              _followSmooth * Time.deltaTime);
            transform.LookAt(lookTarget);
        }

        private void SnapToTarget()
        {
            Vector3    lookTarget = _target.position + Vector3.up * _targetHeight;
            Quaternion rotation   = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position   = lookTarget + rotation * new Vector3(0f, 0f, -_distance);
            transform.LookAt(lookTarget);
        }

        // ── Курсор-прицел ────────────────────────────────────────────────────

        private void ApplyCrosshairCursor()
        {
            var src = _crosshairTexture != null ? _crosshairTexture : BuildCrosshairTexture();
            _activeCursorTex = ResizeTexture(src, _cursorSize, _cursorSize);
            var hotspot = new Vector2(_cursorSize * 0.5f, _cursorSize * 0.5f);

            Cursor.SetCursor(_activeCursorTex, hotspot, CursorMode.ForceSoftware);
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible   = true;
        }

        /// <summary>Масштабирует текстуру до нужного размера через GPU blit.</summary>
        private static Texture2D ResizeTexture(Texture2D src, int w, int h)
        {
            var rt   = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;

            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        /// <summary>Генерирует красный круг-прицел 32×32.</summary>
        private static Texture2D BuildCrosshairTexture()
        {
            const int   size      = 16;
            const float cx        = size / 2f;
            const float cy        = size / 2f;
            const float outerR    = 4f;  // внешний радиус
            const float innerR    = 3f;  // внутренний радиус (толщина кольца = 4px)
            var         ringColor = new Color(1f, 0.1f, 0.1f, 1f); // ярко-красный

            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    pixels[y * size + x] = (dist >= innerR && dist <= outerR)
                        ? ringColor
                        : Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
