
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Walks the tourist to a service building for each of its needs,
// in the order the needs were assigned (Rooms first).
[RequireComponent(typeof(TouristNavigation))]
[RequireComponent(typeof(TouristNeeds))]
public class TouristServiceRoute : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private List<GuestNeedType> remainingNeeds = new();
    [SerializeField] private ServiceNavigationTarget currentTarget;
    [SerializeField] private bool routeCompleted;

    private TouristNavigation navigation;
    private TouristNeeds touristNeeds;

    public ServiceNavigationTarget CurrentTarget => currentTarget;
    public IReadOnlyList<GuestNeedType> RemainingNeeds => remainingNeeds;
    public bool RouteCompleted => routeCompleted;

    public event Action<TouristServiceRoute, ServiceNavigationTarget>
        OnServiceReached;

    public event Action<TouristServiceRoute> OnRouteCompleted;

    private void Awake()
    {
        navigation = GetComponent<TouristNavigation>();
        touristNeeds = GetComponent<TouristNeeds>();
    }

    private void OnEnable()
    {
        navigation.OnDestinationReached += HandleDestinationReached;
    }

    private void OnDisable()
    {
        navigation.OnDestinationReached -= HandleDestinationReached;
    }

    private IEnumerator Start()
    {
        // Wait one frame so TouristNeeds has assigned the needs.
        yield return null;

        remainingNeeds.Clear();
        remainingNeeds.AddRange(touristNeeds.Needs);

        GoToNextService();
    }

    private void GoToNextService()
    {
        currentTarget = null;

        while (remainingNeeds.Count > 0)
        {
            GuestNeedType need = remainingNeeds[0];
            remainingNeeds.RemoveAt(0);

            ServiceNavigationTarget target =
                ServiceNavigationTarget.FindNearest(need, transform.position);

            if (target == null)
            {
                Debug.LogWarning(
                    $"{name}: No building with a ServiceNavigationTarget " +
                    $"serves {need.ToDisplayName()}. Skipping.",
                    this
                );
                continue;
            }

            if (navigation.MoveTo(target.InteractionPosition))
            {
                currentTarget = target;

                Debug.Log(
                    $"{name}: going to {target.name} " +
                    $"for {need.ToDisplayName()}",
                    this
                );
                return;
            }
        }

        CompleteRoute();
    }

    private void HandleDestinationReached(TouristNavigation nav)
    {
        if (currentTarget == null)
        {
            return;
        }

        ServiceNavigationTarget reachedTarget = currentTarget;

        Debug.Log($"{name}: reached {reachedTarget.name}", this);

        OnServiceReached?.Invoke(this, reachedTarget);

        GoToNextService();
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
}
