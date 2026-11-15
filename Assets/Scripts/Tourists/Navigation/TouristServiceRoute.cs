
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Walks the tourist to a service building for each of its needs
// (Rooms -> hotel, Food -> restaurant, Drinks -> bar, ...). On arrival
// it goes inside and joins the building's queue; it comes out only
// when the building has actually served it, then goes to the next
// need. After the last need it walks back to where it spawned (the
// dock) and leaves.
[RequireComponent(typeof(TouristNavigation))]
[RequireComponent(typeof(TouristNeeds))]
public class TouristServiceRoute : MonoBehaviour
{
    [Header("Visit")]
    [Tooltip("Leave a building after waiting this many seconds for a " +
             "free seat (e.g. no workers). 0 = wait forever.")]
    [SerializeField, Min(0f)] private float maxWaitForSeat = 60f;

    [Header("End Of Route")]
    [Tooltip("After the last need, walk back to the spawn point and leave.")]
    [SerializeField] private bool leaveAtEnd = true;

    [Header("Debug")]
    [SerializeField] private List<GuestNeedType> remainingNeeds = new();
    [SerializeField] private ServiceNavigationTarget currentTarget;
    [SerializeField] private bool isInsideBuilding;
    [SerializeField] private bool isLeaving;
    [SerializeField] private bool routeCompleted;

    private TouristNavigation navigation;
    private TouristNeeds touristNeeds;

    // Remaining buildings to try for the current need (nearest first).
    private readonly List<ServiceNavigationTarget> pendingCandidates = new();
    private Vector3 spawnPosition;
    private Coroutine visitCoroutine;

    // Need currently being walked to (reserved, not yet in a queue).
    private bool hasPendingNeed;
    private GuestNeedType pendingNeed;

    private GuestServiceTicket currentTicket;

    public ServiceNavigationTarget CurrentTarget => currentTarget;
    public IReadOnlyList<GuestNeedType> RemainingNeeds => remainingNeeds;
    public bool IsInsideBuilding => isInsideBuilding;
    public bool IsLeaving => isLeaving;
    public bool RouteCompleted => routeCompleted;

    public event Action<TouristServiceRoute, ServiceNavigationTarget>
        OnBuildingEntered;

    public event Action<TouristServiceRoute, ServiceNavigationTarget>
        OnBuildingExited;

    public event Action<TouristServiceRoute> OnRouteCompleted;

    private void Awake()
    {
        navigation = GetComponent<TouristNavigation>();
        touristNeeds = GetComponent<TouristNeeds>();
    }

    private void OnEnable()
    {
        navigation.OnDestinationReached += HandleDestinationReached;
        navigation.OnDestinationUnreachable += HandleDestinationUnreachable;
    }

    private void OnDisable()
    {
        navigation.OnDestinationReached -= HandleDestinationReached;
        navigation.OnDestinationUnreachable -= HandleDestinationUnreachable;
    }

    private IEnumerator Start()
    {
        // Wait one frame so TouristNeeds has assigned the needs and
        // TouristNavigation has snapped onto the NavMesh.
        yield return null;

        spawnPosition = transform.position;

        remainingNeeds.Clear();
        remainingNeeds.AddRange(touristNeeds.Needs);

        GoToNextService();
    }

    private void GoToNextService()
    {
        currentTarget = null;

        // First try the other buildings for the current need.
        if (TryNextCandidate())
        {
            return;
        }

        AbandonPendingNeed();

        while (remainingNeeds.Count > 0)
        {
            GuestNeedType need = remainingNeeds[0];
            remainingNeeds.RemoveAt(0);

            hasPendingNeed = true;
            pendingNeed = need;

            pendingCandidates.Clear();
            pendingCandidates.AddRange(
                ServiceNavigationTarget.GetTargetsFor(need, transform.position)
            );

            if (pendingCandidates.Count == 0)
            {
                Debug.LogWarning(
                    $"{name}: No building with a ServiceNavigationTarget " +
                    $"serves {need.ToDisplayName()}. Skipping.",
                    this
                );
                AbandonPendingNeed();
                continue;
            }

            if (TryNextCandidate())
            {
                return;
            }

            Debug.LogWarning(
                $"{name}: No reachable building for " +
                $"{need.ToDisplayName()}. Skipping.",
                this
            );
            AbandonPendingNeed();
        }

        FinishRoute();
    }

