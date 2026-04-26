using Unity.Netcode;
using UnityEngine;

// Расставь чекпоинты в сцене, назначь Index каждому (0, 1, 2...) и укажи TotalCheckpoints.
// На слой этого объекта поставь триггер-коллайдер. Тег машины должен быть "Player".
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private int _index;
    [SerializeField] private int _totalCheckpoints;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Работаем только на сервере
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null) return;

        RaceManager.Instance?.ReportCheckpoint(no.OwnerClientId, _index, _totalCheckpoints);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
        var col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"CP {_index}");
    }
#endif
}
