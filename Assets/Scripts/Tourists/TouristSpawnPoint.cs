
using UnityEngine;

public class TouristSpawnPoint : MonoBehaviour
{
    [Header("Spawn Point Settings")]
    [SerializeField] private float gizmoRadius = 0.5f;
    [SerializeField] private Color gizmoColor = Color.green;

    public Vector3 SpawnPosition => transform.position;
    public Quaternion SpawnRotation => transform.rotation;

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;

        Gizmos.DrawWireSphere(transform.position, gizmoRadius);

        Gizmos.DrawLine(
            transform.position,
            transform.position + transform.forward * 1.5f
        );
    }
}
