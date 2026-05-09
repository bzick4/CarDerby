// Assets/Scripts/UI/CarPreviewSystem.cs
using System.Collections;
using UnityEngine;

namespace CarDerby.UI
{
    public enum PreviewView { Overview, Weapon, Scoop }

    /// <summary>
    /// Рендерит машину в RenderTexture для UI.
    ///
    /// Работает в двух режимах:
    ///   AUTO  — если _overviewAnchor не назначен, камера позиционируется
    ///           автоматически по bounding box машины. Настройка не нужна.
    ///   MANUAL — назначь _overviewAnchor / _weaponAnchor / _scoopAnchor
    ///            в Inspector для точного кадрирования.
    ///
    /// _previewCamera и _spawnPoint назначить нужно всегда.
    /// RenderTexture создаётся автоматически если не назначен.
    /// </summary>
    public class CarPreviewSystem : MonoBehaviour
    {
        [Header("Camera (обязательно)")]
        [SerializeField] private Camera        _previewCamera;
        [SerializeField] private RenderTexture _renderTexture; // опционально — создаётся авто

        [Header("Camera anchors (опционально — авто если пусто)")]
        [SerializeField] private Transform _overviewAnchor;
        [SerializeField] private Transform _weaponAnchor;
        [SerializeField] private Transform _scoopAnchor;

        [Header("Car placement (обязательно)")]
        [SerializeField] private Transform _spawnPoint;

        public RenderTexture RenderTexture => _renderTexture;

        private GameObject _previewCar;
        private Coroutine  _moveCoroutine;
        private Bounds     _carBounds;

        private void Awake()
        {
            // Создаём RenderTexture если не назначен
            if (_renderTexture == null)
                _renderTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);

            if (_previewCamera != null)
                _previewCamera.targetTexture = _renderTexture;
        }

        private void OnDestroy()
        {
            if (_previewCar != null) Destroy(_previewCar);
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void ShowCar(GameObject prefab)
        {
            if (_previewCar != null) Destroy(_previewCar);
            if (prefab == null || _spawnPoint == null) return;

            _previewCar = Instantiate(prefab, _spawnPoint.position, _spawnPoint.rotation);
            DisableGameplayComponents(_previewCar);
            _carBounds = CalculateBounds(_previewCar);

            // Если анкоры не назначены — позиционируем камеру автоматически
            if (_overviewAnchor == null && _previewCamera != null)
                SetCameraAuto(PreviewView.Overview);
        }

        public void MoveToView(PreviewView view, bool instant = false)
        {
            if (_previewCamera == null) return;

            // Режим MANUAL — анкоры заданы
            if (_overviewAnchor != null)
            {
                Transform anchor = view switch
                {
                    PreviewView.Overview => _overviewAnchor,
                    PreviewView.Weapon   => _weaponAnchor != null ? _weaponAnchor : _overviewAnchor,
                    PreviewView.Scoop    => _scoopAnchor  != null ? _scoopAnchor  : _overviewAnchor,
                    _                    => _overviewAnchor,
                };
                if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
                if (instant) _previewCamera.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
                else         _moveCoroutine = StartCoroutine(LerpTo(anchor.position, anchor.rotation));
                return;
            }

            // Режим AUTO — считаем позицию по bounds
            if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
            var (pos, rot) = GetAutoCamera(view);
            if (instant) _previewCamera.transform.SetPositionAndRotation(pos, rot);
            else         _moveCoroutine = StartCoroutine(LerpTo(pos, rot));
        }

        // ── Auto positioning ─────────────────────────────────────────────────

        private void SetCameraAuto(PreviewView view)
        {
            if (_previewCamera == null) return;
            var (pos, rot) = GetAutoCamera(view);
            _previewCamera.transform.SetPositionAndRotation(pos, rot);
        }

        private (Vector3 pos, Quaternion rot) GetAutoCamera(PreviewView view)
        {
            Vector3 center = _carBounds.center;
            float   size   = _carBounds.size.magnitude;

            Vector3 camPos;
            Vector3 lookAt;

            switch (view)
            {
                case PreviewView.Weapon:
                    // Сверху чуть сбоку — смотрит на крышу
                    camPos = center + new Vector3(size * 0.3f, size * 0.7f, -size * 0.5f);
                    lookAt = center + Vector3.up * _carBounds.size.y * 0.35f;
                    break;
                case PreviewView.Scoop:
                    // Спереди чуть снизу — смотрит на перед
                    camPos = center + new Vector3(0f, size * 0.2f, -size * 0.9f);
                    lookAt = center - Vector3.up * _carBounds.size.y * 0.1f;
                    break;
                default: // Overview
                    camPos = center + new Vector3(size * 0.6f, size * 0.4f, -size * 1.1f);
                    lookAt = center;
                    break;
            }

            var rot = Quaternion.LookRotation(lookAt - camPos);
            return (camPos, rot);
        }

        // ── Smooth movement ──────────────────────────────────────────────────

        private IEnumerator LerpTo(Vector3 targetPos, Quaternion targetRot)
        {
            var        cam      = _previewCamera.transform;
            Vector3    startPos = cam.position;
            Quaternion startRot = cam.rotation;
            float      t        = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime * 1.8f;
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                cam.SetPositionAndRotation(
                    Vector3.Lerp(startPos, targetPos, s),
                    Quaternion.Lerp(startRot, targetRot, s));
                yield return null;
            }
            cam.SetPositionAndRotation(targetPos, targetRot);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Bounds CalculateBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 2f);

            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static void DisableGameplayComponents(GameObject go)
        {
            foreach (var nb in go.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>(true))
                nb.enabled = false;
            if (go.TryGetComponent<Unity.Netcode.NetworkObject>(out var no))
                no.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                rb.isKinematic = true;
            foreach (var wc in go.GetComponentsInChildren<WheelCollider>(true))
                wc.enabled = false;
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }
    }
}
