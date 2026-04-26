using UnityEngine;
using Unity.Netcode;

using Unity.Cinemachine;

/// <summary>
/// Расширенное управление Cinemachine камерой с синхронизацией скорости и направления
/// </summary>
[RequireComponent(typeof(CinemachineVirtualCamera))]
public class AdvancedNetworkCamera : NetworkBehaviour
{
    [Header("Camera Follow Settings")]
    [SerializeField] private CinemachineVirtualCamera _virtualCamera;
    [SerializeField] private float _baseFollowDistance = 5f;
    [SerializeField] private float _baseFollowHeight = 2.5f;
    [SerializeField] private float _maxSpeedFollowDistance = 8f;
    [SerializeField] private float _maxSpeed = 50f;

    [Header("Look Ahead")]
    [SerializeField] private float _lookAheadDistance = 3f;
    [SerializeField] private float _lookAheadSmoothing = 0.15f;

    [Header("Camera Tilt")]
    [SerializeField] private bool _enableSpeedTilt = true;
    [SerializeField] private float _maxTiltAngle = 10f;

    [Header("Smoothing")]
    [SerializeField] private float _damping = 0.1f;

    private Rigidbody _targetRigidbody;
    private CinemachineTransposer _transposer;
    private CinemachineComposer _composer;
    private Vector3 _lookAheadOffset = Vector3.zero;
    private float _currentSpeed = 0f;

    private void Awake()
    {
        if (_virtualCamera == null)
            _virtualCamera = GetComponent<CinemachineVirtualCamera>();

        _transposer = _virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
        _composer = _virtualCamera.GetCinemachineComponent<CinemachineComposer>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsLocalPlayer)
        {
            _virtualCamera.enabled = false;
            enabled = false;
            return;
        }

        _targetRigidbody = GetComponent<Rigidbody>();
        _virtualCamera.enabled = true;
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        if (_transposer != null)
        {
            _transposer.m_FollowOffset = new Vector3(0, _baseFollowHeight, -_baseFollowDistance);
            _transposer.m_XDamping = _damping;
            _transposer.m_YDamping = _damping;
            _transposer.m_ZDamping = _damping;
        }

        _virtualCamera.Follow = transform;
        _virtualCamera.LookAt = transform;
    }

    private void Update()
    {
        if (!IsLocalPlayer || _virtualCamera == null || !_virtualCamera.enabled)
            return;

        UpdateCameraPosition();
        UpdateLookAhead();

        if (_enableSpeedTilt)
            UpdateCameraTilt();
    }

    /// <summary>
    /// Обновляет позицию камеры в зависимости от скорости
    /// </summary>
    private void UpdateCameraPosition()
    {
        if (_targetRigidbody == null || _transposer == null)
            return;

        _currentSpeed = _targetRigidbody.linearVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(_currentSpeed / _maxSpeed);

        // Увеличиваем расстояние камеры при высокой скорости
        float followDistance = Mathf.Lerp(_baseFollowDistance, _maxSpeedFollowDistance, speedRatio);
        float followHeight = Mathf.Lerp(_baseFollowHeight, _baseFollowHeight * 1.2f, speedRatio * 0.5f);

        Vector3 currentOffset = _transposer.m_FollowOffset;
        currentOffset.z = -followDistance;
        currentOffset.y = followHeight;

        _transposer.m_FollowOffset = currentOffset;
    }

    /// <summary>
    /// Обновляет предварительный просмотр (look ahead) в направлении движения
    /// </summary>
    private void UpdateLookAhead()
    {
        if (_targetRigidbody == null || _composer == null)
            return;

        Vector3 velocity = _targetRigidbody.linearVelocity;

        if (velocity.magnitude > 0.1f)
        {
            // Вычисляем направление движения
            Vector3 velocityDirection = velocity.normalized;
            _lookAheadOffset = velocityDirection * _lookAheadDistance;
        }
        else
        {
            _lookAheadOffset = Vector3.Lerp(_lookAheadOffset, Vector3.zero, _lookAheadSmoothing);
        }

        // Применяем look ahead к позиции объекта внимания
        Vector3 lookTarget = transform.position + _lookAheadOffset;
        
        // Можно дополнительно настроить компоновку композера
        float forwardLookAmount = Mathf.Clamp01(velocity.magnitude / _maxSpeed) * 0.1f;
        _composer.m_ScreenY = 0.55f + forwardLookAmount;
    }

    /// <summary>
    /// Обновляет наклон камеры в зависимости от скорости
    /// </summary>
    private void UpdateCameraTilt()
    {
        if (_transposer == null)
            return;

        float speedRatio = Mathf.Clamp01(_currentSpeed / _maxSpeed);
        float tiltAmount = Mathf.Lerp(0, _maxTiltAngle, speedRatio);

        // Применяем наклон путем изменения позиции
        Vector3 currentOffset = _transposer.m_FollowOffset;
        
        // Немного приподнимаем камеру при высокой скорости для большего угла обзора
        currentOffset.y += tiltAmount * 0.02f;

        _transposer.m_FollowOffset = currentOffset;
    }

    /// <summary>
    /// Возвращает текущую скорость транспортного средства
    /// </summary>
    public float GetCurrentSpeed()
    {
        return _currentSpeed;
    }

    /// <summary>
    /// Временно изменяет расстояние камеры (например, при столкновении)
    /// </summary>
    public void SetTemporaryOffset(Vector3 offset, float duration)
    {
        StartCoroutine(ApplyTemporaryOffset(offset, duration));
    }

    private System.Collections.IEnumerator ApplyTemporaryOffset(Vector3 offset, float duration)
    {
        if (_transposer == null)
            yield break;

        Vector3 originalOffset = _transposer.m_FollowOffset;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            _transposer.m_FollowOffset = Vector3.Lerp(originalOffset, originalOffset + offset, t);
            yield return null;
        }

        _transposer.m_FollowOffset = originalOffset;
    }
}
