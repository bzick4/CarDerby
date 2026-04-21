using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Linq;   // добавь эту строку

[RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
public class VehicleController : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _driftBrakeAction;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider[] _frontWheels = new WheelCollider[2];
    [SerializeField] private WheelCollider[] _rearWheels = new WheelCollider[2];

    [Header("Car Data")]
    [SerializeField] private CarData _carData;

    [Header("Tuning")]
    [SerializeField] private float _motorTorque = 1800f;
    [SerializeField] private float _brakeTorque = 4000f;
    [SerializeField] private float _maxSteerAngle = 32f;
    [SerializeField] private float _driftStiffness = 0.55f;   // 0.4 = очень сильный дрифт

    private Rigidbody _rb;
    private Vector2 _moveInput;
    private bool _isDrifting;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsLocalPlayer) return;

        _moveAction.action.Enable();
        _driftBrakeAction.action.Enable();

        _moveAction.action.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _moveAction.action.canceled += ctx => _moveInput = Vector2.zero;

        _driftBrakeAction.action.performed += _ => _isDrifting = true;
        _driftBrakeAction.action.canceled += _ => _isDrifting = false;
    }

    private void FixedUpdate()
    {
        if (!IsLocalPlayer || _carData == null) return;

        float steer = _moveInput.x * _maxSteerAngle;
        float accel = _moveInput.y * _motorTorque * _carData.Acceleration;

        // Руление
        for (int i = 0; i < _frontWheels.Length; i++)
            if (_frontWheels[i] != null) _frontWheels[i].steerAngle = steer;

        // Мотор (задний привод)
        for (int i = 0; i < _rearWheels.Length; i++)
            if (_rearWheels[i] != null) _rearWheels[i].motorTorque = accel;

        // Дрифт
        if (_isDrifting)
        {
            for (int i = 0; i < _rearWheels.Length; i++)
                if (_rearWheels[i] != null) _rearWheels[i].brakeTorque = _brakeTorque * 0.6f;

            SetSidewaysStiffness(_driftStiffness);
        }
        else
        {
            for (int i = 0; i < _rearWheels.Length; i++)
                if (_rearWheels[i] != null) _rearWheels[i].brakeTorque = 0f;

            SetSidewaysStiffness(1f);
        }
    }

    private void SetSidewaysStiffness(float stiffness)
    {
        WheelFrictionCurve curve = new WheelFrictionCurve();

        foreach (var wheel in _frontWheels)
        {
            if (wheel == null) continue;
            curve = wheel.sidewaysFriction;
            curve.stiffness = stiffness;
            wheel.sidewaysFriction = curve;
        }

        foreach (var wheel in _rearWheels)
        {
            if (wheel == null) continue;
            curve = wheel.sidewaysFriction;
            curve.stiffness = stiffness;
            wheel.sidewaysFriction = curve;
        }
    }

    public void SetCarData(CarData data) => _carData = data;
}