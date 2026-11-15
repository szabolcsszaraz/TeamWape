
using System;
using UnityEngine;
using UnityEngine.AI;

public enum TouristNavigationState
{
    AwaitingDestination,
    Walking,
    Arrived,
    Unreachable,
    InsideBuilding
}

[RequireComponent(typeof(NavMeshAgent))]
public class TouristNavigation : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float walkSpeed = 2.5f;

    [Tooltip("How close the tourist must get to count as arrived.")]
    [SerializeField, Min(0.05f)] private float arriveDistance = 0.4f;

    [Tooltip("Max distance to search for the NavMesh around a position.")]
    [SerializeField, Min(0.1f)] private float navMeshSearchRadius = 2f;

    [Header("Unreachable Detection")]
    [Tooltip("If the tourist barely moves for this many seconds while " +
             "walking, the destination counts as unreachable.")]
    [SerializeField, Min(0.5f)] private float stuckTimeout = 4f;

    [Tooltip("Speed below which the tourist counts as not moving.")]
    [SerializeField, Min(0f)] private float stuckSpeedThreshold = 0.05f;

    [Tooltip("When stuck this close to the target (e.g. crowd at the " +
             "door), the tourist counts as arrived instead.")]
    [SerializeField, Min(0f)] private float crowdArriveDistance = 1.5f;

    [Header("Debug")]
    [SerializeField]
    private TouristNavigationState state =
        TouristNavigationState.AwaitingDestination;

    [SerializeField] private Vector3 currentDestination;

    [Tooltip("Test only: target used by the Test / Walk To Test Target menu.")]
    [SerializeField] private Transform testTarget;

    private NavMeshAgent agent;
    private Renderer[] renderers;
    private Collider[] colliders;
    private float stuckTimer;

    public TouristNavigationState State => state;
    public Vector3 CurrentDestination => currentDestination;
    public bool IsOnNavMesh => agent != null && agent.enabled && agent.isOnNavMesh;
    public bool IsVisible { get; private set; } = true;

    public event Action<TouristNavigation> OnDestinationReached;
    public event Action<TouristNavigation> OnDestinationUnreachable;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        agent.speed = walkSpeed;
        agent.stoppingDistance = arriveDistance * 0.5f;
        agent.autoBraking = true;
    }

    private void Start()
    {
        PlaceOnNavMesh();
    }

    private void Update()
    {
        if (state != TouristNavigationState.Walking || !IsOnNavMesh)
        {
            return;
        }

        if (agent.pathPending)
        {
            return;
        }

        if (agent.remainingDistance <= arriveDistance)
        {
            Arrive();
            return;
        }

        UpdateStuckCheck();
    }

    private void UpdateStuckCheck()
    {
        bool isMoving = agent.velocity.sqrMagnitude >
                        stuckSpeedThreshold * stuckSpeedThreshold;

        // A path that became partial/invalid can never be finished.
        bool pathBroken = agent.pathStatus != NavMeshPathStatus.PathComplete;

        if (isMoving && !pathBroken)
        {
            stuckTimer = 0f;
            return;
        }

        stuckTimer += Time.deltaTime;

        if (stuckTimer < stuckTimeout)
        {
            return;
        }

        // Blocked by other tourists right next to the entrance:
        // close enough, count it as arrived.
        if (!pathBroken && agent.remainingDistance <= crowdArriveDistance)
        {
            Arrive();
            return;
        }

        FailDestination(pathBroken ? "path is broken" : "tourist is stuck");
    }

    // Snaps the tourist onto the nearest NavMesh point, so a spawn
    // point that is slightly off the mesh still works.
    private bool PlaceOnNavMesh()
    {
        if (!agent.enabled)
        {
            return false;
        }

        if (agent.isOnNavMesh)
        {
            return true;
        }

        if (NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit hit,
                navMeshSearchRadius,
                NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            return true;
        }

        Debug.LogWarning(
            $"{name}: No NavMesh near position " +
            $"{transform.position}. Is the NavMesh baked?",
            this
        );
        return false;
    }

    // Returns false if the destination is off the NavMesh or no
    // complete path leads there (SCRUM-278).
    public bool MoveTo(Vector3 destination)
    {
        if (!IsOnNavMesh && !PlaceOnNavMesh())
        {
            return false;
        }

        if (!NavMesh.SamplePosition(
                destination,
                out NavMeshHit hit,
                navMeshSearchRadius,
                NavMesh.AllAreas))
        {
            Debug.LogWarning(
                $"{name}: Destination {destination} is not on the NavMesh.",
                this
            );
            return false;
        }

        NavMeshPath path = new NavMeshPath();

        if (!agent.CalculatePath(hit.position, path) ||
            path.status != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning(
                $"{name}: Destination {hit.position} is unreachable " +
                $"(path: {path.status}).",
                this
            );
            return false;
        }

        agent.SetPath(path);

        currentDestination = hit.position;
        agent.isStopped = false;
        stuckTimer = 0f;
        state = TouristNavigationState.Walking;

        return true;
    }

    public bool CanReach(Vector3 destination)
    {
        if (!IsOnNavMesh ||
            !NavMesh.SamplePosition(
                destination,
                out NavMeshHit hit,
                navMeshSearchRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();

        return agent.CalculatePath(hit.position, path) &&
               path.status == NavMeshPathStatus.PathComplete;
    }

    public void Stop()
    {
        if (IsOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        state = TouristNavigationState.AwaitingDestination;
    }

    // Hides the tourist as if it walked into a building. The object
    // stays alive, so all its data is kept (SCRUM-279).
    public void EnterBuilding()
    {
        Stop();

        agent.enabled = false;
        SetVisible(false);

        state = TouristNavigationState.InsideBuilding;
    }

    // Shows the tourist again at the given position (usually the
    // building's interaction point).
    public void ExitBuilding(Vector3 position)
    {
        if (NavMesh.SamplePosition(
                position,
                out NavMeshHit hit,
                navMeshSearchRadius,
                NavMesh.AllAreas))
        {
            position = hit.position;
        }

        transform.position = position;

        agent.enabled = true;
        agent.Warp(position);

        SetVisible(true);

        state = TouristNavigationState.AwaitingDestination;
    }

    private void SetVisible(bool visible)
    {
        IsVisible = visible;

        foreach (Renderer rend in renderers)
        {
            if (rend != null)
            {
                rend.enabled = visible;
            }
        }

        foreach (Collider col in colliders)
        {
            if (col != null)
            {
                col.enabled = visible;
            }
        }
    }

    private void Arrive()
    {
        agent.isStopped = true;
        agent.ResetPath();

        state = TouristNavigationState.Arrived;

        OnDestinationReached?.Invoke(this);
    }

    private void FailDestination(string reason)
    {
        Debug.LogWarning(
            $"{name}: could not reach {currentDestination} ({reason}).",
            this
        );

        if (IsOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        stuckTimer = 0f;
        state = TouristNavigationState.Unreachable;

        OnDestinationUnreachable?.Invoke(this);
    }

    private void OnValidate()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (agent != null)
        {
            agent.speed = walkSpeed;
        }
    }

    [ContextMenu("Test / Walk To Test Target")]
    private void TestWalkToTarget()
    {
        if (testTarget == null)
        {
            Debug.LogWarning($"{name}: Test Target is not set.", this);
            return;
        }

        MoveTo(testTarget.position);
    }
}
