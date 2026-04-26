using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
public class ProjectileBehaviour : NetworkBehaviour
{
    [SerializeField] private float _speed = 40f;
    [SerializeField] private int _damage = 25;
    [SerializeField] private float _lifeTime = 3f;

    private Rigidbody _rb;
    private float _spawnTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        _spawnTime = Time.time;
        _rb.linearVelocity = transform.forward * _speed;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (Time.time - _spawnTime >= _lifeTime)
            Recycle();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        var health = collision.gameObject.GetComponent<HealthComponent>();
        health?.TakeDamageServerRpc(_damage);

        Recycle();
    }

    private void Recycle()
    {
        _rb.linearVelocity = Vector3.zero;
        NetworkObject.Despawn(false);
        gameObject.SetActive(false);
    }
}
