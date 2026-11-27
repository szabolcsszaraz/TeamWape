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

    [Tooltip("Completed waves required to start repairing this building. 0 = no wave requirement.")]
    [SerializeField, Min(0)]
    private int minimumRepairCompletedWaves;

    [Tooltip("Guests start wanting this building's service as soon as its repair " +
             "unlocks (e.g. wave 6), even while it is still a ruin. " +
             "Off = the service unlocks only when the repair is finished.")]
    [SerializeField]
    private bool unlockNeedBeforeRepair;

    public bool UnlockNeedBeforeRepair => unlockNeedBeforeRepair;

    public int MinimumRepairResortLevel =>
        Mathf.Max(1, minimumRepairResortLevel);

    public int MinimumRepairCompletedWaves =>
        Mathf.Max(0, minimumRepairCompletedWaves);

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