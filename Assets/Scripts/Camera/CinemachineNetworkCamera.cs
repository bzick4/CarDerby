using UnityEngine;
using Unity.Netcode;
using  Unity.Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class CinemachineNetworkCamera : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private CinemachineVirtualCamera _virtualCamera;
    [SerializeField] private float _followDistance = 5f;
    [SerializeField] private float _followHeight = 2f;
    [SerializeField] private float _lookAhead = 2f;
    
    [Header("Cinemachine Composer")]
    [SerializeField] private float _screenX = 0.5f;
    [SerializeField] private float _screenY = 0.55f;
    [SerializeField] private float _deadZoneWidth = 0.1f;
    [SerializeField] private float _deadZoneHeight = 0.1f;

    [Header("Damping")]
    [SerializeField] private float _positionDamping = 0.1f;
    [SerializeField] private float _rotationDamping = 0.05f;

    private Transform _targetTransform;
    private CinemachineTransposer _transposer;
    private CinemachineComposer _composer;
    private Vector3 _targetOffset;

    private void Awake()
    {
        if (_virtualCamera == null)
            _virtualCamera = GetComponent<CinemachineVirtualCamera>();

        _transposer = _virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
        _composer = _virtualCamera.GetCinemachineComponent<CinemachineComposer>();

        if (_transposer != null)
        {
            _targetOffset = _transposer.m_FollowOffset;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsLocalPlayer)
        {
            // Деактивируем камеру для других игроков
            _virtualCamera.enabled = false;
            enabled = false;
            return;
        }

        // Активируем камеру только для локального игрока
        _virtualCamera.enabled = true;
        _targetTransform = transform.parent ?? transform;
        SetupCamera();
    }

    private void SetupCamera()
    {
        if (_transposer != null)
        {
            // Устанавливаем смещение камеры позади и выше транспортного средства
            _transposer.m_FollowOffset = new Vector3(0, _followHeight, -_followDistance);
            _transposer.m_XDamping = _positionDamping;
            _transposer.m_YDamping = _positionDamping;
            _transposer.m_ZDamping = _positionDamping;
        }

        if (_composer != null)
        {
            _composer.m_ScreenX = _screenX;
            _composer.m_ScreenY = _screenY;
            _composer.m_DeadZoneWidth = _deadZoneWidth;
            _composer.m_DeadZoneHeight = _deadZoneHeight;
        }

        _virtualCamera.Follow = transform;
        _virtualCamera.LookAt = transform;
    }

    private void Update()
    {
        if (!IsLocalPlayer || _virtualCamera == null || !_virtualCamera.enabled)
            return;

        UpdateCameraHeight();
    }

    /// <summary>
    /// Динамически исправляет высоту камеры по поверхности
    /// </summary>
    private void UpdateCameraHeight()
    {
        if (_transposer == null)
            return;

        Vector3 currentOffset = _transposer.m_FollowOffset;
        
        // Проверяем высоту под машиной
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 10f))
        {
            float terrainHeight = hit.point.y;
            float desiredHeight = terrainHeight + _followHeight;
            float cameraHeight = transform.position.y;

            // Плавно подстраиваем высоту
            currentOffset.y = Mathf.Lerp(cameraHeight - transform.position.y, desiredHeight - transform.position.y, Time.deltaTime * 2f);
        }

        _transposer.m_FollowOffset = currentOffset;
    }

    /// <summary>
    /// Устанавливает дополнительное смещение камеры (например, при поворотах)
    /// </summary>
    public void SetCameraOffset(Vector3 offset)
    {
        if (!IsLocalPlayer || _transposer == null)
            return;

        _transposer.m_FollowOffset = offset;
    }

    /// <summary>
    /// Возвращает текущее смещение камеры
    /// </summary>
    public Vector3 GetCameraOffset()
    {
        return _transposer != null ? _transposer.m_FollowOffset : Vector3.zero;
    }
}
