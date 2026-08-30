using UnityEngine;
using TMPro;

public class PlayerStats : MonoBehaviour
{
    [Header("Player Stats")]
    public int Secrecy;
    [SerializeField] private TextMeshProUGUI secrecyText;

    public int Legitimacy;
    [SerializeField] private TextMeshProUGUI legitimacyText;

    private void Start()
    {
        UpdateAllText();
    }

    public void UpdateAllText()
    {
        if (secrecyText != null) secrecyText.text = Secrecy.ToString();
        if (legitimacyText != null) legitimacyText.text = Legitimacy.ToString();
    }

    // Hook this to MissionManager's OnPlayerStatChange event in the Inspector.
    // Matches the outcome's statName string against these fields.
    public void ApplyStatChange(string statName, int amount)
    {
        if (statName == "Secrecy")
        {
            Secrecy += amount;
        }
        else if (statName == "Legitimacy")
        {
            Legitimacy += amount;
        }
        else
        {
            Debug.LogWarning($"PlayerStats: '{statName}' does not match a known stat.");
        }

        UpdateAllText();
    }
}
