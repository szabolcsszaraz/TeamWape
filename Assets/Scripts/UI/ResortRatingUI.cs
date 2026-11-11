
using TMPro;
using UnityEngine;

public class ResortRatingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private TMP_Text ratingText;

    private ResortRatingManager ratingManager;

    private void Start()
    {
        ratingManager =
            FindFirstObjectByType<ResortRatingManager>();

        if (ratingManager == null)
        {
            Debug.LogError(
                "ResortRatingUI: ResortRatingManager not found!"
            );
            enabled = false;
            return;
        }

        if (ratingText == null)
        {
            Debug.LogError(
                "ResortRatingUI: Rating Text is not assigned!"
            );
            enabled = false;
            return;
        }

        ratingManager.OnRatingChanged += UpdateRating;

        // Display the current rating immediately.
        UpdateRating(ratingManager.CurrentRating);
    }

    private void UpdateRating(float rating)
    {
        float stars = Mathf.Clamp(rating, 0f, 100f) / 20f;

        ratingText.text = $"{stars:F1}/5";
    }

    private void OnDestroy()
    {
        if (ratingManager != null)
        {
            ratingManager.OnRatingChanged -= UpdateRating;
        }
    }
}
