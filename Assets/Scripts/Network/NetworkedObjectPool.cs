using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class NetworkedObjectPool : MonoBehaviour
{
    public static NetworkedObjectPool Instance { get; private set; }

    private readonly Dictionary<GameObject, Queue<NetworkObject>> _pools = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Получить объект из пула или создать новый (сервер/хост)
    /// </summary>
    public NetworkObject GetNetworkObject(GameObject prefab, Vector3 position = default, Quaternion rotation = default)
    {
        if (!_pools.TryGetValue(prefab, out var queue) || queue.Count == 0)
        {
            // Создаём новый
            var instance = Instantiate(prefab, position, rotation);
            var no = instance.GetComponent<NetworkObject>();
            return no;
        }

        var pooled = queue.Dequeue();
        pooled.transform.SetPositionAndRotation(position, rotation);
        pooled.gameObject.SetActive(true);
        return pooled;
    }

    /// <summary>
    /// Вернуть объект в пул (вызывать вместо Despawn на клиенте/сервере)
    /// </summary>
    public void ReturnNetworkObject(NetworkObject networkObject, GameObject prefab)
    {
        networkObject.gameObject.SetActive(false);
        if (!_pools.TryGetValue(prefab, out var queue))
        {
            queue = new Queue<NetworkObject>();
            _pools[prefab] = queue;
        }
        queue.Enqueue(networkObject);
    }

    // Pre-warm пул (вызвать в Start на хосте)
    public void PreWarmPool(GameObject prefab, int count)
    {
        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<NetworkObject>();

        for (int i = 0; i < count; i++)
        {
            var instance = Instantiate(prefab);
            var no = instance.GetComponent<NetworkObject>();
            instance.SetActive(false);
            _pools[prefab].Enqueue(no);
        }
    }
}