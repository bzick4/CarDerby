using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Unity.Cinemachine;

/// <summary>
/// Управляет несколькими виртуальными камерами и предоставляет эффекты для сетевой игры
/// </summary>
public class CameraManager : Singleton<CameraManager>
{
    [System.Serializable]
    public class CameraPreset
    {
        public string name;
        public Vector3 offset;
        public float damping;
        public float fov;
    }

    [SerializeField] private List<CameraPreset> _cameraPresets = new List<CameraPreset>();
    
    private CinemachineVirtualCamera _activeCamera;
    private Dictionary<string, CinemachineVirtualCamera> _cameras = new Dictionary<string, CinemachineVirtualCamera>();
    private Coroutine _shakeCoroutine;
    private Coroutine _cameraTransitionCoroutine;

    /// <summary>
    /// Регистрирует виртуальную камеру
    /// </summary>
    public void RegisterCamera(string cameraName, CinemachineVirtualCamera camera)
    {
        if (!_cameras.ContainsKey(cameraName))
            _cameras[cameraName] = camera;
    }

    /// <summary>
    /// Активирует камеру по имени
    /// </summary>
    public void ActivateCamera(string cameraName)
    {
        if (_cameras.TryGetValue(cameraName, out CinemachineVirtualCamera camera))
        {
            if (_activeCamera != null)
                _activeCamera.enabled = false;

            _activeCamera = camera;
            _activeCamera.enabled = true;
        }
    }

    /// <summary>
    /// Применяет импульс к камере (для эффектов столкновений, взрывов и т.д.)
    /// </summary>
    public void ApplyCameraShake(float intensity = 1f, float duration = 0.2f)
    {
        if (_activeCamera == null)
            return;

        if (_shakeCoroutine != null)
            StopCoroutine(_shakeCoroutine);

        _shakeCoroutine = StartCoroutine(CameraShakeCoroutine(intensity, duration));
    }

    private System.Collections.IEnumerator CameraShakeCoroutine(float intensity, float duration)
    {
        CinemachineBasicMultiChannelPerlin noise = null;

        if (_activeCamera != null)
            noise = _activeCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

        if (noise != null)
        {
            // Используем reflection для совместимости с разными версиями Cinemachine
            var amplitudeProperty = typeof(CinemachineBasicMultiChannelPerlin).GetProperty("AmplitudeGain") 
                ?? typeof(CinemachineBasicMultiChannelPerlin).GetProperty("m_AmplitudeGain");
            
            if (amplitudeProperty != null)
            {
                amplitudeProperty.SetValue(noise, intensity);
                yield return new WaitForSeconds(duration);
                amplitudeProperty.SetValue(noise, 0f);
            }
        }
    }

    /// <summary>
    /// Переходит между двумя камерами с плавной интерполяцией
    /// </summary>
    public void SwitchCameraSmooth(CinemachineVirtualCamera from, CinemachineVirtualCamera to, float duration = 1f)
    {
        if (_cameraTransitionCoroutine != null)
            StopCoroutine(_cameraTransitionCoroutine);

        _cameraTransitionCoroutine = StartCoroutine(CameraSwitchCoroutine(from, to, duration));
    }

    private System.Collections.IEnumerator CameraSwitchCoroutine(CinemachineVirtualCamera from, CinemachineVirtualCamera to, float duration)
    {
        // Увеличиваем приоритет новой камеры
        to.Priority = from.Priority + 1;
        
        // Даём время для переключения
        yield return new WaitForSeconds(duration);
        
        // Отключаем старую камеру
        from.enabled = false;
        from.Priority = 0;
        to.Priority = 10;
        
        _activeCamera = to;
    }

    /// <summary>
    /// Применяет предустановку камеры
    /// </summary>
    public void ApplyCameraPreset(string presetName)
    {
        var preset = _cameraPresets.Find(p => p.name == presetName);
        if (preset != null && _activeCamera != null)
        {
            var transposer = _activeCamera.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                transposer.m_FollowOffset = preset.offset;
                transposer.m_XDamping = preset.damping;
                transposer.m_YDamping = preset.damping;
                transposer.m_ZDamping = preset.damping;
            }
        }
    }

    /// <summary>
    /// Устанавливает FOV активной камеры
    /// </summary>
    public void SetCameraFOV(float fov)
    {
        if (_activeCamera != null)
            _activeCamera.m_Lens.FieldOfView = fov;
    }

    /// <summary>
    /// Создает временный эффект zoom-in
    /// </summary>
    public void CameraZoomEffect(float targetFOV, float duration)
    {
        if (_activeCamera != null)
            StartCoroutine(ZoomCoroutine(targetFOV, duration));
    }

    private System.Collections.IEnumerator ZoomCoroutine(float targetFOV, float duration)
    {
        float startFOV = _activeCamera.m_Lens.FieldOfView;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            _activeCamera.m_Lens.FieldOfView = Mathf.Lerp(startFOV, targetFOV, t);
            yield return null;
        }

        _activeCamera.m_Lens.FieldOfView = targetFOV;
    }

    /// <summary>
    /// Возвращает активную камеру
    /// </summary>
    public CinemachineVirtualCamera GetActiveCamera() => _activeCamera;
}

/// <summary>
/// Базовый класс для Singleton паттерна
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<T>();

            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = (T)(object)this;
        DontDestroyOnLoad(gameObject);
    }
}
