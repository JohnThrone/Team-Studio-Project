using UnityEngine;
using TMPro;

// Attach one of these to each of the three result slots in the Skill Check
// Popup (Conflict, Guile, Rhetoric), arranged left to right. MissionManager
// drives the timing — this component just holds the visuals for one slot.
public class SkillCheckResultSlot : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("Plays the 2D UI image sequence animation. All three slots can share the same Animator Controller asset.")]
    [SerializeField] private Animator animator;
    [SerializeField] private string playAnimationTrigger = "Play";

    [Header("Breakdown Text (optional)")]
    [SerializeField] private TextMeshProUGUI breakdownText;

    [Header("Result Images (hidden until revealed)")]
    [SerializeField] private GameObject successImage;
    [SerializeField] private GameObject failureImage;

    private bool pendingSuccess;

    // Called by MissionManager right before this slot's animation starts.
    public void BeginCheck(bool success, string breakdownTextValue)
    {
        pendingSuccess = success;

        if (breakdownText != null) breakdownText.text = breakdownTextValue;
        if (successImage != null) successImage.SetActive(false);
        if (failureImage != null) failureImage.SetActive(false);

        if (animator != null) animator.SetTrigger(playAnimationTrigger);
    }

    // Called by MissionManager once the fixed animation duration has elapsed.
    public void Reveal()
    {
        if (pendingSuccess)
        {
            if (successImage != null) successImage.SetActive(true);
        }
        else
        {
            if (failureImage != null) failureImage.SetActive(true);
        }
    }
}