using UnityEngine;
using TMPro;

public class AgentStats : MonoBehaviour
{
    [Header("Identity")]
    public string AgentName;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Core Stats")]
    public int Conflict;
    [SerializeField] private TextMeshProUGUI conflictText;

    public int Rhetoric;
    [SerializeField] private TextMeshProUGUI rhetoricText;

    public int Guile;
    [SerializeField] private TextMeshProUGUI guileText;

    [Header("Status")]
    public bool IsAvailable = false; // true = currently assigned to a mission (toggled by the drop zone)
    [SerializeField] private TextMeshProUGUI isAvailableText;

    private void Start()
    {
        UpdateAllText();
    }

    public void UpdateAllText()
    {
        if (nameText != null) nameText.text = AgentName;
        if (conflictText != null) conflictText.text = Conflict.ToString();
        if (rhetoricText != null) rhetoricText.text = Rhetoric.ToString();
        if (guileText != null) guileText.text = Guile.ToString();
        if (isAvailableText != null) isAvailableText.text = IsAvailable ? "Yes" : "No";
    }

    // Call this when the agent is killed on a mission. Only actually destroys
    // the GameObject if the agent is currently assigned to a mission
    // (IsAvailable == true) — otherwise this is a no-op, so Die() can never
    // accidentally remove an agent who wasn't participating.
    public void Die()
    {
        if (!IsAvailable)
        {
            Debug.LogWarning($"AgentStats: Die() called on {AgentName}, but they are not currently assigned to a mission. Ignoring.");
            return;
        }

        IsAvailable = false;
        Destroy(gameObject);
    }
}