    private bool TryNextCandidate()
    {
        while (pendingCandidates.Count > 0)
        {
            ServiceNavigationTarget target = pendingCandidates[0];
            pendingCandidates.RemoveAt(0);

            if (target == null)
            {
                continue;
            }

            if (navigation.MoveTo(target.InteractionPosition))
            {
                currentTarget = target;

                Debug.Log(
                    $"{name}: going to {target.name} " +
                    $"for {target.ServiceNeed.ToDisplayName()}",
                    this
                );
                return true;
            }
        }

        return false;
    }

    private void FinishRoute()
    {
        if (leaveAtEnd && navigation.MoveTo(spawnPosition))
        {
            isLeaving = true;

            Debug.Log($"{name}: all needs done, leaving the resort.", this);
            return;
        }

        CompleteRoute();
    }

    private void HandleDestinationReached(TouristNavigation nav)
    {
        if (isLeaving)
        {
            CompleteRoute();
            Destroy(gameObject);
            return;
        }

        if (currentTarget == null)
        {
            return;
        }

        pendingCandidates.Clear();

        visitCoroutine = StartCoroutine(VisitBuilding(currentTarget));
    }

    // SCRUM-278: try another building for the same need, otherwise
    // move on to the next need.
    private void HandleDestinationUnreachable(TouristNavigation nav)
    {
        if (isLeaving)
        {
            CompleteRoute();
            Destroy(gameObject);
            return;
        }

        GoToNextService();
    }

    // The need will never be served: tell the needs system, so the
    // wave can still complete.
    private void AbandonPendingNeed()
    {
        if (!hasPendingNeed)
        {
            return;
        }

        hasPendingNeed = false;
        GuestNeedsManager.Instance?.AbandonDemand(pendingNeed);
    }

    // SCRUM-279: tourist stops at the interaction point, goes inside
    // and stays there until the building has served it.
    private IEnumerator VisitBuilding(ServiceNavigationTarget target)
    {
        EnterBuilding(target);

        hasPendingNeed = false;

        GuestNeedsManager needsManager = GuestNeedsManager.Instance;

        currentTicket = needsManager != null
            ? needsManager.RequestService(target.ServiceNeed)
            : null;

        if (currentTicket == null)
        {
            Debug.LogWarning(
                $"{name}: {target.name} cannot serve " +
                $"{target.ServiceNeed.ToDisplayName()} right now.",
                this
            );
        }
        else
        {
            float waitedForSeat = 0f;

            while (!currentTicket.IsFinished)
            {
                if (!currentTicket.IsInService)
                {
                    waitedForSeat += Time.deltaTime;

                    if (maxWaitForSeat > 0f &&
                        waitedForSeat >= maxWaitForSeat &&
                        needsManager.CancelService(currentTicket))
                    {
                        Debug.Log(
                            $"{name}: waited too long at {target.name}, " +
                            "giving up.",
                            this
                        );
                        break;
                    }
                }

                yield return null;
            }

            if (currentTicket.IsServed)
            {
                Debug.Log(
                    $"{name}: {target.ServiceNeed.ToDisplayName()} " +
                    $"need met at {target.name}.",
                    this
                );
            }
        }

        currentTicket = null;

        ExitBuilding(target);

        visitCoroutine = null;

        GoToNextService();
    }

    private void EnterBuilding(ServiceNavigationTarget target)
    {
        navigation.EnterBuilding();
        target.AddVisitor(this);
        isInsideBuilding = true;

        Debug.Log($"{name}: entered {target.name}", this);

        OnBuildingEntered?.Invoke(this, target);
    }

    private void ExitBuilding(ServiceNavigationTarget target)
    {
        target.RemoveVisitor(this);
        navigation.ExitBuilding(target.InteractionPosition);
        isInsideBuilding = false;

        Debug.Log($"{name}: left {target.name}", this);

        OnBuildingExited?.Invoke(this, target);
    }

    private void CompleteRoute()
    {
        if (routeCompleted)
        {
            return;
        }

        routeCompleted = true;

        Debug.Log($"{name}: route completed.", this);

        OnRouteCompleted?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (visitCoroutine != null)
        {
            StopCoroutine(visitCoroutine);
        }

        if (currentTarget != null)
        {
            currentTarget.RemoveVisitor(this);
        }

        // Release needs that will now never be served.
        GuestNeedsManager needsManager = GuestNeedsManager.Instance;

        if (needsManager == null)
        {
            return;
        }

        if (currentTicket != null)
        {
            needsManager.CancelService(currentTicket);
        }

        AbandonPendingNeed();

        foreach (GuestNeedType need in remainingNeeds)
        {
            needsManager.AbandonDemand(need);
        }

        remainingNeeds.Clear();
    }
}
