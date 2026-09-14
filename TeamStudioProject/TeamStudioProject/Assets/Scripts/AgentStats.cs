using UnityEngine;
using UnityEngine.UI;
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
    // NOTE: despite the name, this currently means "assigned to a mission" —
    // it's set true by the drop zone and used by MissionManager to pull an
    // active roster. Kept as-is to avoid breaking every script that already
    // references it.
    public bool IsAvailable = false;
    [SerializeField] private TextMeshProUGUI isAvailableText;

    public bool isInjured = false;
    [SerializeField] private TextMeshProUGUI isInjuredText;

    [Header("Progression")]
    public int SkillPoints = 0;
    [SerializeField] private TextMeshProUGUI skillPointsText;

    [Header("Portrait (swaps automatically when injured)")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private Sprite normalPortrait;
    [SerializeField] private Sprite injuredPortrait;

    [Header("Sound Effects")]
    [Tooltip("Add an AudioSource component to this GameObject and assign it here. Used for the Injured sound; the Death sound plays independently since this GameObject is destroyed.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip injuredSound;
    [SerializeField] private AudioClip deathSound;

    // Fired only when an agent is actually destroyed (the second time they'd
    // be killed while already injured). Other scripts (e.g. PlayerStats) can
    // subscribe to react to a permanent death without needing a direct reference.
    public static event System.Action<AgentStats> OnAnyAgentPermanentlyDied;

    // Fired for any player-facing message this agent wants shown in the UI
    // (e.g. the Activity Log), for situations that happen outside of a mission
    // context (like the roster/upgrade screen) where there's no MissionManager
    // in the loop to route a message through.
    public static event System.Action<string> OnAgentLogMessage;

    private static void LogMessage(string message)
    {
        OnAgentLogMessage?.Invoke(message);
        Debug.Log(message);
    }

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
        if (isInjuredText != null) isInjuredText.text = isInjured ? "Yes" : "No";
        if (skillPointsText != null) skillPointsText.text = SkillPoints.ToString();

        if (portraitImage != null)
        {
            if (isInjured && injuredPortrait != null) portraitImage.sprite = injuredPortrait;
            else if (normalPortrait != null) portraitImage.sprite = normalPortrait;
        }
    }

    // Call this when the agent is killed on a mission.
    // First time: the agent survives but becomes injured (isInjured -> true), and is
    //             unassigned from the mission (IsAvailable -> false).
    // Second time (already injured): this time it's fatal — the GameObject is destroyed.
    public void Die()
    {
        if (!IsAvailable)
        {
            Debug.LogWarning($"AgentStats: Die() called on {AgentName}, but they are not currently assigned to a mission. Ignoring.");
            return;
        }

        if (isInjured)
        {
            IsAvailable = false;
            LogMessage($"{AgentName} has died.");
            // PlayClipAtPoint (not the local AudioSource) so the sound survives past this frame,
            // since Destroy(gameObject) below would otherwise cut a normal PlayOneShot() short.
            if (deathSound != null) AudioSource.PlayClipAtPoint(deathSound, transform.position);
            OnAnyAgentPermanentlyDied?.Invoke(this);
            Destroy(gameObject);
        }
        else
        {
            isInjured = true;
            IsAvailable = false;
            LogMessage($"{AgentName} was injured and is out of action.");
            PlaySound(injuredSound);
            UpdateAllText();
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Call this when a mission ends, to unassign every agent in the scene.
    public static void UnassignAllAgents()
    {
        AgentStats[] allAgents = Object.FindObjectsByType<AgentStats>(FindObjectsSortMode.None);
        foreach (AgentStats agent in allAgents)
        {
            agent.IsAvailable = false;
            agent.UpdateAllText();
        }
    }

    // Call this on every unassigned + injured agent after a mission ends, to heal them.
    public static void HealAllUnassignedInjuredAgents()
    {
        AgentStats[] allAgents = Object.FindObjectsByType<AgentStats>(FindObjectsSortMode.None);
        foreach (AgentStats agent in allAgents)
        {
            if (!agent.IsAvailable && agent.isInjured)
            {
                agent.isInjured = false;
                agent.UpdateAllText();
            }
        }
    }

    // Hook a Button's OnClick() on each stat number to this, passing the exact
    // field name ("Conflict", "Rhetoric", or "Guile"). Only works while the
    // agent is unassigned (IsAvailable == false) and only if SkillPoints > 0.
    public void IncreaseStat(string statName)
    {
        if (IsAvailable)
        {
            LogMessage($"{AgentName} is currently assigned to a mission and cannot be upgraded.");
            return;
        }

        if (SkillPoints <= 0)
        {
            LogMessage($"{AgentName} has no Skill Points to spend.");
            return;
        }

        switch (statName)
        {
            case "Conflict": Conflict++; break;
            case "Rhetoric": Rhetoric++; break;
            case "Guile": Guile++; break;
            default:
                Debug.LogWarning($"AgentStats: '{statName}' is not a recognized stat for IncreaseStat().");
                return;
        }

        SkillPoints--;
        UpdateAllText();
    }
}