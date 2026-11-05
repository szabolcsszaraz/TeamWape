
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BuildingCapacityWarningUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private TMP_Text warningTitle;
    [SerializeField] private TMP_Text warningText;

    private readonly List<ServiceBuilding> buildings = new();

    private void Start()
    {
        // Find all service buildings in the scene.
        ServiceBuilding[] foundBuildings =
            FindObjectsByType<ServiceBuilding>(
                FindObjectsSortMode.None
            );

        foreach (ServiceBuilding building in foundBuildings)
        {
            buildings.Add(building);

            // Listen for capacity state changes.
            building.OnServiceStateChanged += HandleStateChanged;
        }

        // Hide the warning initially.
        RefreshWarning();
    }

    private void HandleStateChanged(
        ServiceBuilding building,
        ServiceLoadState state)
    {
        Debug.Log(
            $"{building.name} capacity state: {state}"
        );

        RefreshWarning();
    }

    private void RefreshWarning()
    {
        if (warningPanel == null)
        {
            return;
        }

        List<ServiceBuilding> overloadedBuildings = new();

        foreach (ServiceBuilding building in buildings)
        {
            if (building != null &&
                building.CurrentState == ServiceLoadState.Overloaded)
            {
                overloadedBuildings.Add(building);
            }
        }

        // No overloaded buildings: hide the panel.
        if (overloadedBuildings.Count == 0)
        {
            warningPanel.SetActive(false);
            return;
        }

        warningPanel.SetActive(true);

        if (warningTitle != null)
        {
            warningTitle.text = "INSUFFICIENT CAPACITY!";
        }

        if (warningText != null)
        {
            List<string> messages = new();

            foreach (ServiceBuilding building in overloadedBuildings)
            {
                messages.Add(
                    $"{building.name}: " +
                    $"Demand {building.TotalLoad} / " +
                    $"Capacity {building.Seats}"
                );
            }

            warningText.text = string.Join("\n", messages);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events.
        foreach (ServiceBuilding building in buildings)
        {
            if (building != null)
            {
                building.OnServiceStateChanged -= HandleStateChanged;
            }
        }

        buildings.Clear();
    }
}
