using Unity.Netcode;
using UnityEngine;

public class HealthComponent : NetworkBehaviour
{
    private readonly NetworkVariable<int> _currentHealth = new(100, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);

    public int CurrentHealth => _currentHealth.Value;
    public event System.Action<int> OnHealthChanged;

    public override void OnNetworkSpawn()
    {
        _currentHealth.OnValueChanged += (_, newVal) => OnHealthChanged?.Invoke(newVal);
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damage)
    {
        _currentHealth.Value = Mathf.Max(0, _currentHealth.Value - damage);
        if (_currentHealth.Value <= 0)
            NetworkObject.Despawn(true);
    }
}