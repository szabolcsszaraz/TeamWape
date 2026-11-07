
using TMPro;
using UnityEngine;

public class MainGameplayHUD : MonoBehaviour
{
    public static MainGameplayHUD Instance { get; private set; }

    [Header("HUD References")]
    [SerializeField] private TMP_Text coinsValue;
    [SerializeField] private TMP_Text resortRatingValue;
    [SerializeField] private TMP_Text satisfactionValue;
    [SerializeField] private TMP_Text resourcesValue;

    [Header("Temporary Test Values")]
    [SerializeField, Min(0)] private int coins = 2500;
    [SerializeField, Range(0f, 5f)]
    private float resortRating = 0f;
    [SerializeField, Min(0)] private int resources = 150;

    private GuestSatisfactionManager satisfactionManager;

    public int Coins => coins;
    public float ResortRating => resortRating;
    public int Resources => resources;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        satisfactionManager = GuestSatisfactionManager.Instance;

        if (satisfactionManager != null)
        {
            satisfactionManager.OnSatisfactionChanged
                += HandleSatisfactionChanged;
        }
        else
        {
            Debug.LogWarning(
                "MainGameplayHUD: GuestSatisfactionManager not found!"
            );
        }

        RefreshHUD();
    }

    private void RefreshHUD()
    {
        UpdateCoinsText();
        UpdateResortRatingText();
        UpdateResourcesText();

        if (satisfactionManager != null)
        {
            HandleSatisfactionChanged(
                satisfactionManager.Satisfaction
            );
        }
        else if (satisfactionValue != null)
        {
            satisfactionValue.text = "--";
        }
    }

    private void UpdateCoinsText()
    {
        if (coinsValue != null)
            coinsValue.text = coins.ToString("N0");
    }

    private void UpdateResortRatingText()
    {
        if (resortRatingValue != null)
            resortRatingValue.text = $"{resortRating:0.0} / 5";
    }

    private void UpdateResourcesText()
    {
        if (resourcesValue != null)
            resourcesValue.text = resources.ToString("N0");
    }

    private void HandleSatisfactionChanged(float satisfaction)
    {
        if (satisfactionValue != null)
            satisfactionValue.text = $"{satisfaction:0}%";
    }

    // Called by future economy systems.
    public void SetCoins(int amount)
    {
        coins = Mathf.Max(0, amount);
        UpdateCoinsText();
    }

    // Called by future resort rating systems.
    public void SetResortRating(float rating)
    {
        resortRating = Mathf.Clamp(rating, 0f, 5f);
        UpdateResortRatingText();
    }

    // Called by future resource systems.
    public void SetResources(int amount)
    {
        resources = Mathf.Max(0, amount);
        UpdateResourcesText();
    }

    [ContextMenu("Test / Add 100 Coins")]
    private void TestAddCoins()
    {
        SetCoins(coins + 100);
    }

    [ContextMenu("Test / Add 25 Resources")]
    private void TestAddResources()
    {
        SetResources(resources + 25);
    }

    [ContextMenu("Test / Increase Resort Rating")]
    private void TestIncreaseRating()
    {
        SetResortRating(resortRating + 0.5f);
    }

    private void OnDestroy()
    {
        if (satisfactionManager != null)
        {
            satisfactionManager.OnSatisfactionChanged
                -= HandleSatisfactionChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
