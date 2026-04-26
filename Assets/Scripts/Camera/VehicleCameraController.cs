using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;

/// <summary>
/// Интеграция Cinemachine камеры с VehicleController
/// Обеспечивает следование за машиной и эффекты при взаимодействии
/// </summary>
public class VehicleCameraController : NetworkBehaviour
{
    [Header("Camera References")]
    [SerializeField] private CinemachineNetworkCamera _networkCamera;
    [SerializeField] private AdvancedNetworkCamera _advancedCamera;

    [Header("Camera Effects")]
    [SerializeField] private float _collisionShakeDuration = 0.2f;
    [SerializeField] private float _collisionShakeIntensity = 0.5f;

    [Header("Dynamic FOV")]
    [SerializeField] private bool _enableDynamicFOV = true;
    [SerializeField] private float _baseFOV = 60f;
    [SerializeField] private float _maxFOV = 75f;
    [SerializeField] private float _maxVelocityForFOV = 50f;

    private Rigidbody _rigidbody;
    private CinemachineVirtualCamera _virtualCamera;
    private float _currentFOV;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsLocalPlayer)
            return;

        // Если камеры не найдены в инспекторе - ищем в children
        if (_advancedCamera == null && _networkCamera == null)
        {
            _advancedCamera = GetComponentInChildren<AdvancedNetworkCamera>();
            _networkCamera = GetComponentInChildren<CinemachineNetworkCamera>();
        }

        // Выбираем камеру (продвинутая или обычная)
        if (_advancedCamera != null)
        {
            _virtualCamera = _advancedCamera.GetComponent<CinemachineVirtualCamera>();
        }
        else if (_networkCamera != null)
        {
            _virtualCamera = _networkCamera.GetComponent<CinemachineVirtualCamera>();
        }

        // Если есть Virtual Camera - устанавливаем правильный приоритет
        if (_virtualCamera != null)
        {
            _virtualCamera.Priority = 10; // Высокий приоритет для локального игрока
        }

        _currentFOV = _baseFOV;
    }

    private void Update()
    {
        if (!IsLocalPlayer || _virtualCamera == null)
            return;

        if (_enableDynamicFOV)
            UpdateDynamicFOV();
    }

    /// <summary>
    /// Обновляет FOV в зависимости от скорости
    /// </summary>
    private void UpdateDynamicFOV()
    {
        if (_rigidbody == null)
            return;

        float velocity = _rigidbody.linearVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(velocity / _maxVelocityForFOV);
        float targetFOV = Mathf.Lerp(_baseFOV, _maxFOV, speedRatio);

        _currentFOV = Mathf.Lerp(_currentFOV, targetFOV, Time.deltaTime * 2f);
        _virtualCamera.m_Lens.FieldOfView = _currentFOV;
    }

    /// <summary>
    /// Вызывается при столкновении (используйте из скрипта обработки столкновений)
    /// </summary>
    public void OnVehicleCollision(float impactForce)
    {
        if (!IsLocalPlayer)
            return;

        // Применяем эффект покачивания камеры
        float shakeIntensity = Mathf.Clamp01(impactForce / 100f) * _collisionShakeIntensity;
        CameraManager.Instance?.ApplyCameraShake(shakeIntensity, _collisionShakeDuration);
    }

    /// <summary>
    /// Настраивает камеру для режима спектатора
    /// </summary>
    public void SetSpectatorMode(Transform targetVehicle, float distance = 8f, float height = 3f)
    {
        if (_networkCamera == null)
            return;

        _virtualCamera.Follow = targetVehicle;
        _virtualCamera.LookAt = targetVehicle;

        var offset = new Vector3(0, height, -distance);
        _networkCamera.SetCameraOffset(offset);
    }

    /// <summary>
    /// Возвращает в обычный режим следования
    /// </summary>
    public void SetFollowMode()
    {
        if (_virtualCamera == null)
            return;

        _virtualCamera.Follow = transform;
        _virtualCamera.LookAt = transform;
    }

    /// <summary>
    /// Применяет временное смещение камеры (для специальных эффектов)
    /// </summary>
    public void ApplyCameraEffect(Vector3 offset, float duration)
    {
        if (_networkCamera != null)
            _networkCamera.SetCameraEffect(offset, duration);
    }

    /// <summary>
    /// Получает текущую скорость (для использования в UI и других целях)
    /// </summary>
    public float GetCurrentSpeed()
    {
        return _rigidbody != null ? _rigidbody.linearVelocity.magnitude : 0f;
    }
}

/// <summary>
/// Расширение для CinemachineNetworkCamera с поддержкой эффектов
/// </summary>
public static class CameraEffectExtensions
{
    public static void SetCameraEffect(this CinemachineNetworkCamera camera, Vector3 offset, float duration)
    {
        // Можно расширить для создания более сложных эффектов
        camera.SetCameraOffset(offset);
    }
}
