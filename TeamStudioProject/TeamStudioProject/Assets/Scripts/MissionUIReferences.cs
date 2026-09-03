using UnityEngine;
using TMPro;

// Attach to the ROOT of your mission UI prefab. MissionManager grabs this
// component via GetComponent right after instantiating the prefab, and uses
// these three fields to display the active mission.
public class MissionUIReferences : MonoBehaviour
{
    public TextMeshProUGUI missionNameText;
    public TextMeshProUGUI missionDescriptionText;
    public TextMeshProUGUI eventText;
}