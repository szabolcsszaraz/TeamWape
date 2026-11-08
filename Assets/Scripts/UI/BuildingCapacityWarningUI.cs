
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BuildingCapacityWarningUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private TMP_Text warningTitle;
    [SerializeField] private TMP_Text warningText;

    [Header("Warning Settings")]
    [SerializeField, Min(0.1f)]
    private float refreshInterval = 0.25f;

    private readonly List<ServiceBuilding> buildings = new();

    private float nextRefreshTime;

    private void Start()
    {
        // Find service buildings in the scene.
        ServiceBuilding[] foundBuildings =
            FindObjectsByType<ServiceBuilding>(
                FindObjectsSortMode.None
            );

        foreach (ServiceBuilding building in foundBuildings)
        {
            if (building == null)
                continue;

            buildings.Add(building);

            building.OnServiceStateChanged +=
                HandleStateChanged;
        }

        RefreshWarning();
    }

    private void Update()
    {
        // Update warnings periodically because waiting
        // requests can change without a state change.
        if (Time.time < nextRefreshTime)
            return;

        nextRefreshTime = Time.time + refreshInterval;

        RefreshWarning();
    }

    private void HandleStateChanged(
        ServiceBuilding building,
        ServiceLoadState state)
    {
        RefreshWarning();
    }

    private void RefreshWarning()
    {
        if (warningPanel == null)
            return;

        List<string> inactiveMessages = new();
        List<string> overloadedMessages = new();

        foreach (ServiceBuilding building in buildings)
        {
            if (building == null)
                continue;

            // The building cannot serve guests because
            // no required workers are assigned.
            if (building.IsMissingRequiredWorkers &&
                building.WaitingRequests > 0)
            {
                inactiveMessages.Add(
                    $"{building.name}: No workers assigned! " +
                    $"Waiting guests: {building.WaitingRequests}"
                );
            }
            // The building is operating but does not
            // have enough service capacity.
            else if (building.CurrentState ==
                     ServiceLoadState.Overloaded)
            {
                overloadedMessages.Add(
                    $"{building.name}: " +
                    $"Demand {building.TotalLoad} / " +
                    $"Capacity {building.Seats}"
                );
            }
        }

        bool hasInactiveWarnings =
            inactiveMessages.Count > 0;

        bool hasOverloadWarnings =
            overloadedMessages.Count > 0;

        // Hide the panel if there are no problems.
        if (!hasInactiveWarnings && !hasOverloadWarnings)
        {
            warningPanel.SetActive(false);
            return;
        }

        warningPanel.SetActive(true);

        if (warningTitle != null)
        {
            if (hasInactiveWarnings && hasOverloadWarnings)
            {
                warningTitle.text = "SERVICE WARNINGS!";
            }
            else if (hasInactiveWarnings)
            {
                warningTitle.text = "NO WORKERS ASSIGNED!";
            }
            else
            {
                warningTitle.text = "INSUFFICIENT CAPACITY!";
            }
        }

        if (warningText != null)
        {
            List<string> allMessages = new();

            allMessages.AddRange(inactiveMessages);
            allMessages.AddRange(overloadedMessages);

            warningText.text = string.Join(
                "\n",
                allMessages
            );
        }
    }

    private void OnDestroy()
    {
        foreach (ServiceBuilding building in buildings)
        {
            if (building != null)
            {
                building.OnServiceStateChanged -=
                    HandleStateChanged;
            }
        }

        buildings.Clear();
    }
}
