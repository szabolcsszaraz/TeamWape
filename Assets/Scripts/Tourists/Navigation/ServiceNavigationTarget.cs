
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Marks where tourists stand when they use a service building.
[RequireComponent(typeof(ServiceBuilding))]
public class ServiceNavigationTarget : MonoBehaviour
{
    private static readonly List<ServiceNavigationTarget> activeTargets = new();

    public static IReadOnlyList<ServiceNavigationTarget> All => activeTargets;

    [Header("Interaction Point")]
    [Tooltip("Where tourists stop (e.g. in front of the door). " +
             "If empty, the closest NavMesh point to the building is used.")]
    [SerializeField] private Transform interactionPoint;

    [Tooltip("Max distance to search for the NavMesh around the point.")]
    [SerializeField, Min(0.5f)] private float navMeshSearchRadius = 6f;

    private ServiceBuilding building;

    public ServiceBuilding Building => building;
    public GuestNeedType ServiceNeed => building.ServiceNeed;

    // Building to look at when standing at the interaction point.
    public Vector3 BuildingPosition => transform.position;

    public Vector3 InteractionPosition
    {
        get
        {
            Vector3 source = interactionPoint != null
                ? interactionPoint.position
                : transform.position;

            if (NavMesh.SamplePosition(
                    source,
                    out NavMeshHit hit,
                    navMeshSearchRadius,
                    NavMesh.AllAreas))
            {
                return hit.position;
            }

            return source;
        }
    }

    private void Awake()
    {
        building = GetComponent<ServiceBuilding>();
    }

    private void OnEnable()
    {
        if (!activeTargets.Contains(this))
        {
            activeTargets.Add(this);
        }
    }

    private void OnDisable()
    {
        activeTargets.Remove(this);
    }

    public static ServiceNavigationTarget FindNearest(
        GuestNeedType need,
        Vector3 fromPosition)
    {
        ServiceNavigationTarget nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (ServiceNavigationTarget target in activeTargets)
        {
            if (target == null || target.ServiceNeed != need)
            {
                continue;
            }

            float distance =
                (target.InteractionPosition - fromPosition).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = target;
            }
        }

        return nearest;
    }

    private void OnDrawGizmos()
    {
        Vector3 point = InteractionPosition;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(point, 0.3f);
        Gizmos.DrawLine(point, transform.position);
    }
}
