using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class MissionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Availability")]
    [Tooltip("Must be set to true (Yes) for this mission to be activatable. If false, pressing the button logs 'Mission not available yet.'")]
    public bool isAvailable = false;

    [Header("Mission Data")]
    public Mission mission;

    [Header("Agent Assignment Limit")]
    [Range(1, 3)]
    [Tooltip("Maximum number of agents that can be assigned to this mission. Minimum is always 1 (enforced by MissionManager).")]
    public int maxAssignedAgents = 3;

    [Header("Rewards (unlocked when this mission is completed SUCCESSFULLY)")]
    public List<MissionButton> missionsToUnlock = new List<MissionButton>();

    [Header("Locking")]
    [Tooltip("If true, this mission becomes unavailable again after being completed successfully — a one-time mission.")]
    public bool lockAfterCompletion = true;

    [Header("Hover Highlight")]
    [Tooltip("The child TextMeshPro object showing the mission's name.")]
    [SerializeField] private TextMeshProUGUI missionNameText;
    [SerializeField] private Color availableHighlightColor = Color.green;
    [SerializeField] private Color unavailableHighlightColor = Color.red;
    private Color originalTextColor;
    private bool hasStoredOriginalColor = false;

    [Header("Reference")]
    [SerializeField] private MissionManager missionManager;

    // Hook this to the Button component's OnClick() event
    public void OnButtonPressed()
    {
        if (missionManager != null)
        {
            missionManager.RequestActivateMission(this);
        }
        else
        {
            Debug.LogWarning("MissionButton: Mission Manager reference is not assigned.");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (missionNameText == null) return;

        if (!hasStoredOriginalColor)
        {
            originalTextColor = missionNameText.color;
            hasStoredOriginalColor = true;
        }

        missionNameText.color = isAvailable ? availableHighlightColor : unavailableHighlightColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (missionNameText == null || !hasStoredOriginalColor) return;
        missionNameText.color = originalTextColor;
    }
}