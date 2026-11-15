
using System;
using UnityEngine;
using UnityEngine.AI;

public enum TouristNavigationState
{
    AwaitingDestination,
    Walking,
    Arrived
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

    [Header("Debug")]
    [SerializeField]
    private TouristNavigationState state =
        TouristNavigationState.AwaitingDestination;

    [SerializeField] private Vector3 currentDestination;

    [Tooltip("Test only: target used by the Test / Walk To Test Target menu.")]
    [SerializeField] private Transform testTarget;

    private NavMeshAgent agent;

    public TouristNavigationState State => state;
    public Vector3 CurrentDestination => currentDestination;
    public bool IsOnNavMesh => agent != null && agent.isOnNavMesh;

    public event Action<TouristNavigation> OnDestinationReached;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

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
        if (state != TouristNavigationState.Walking || !agent.isOnNavMesh)
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
        }
    }

    // Snaps the tourist onto the nearest NavMesh point, so a spawn
    // point that is slightly off the mesh still works.
    private bool PlaceOnNavMesh()
    {
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
            $"{name}: No NavMesh near spawn position " +
            $"{transform.position}. Is the NavMesh baked?",
            this
        );
        return false;
    }

    public bool MoveTo(Vector3 destination)
    {
        if (!agent.isOnNavMesh && !PlaceOnNavMesh())
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

        if (!agent.SetDestination(hit.position))
        {
            return false;
        }

        currentDestination = hit.position;
        agent.isStopped = false;
        state = TouristNavigationState.Walking;

        Debug.Log($"{name}: walking to {currentDestination}", this);
        return true;
    }

    public void Stop()
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        state = TouristNavigationState.AwaitingDestination;
    }

    private void Arrive()
    {
        agent.isStopped = true;
        agent.ResetPath();

        state = TouristNavigationState.Arrived;

        Debug.Log($"{name}: arrived at {currentDestination}", this);

        OnDestinationReached?.Invoke(this);
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
