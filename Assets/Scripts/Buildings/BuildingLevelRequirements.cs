using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildingLevelRequirements : MonoBehaviour
{
    [Tooltip("Minimum Resort Level required to start repairing this building.")]
    [SerializeField, Min(1)]
    private int minimumRepairResortLevel = 1;

    [Tooltip("Element 0 is for building level 2, element 1 is for building level 3, and so on.")]
    [SerializeField]
    private List<int> minimumUpgradeResortLevels = new();

    public int MinimumRepairResortLevel =>
        Mathf.Max(1, minimumRepairResortLevel);

    public int GetMinimumUpgradeResortLevel(int targetBuildingLevel)
    {
        if (targetBuildingLevel < 2)
            return 1;

        int index = targetBuildingLevel - 2;

        if (index >= minimumUpgradeResortLevels.Count)
            return 1;

        return Mathf.Max(1, minimumUpgradeResortLevels[index]);
    }
}