
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TouristNeeds : MonoBehaviour
{
    [Header("Need Assignment")]
    [SerializeField, Min(1)] private int minNeeds = 1;
    [SerializeField, Min(1)] private int maxNeeds = 3;
    [SerializeField] private bool alwaysNeedsRoom = true;

    [Header("Assigned Needs")]
    [SerializeField]
    private List<GuestNeedType> needs = new();

    public IReadOnlyList<GuestNeedType> Needs => needs;

    private void Start()
    {
        GuestNeedsManager needsManager = GuestNeedsManager.Instance;

        if (needsManager == null)
        {
            Debug.LogWarning(
                $"{gameObject.name}: GuestNeedsManager not found!"
            );
            return;
        }

        AssignNeeds(needsManager.UnlockedNeedTypes);

        foreach (GuestNeedType needType in needs)
        {
            needsManager.AddDemand(needType, 1);
        }

        Debug.Log(
            $"{gameObject.name} needs: " +
            string.Join(", ", needs.Select(need => need.ToDisplayName()))
        );
    }

    private void AssignNeeds(IReadOnlyList<GuestNeedType> unlockedNeedTypes)
    {
        needs.Clear();

        List<GuestNeedType> candidates = new(unlockedNeedTypes);

        if (candidates.Count == 0)
        {
            return;
        }

        int needCount = Mathf.Min(
            Random.Range(minNeeds, maxNeeds + 1),
            candidates.Count
        );

        if (alwaysNeedsRoom && candidates.Remove(GuestNeedType.Rooms))
        {
            needs.Add(GuestNeedType.Rooms);
        }

        while (needs.Count < needCount && candidates.Count > 0)
        {
            int index = Random.Range(0, candidates.Count);

            needs.Add(candidates[index]);
            candidates.RemoveAt(index);
        }
    }

    private void OnValidate()
    {
        if (maxNeeds < minNeeds)
        {
            maxNeeds = minNeeds;
        }
    }
}
