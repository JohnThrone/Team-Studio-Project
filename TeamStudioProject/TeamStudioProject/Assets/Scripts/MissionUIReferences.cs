using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Attach to the ROOT of your mission UI prefab. MissionManager grabs this
// component via GetComponent right after instantiating the prefab, and uses
// these fields to display the mission AND its confirmation popup — both
// appear together the moment a mission button is pressed.
public class MissionUIReferences : MonoBehaviour
{
    [Header("Mission Display")]
    public TextMeshProUGUI missionNameText;
    public TextMeshProUGUI missionDescriptionText;
    public TextMeshProUGUI eventText;

    [Header("Confirmation Popup (a child object within this same prefab)")]
    public GameObject confirmationPopup;
    public TextMeshProUGUI confirmationText;
    public Button confirmYesButton;
    public Button confirmNoButton;
}