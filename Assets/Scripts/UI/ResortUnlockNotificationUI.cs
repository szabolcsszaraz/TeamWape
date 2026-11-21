using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ResortUnlockNotificationUI : MonoBehaviour
{
    private const string IslandAvailableMessage = "Second island available for purchase";

    [Header("Unlock Sources")]
    [SerializeField] private ResortUnlockManager unlockManager;
    [SerializeField] private BuildingRepair poolRepair;
    [SerializeField] private BuildingUpgrade hotelUpgrade;
    [SerializeField] private ResortAreaRequirements secondIsland;

    [Header("Notification")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField, Min(0.1f)] private float displayDuration = 4f;

    private readonly List<string> visibleMessages = new();

    private ResortArea secondIslandArea;
    private bool poolUnlockNotified;
    private bool hotelUnlockNotified;
    private bool islandUnlockNotified;
    private bool checkPending;
    private float hideAt;

    private void Start()
    {
        if (unlockManager == null ||
            poolRepair == null ||
            hotelUpgrade == null ||
            secondIsland == null ||
            notificationPanel == null ||
            messageText == null)
        {
            Debug.LogError(
                "ResortUnlockNotificationUI: Assign all references in the Inspector.",
                this
            );
            enabled = false;
            return;
        }

        secondIslandArea = secondIsland.GetComponent<ResortArea>();

        if (secondIslandArea == null)
        {
            Debug.LogError(
                "ResortUnlockNotificationUI: The second island needs a ResortArea component.",
                this
            );
            enabled = false;
            return;
        }

        notificationPanel.SetActive(false);

        // Content available at the start does not need an unlock message.
        poolUnlockNotified = poolRepair.CanRepair;
        hotelUnlockNotified =
            hotelUpgrade.CanUpgrade && hotelUpgrade.MeetsResortLevelRequirement;
        islandUnlockNotified =
            secondIsland.RequirementsMet ||
            secondIslandArea.CurrentState == AreaState.Unlocked;

        unlockManager.OnUnlockProgressChanged += ScheduleUnlockCheck;
        secondIsland.OnRequirementsChanged += ScheduleUnlockCheck;
        secondIslandArea.OnStateChanged += HandleSecondIslandStateChanged;
    }

    private void Update()
    {
        if (visibleMessages.Count > 0 && Time.unscaledTime >= hideAt)
        {
            visibleMessages.Clear();
            notificationPanel.SetActive(false);
        }

        if (!checkPending)
            return;

        checkPending = false;
        CheckNewUnlocks();
    }

    private void ScheduleUnlockCheck()
    {
        // Collect changes from the same progression update before showing them.
        checkPending = true;
    }

    private void CheckNewUnlocks()
    {
        int previousMessageCount = visibleMessages.Count;

        if (!poolUnlockNotified && poolRepair.CanRepair)
        {
            poolUnlockNotified = true;
            visibleMessages.Add("Pool repair unlocked");
        }

        if (!hotelUnlockNotified &&
            hotelUpgrade.CanUpgrade &&
            hotelUpgrade.MeetsResortLevelRequirement)
        {
            hotelUnlockNotified = true;
            visibleMessages.Add("Hotel upgrade unlocked");
        }

        if (!islandUnlockNotified && secondIsland.RequirementsMet)
        {
            islandUnlockNotified = true;
            visibleMessages.Add(IslandAvailableMessage);
        }

        if (visibleMessages.Count == previousMessageCount)
            return;

        ShowMessages();
    }

    private void HandleSecondIslandStateChanged(AreaState state)
    {
        if (state != AreaState.Unlocked)
            return;

        islandUnlockNotified = true;

        if (Time.unscaledTime >= hideAt)
            visibleMessages.Clear();

        visibleMessages.Remove(IslandAvailableMessage);
        visibleMessages.Add("Second island unlocked");

        ShowMessages();
    }

    private void ShowMessages()
    {
        messageText.text = string.Join("\n", visibleMessages);
        hideAt = Time.unscaledTime + displayDuration;
        notificationPanel.SetActive(true);
    }

    private void OnDestroy()
    {
        if (unlockManager != null)
            unlockManager.OnUnlockProgressChanged -= ScheduleUnlockCheck;

        if (secondIsland != null)
            secondIsland.OnRequirementsChanged -= ScheduleUnlockCheck;

        if (secondIslandArea != null)
            secondIslandArea.OnStateChanged -= HandleSecondIslandStateChanged;
    }
}