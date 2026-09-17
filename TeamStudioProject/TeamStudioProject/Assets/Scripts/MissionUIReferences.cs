using UnityEngine;
using TMPro;

// Attach to the root of your permanent, always-in-scene Mission Panel (starts
// inactive in the Hierarchy). MissionManager shows/hides it directly — nothing
// is instantiated or destroyed anymore.
public class MissionUIReferences : MonoBehaviour
{
    [Header("Mission Display")]
    public TextMeshProUGUI missionNameText;
    public TextMeshProUGUI missionDescriptionText;
    public TextMeshProUGUI eventText;

    [Header("Confirmation Popup (a child object within this same panel)")]
    public GameObject confirmationPopup;
    public TextMeshProUGUI confirmationText;
    // Wire this popup's Yes/No buttons directly in the Inspector to
    // MissionManager.ConfirmYes() / MissionManager.ConfirmNo() — same pattern
    // as the Skill Check popup's Continue button.
}