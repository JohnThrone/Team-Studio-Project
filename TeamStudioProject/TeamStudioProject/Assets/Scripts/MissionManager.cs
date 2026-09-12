using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

// A stat change applied to the PLAYER (PlayerStats script)
[System.Serializable]
public class StatOutcome
{
    public string statName;
    public int amount;
}

// A stat change applied to the AGENT(S) assigned to the mission.
// statName must exactly match a public int field name on AgentStats
// (e.g. "Conflict", "Rhetoric", "Guile", "SkillPoints", or any new field you add later).
[System.Serializable]
public class AgentStatOutcome
{
    public string statName;
    public int amount;
}

// One beat within a mission. Every turn (unless Requires Check is off) rolls
// all three skills — Conflict, Guile, and Rhetoric — against their own target
// values. The turn succeeds if at least 2 of the 3 checks pass.
[System.Serializable]
public class MissionTurn
{
    [TextArea] public string turnDescription;

    [Header("Skill Check (leave off for a narrative-only turn with no roll)")]
    public bool requiresCheck = true;

    [Header("Target Values (assigned agents' combined stat minus this = the roll modifier)")]
    public int conflictTarget;
    public int guileTarget;
    public int rhetoricTarget;

    [Header("Result Text")]
    [TextArea] public string successText;
    [TextArea] public string failText;

    [Header("Player Stat Effects")]
    public List<StatOutcome> successOutcomes = new List<StatOutcome>();
    public List<StatOutcome> failureOutcomes = new List<StatOutcome>();

    [Header("Agent Stat Effects (applied to every agent assigned to this mission)")]
    public List<AgentStatOutcome> successAgentOutcomes = new List<AgentStatOutcome>();
    public List<AgentStatOutcome> failureAgentOutcomes = new List<AgentStatOutcome>();

    [Header("Other Effects")]
    public bool failureKillsAgent;
    public bool endsMissionOnFailure;
}

// A full mission: name, description, and its own ordered sequence of turns.
// Each MissionButton holds one of these — missions are spawned individually on demand.
[System.Serializable]
public class Mission
{
    public string missionName;
    [TextArea] public string missionDescription;
    public List<MissionTurn> turns = new List<MissionTurn>();
}

[System.Serializable] public class PlayerStatEvent : UnityEvent<string, int> { }
[System.Serializable] public class LogEvent : UnityEvent<string> { }
[System.Serializable] public class AgentEvent : UnityEvent<AgentStats> { }

public class MissionManager : MonoBehaviour
{
    [Header("Mission Prefab & Spawn Point")]
    [Tooltip("Prefab with a MissionUIReferences component on its root, showing the mission's name/description/event text AND its own confirmation popup.")]
    [SerializeField] private GameObject missionUIPrefab;
    [SerializeField] private Transform missionSpawnPoint;

    [Header("Turn Tracking")]
    [SerializeField] private TurnCounter turnCounter; // global counter, never reset

    [Header("Skill Check Settings")]
    [Tooltip("A roll (d20 + modifier) equal to or above this succeeds that skill check.")]
    [SerializeField] private int difficultyThreshold = 10;

    [Header("Skill Check Popup (shown after every turn's three rolls)")]
    [SerializeField] private GameObject skillCheckPopup;
    [SerializeField] private TextMeshProUGUI conflictResultText;
    [SerializeField] private TextMeshProUGUI guileResultText;
    [SerializeField] private TextMeshProUGUI rhetoricResultText;
    // Wire the popup's Continue button in the Inspector to ContinueAfterSkillCheck()

    [Header("Sound Effects")]
    [Tooltip("Add an AudioSource component to this GameObject (or any GameObject) and assign it here.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip skillCheckSuccessSound;
    [SerializeField] private AudioClip skillCheckFailureSound;
    [Tooltip("Plays when a mission panel (with its confirmation popup) spawns.")]
    [SerializeField] private AudioClip popupAppearSound;

    [Header("Hooks for Other Scripts (PlayerStats, Activity Log, IconResetManager)")]
    public PlayerStatEvent OnPlayerStatChange;
    public LogEvent OnLogEntry;
    public AgentEvent OnAgentDied;
    public UnityEvent OnMissionComplete;
    public UnityEvent OnMissionFailed;
    public UnityEvent OnMissionEnded; // fires after EITHER success or failure

    // --- Runtime state ---
    private Mission pendingMission;
    private MissionButton pendingButton;

    private Mission currentMission;
    private MissionButton currentMissionButton; // remembers which button launched the active mission, for lock/unlock rewards
    private int currentTurnIndex;
    private GameObject activeMissionInstance;
    private MissionUIReferences activeMissionUIRefs;
    private bool isMissionActive = false;

    private MissionTurn pendingTurn;
    private bool waitingOnSkillCheckPopup = false;
    private bool pendingRollSuccess;
    private List<AgentStats> pendingRollAgents;

    // --- MISSION ACTIVATION (called by MissionButton) ---

    public void RequestActivateMission(MissionButton button)
    {
        if (isMissionActive)
        {
            Log("A mission is already in progress.");
            return;
        }

        if (!button.isAvailable)
        {
            Log("Mission not available yet.");
            return;
        }

        pendingButton = button;
        pendingMission = button.mission;
        SpawnMissionPanel(pendingMission);
    }

    // Spawns the mission prefab immediately, showing its name/description AND its
    // built-in confirmation popup at the same time. The mission doesn't actually
    // start (turns, turn counter, etc.) until ConfirmYes() is called.
    private void SpawnMissionPanel(Mission mission)
    {
        if (missionUIPrefab == null || missionSpawnPoint == null)
        {
            Debug.LogWarning("MissionManager: Mission UI Prefab or Spawn Point not assigned.");
            return;
        }

        activeMissionInstance = Instantiate(missionUIPrefab, missionSpawnPoint);
        activeMissionUIRefs = activeMissionInstance.GetComponent<MissionUIReferences>();

        if (activeMissionUIRefs == null)
        {
            Debug.LogWarning("MissionManager: Mission UI Prefab is missing a MissionUIReferences component.");
            return;
        }

        if (activeMissionUIRefs.missionNameText != null) activeMissionUIRefs.missionNameText.text = mission.missionName;
        if (activeMissionUIRefs.missionDescriptionText != null) activeMissionUIRefs.missionDescriptionText.text = mission.missionDescription;

        if (activeMissionUIRefs.confirmationText != null) activeMissionUIRefs.confirmationText.text = $"Activate mission: {mission.missionName}?";
        if (activeMissionUIRefs.confirmationPopup != null) activeMissionUIRefs.confirmationPopup.SetActive(true);

        // Wire this instance's own Yes/No buttons at runtime, since a freshly
        // spawned prefab's buttons can't be pre-wired to a specific instance in the Inspector.
        if (activeMissionUIRefs.confirmYesButton != null)
        {
            activeMissionUIRefs.confirmYesButton.onClick.RemoveAllListeners();
            activeMissionUIRefs.confirmYesButton.onClick.AddListener(ConfirmYes);
        }
        if (activeMissionUIRefs.confirmNoButton != null)
        {
            activeMissionUIRefs.confirmNoButton.onClick.RemoveAllListeners();
            activeMissionUIRefs.confirmNoButton.onClick.AddListener(ConfirmNo);
        }

        PlaySound(popupAppearSound);
    }

    // Hook — or in this new setup, wired automatically in code — to the confirmation popup's "Yes" button
    public void ConfirmYes()
    {
        List<AgentStats> assignedAgents = GetAvailableAgents();

        if (assignedAgents.Count == 0)
        {
            Log("At least one agent must be assigned before activating a mission.");
            CancelPendingMission();
            return;
        }

        if (pendingButton != null && assignedAgents.Count > pendingButton.maxAssignedAgents)
        {
            Log($"Too many agents assigned. This mission allows a maximum of {pendingButton.maxAssignedAgents}.");
            CancelPendingMission();
            return;
        }

        if (activeMissionUIRefs != null && activeMissionUIRefs.confirmationPopup != null)
        {
            activeMissionUIRefs.confirmationPopup.SetActive(false);
        }

        currentMission = pendingMission;
        currentMissionButton = pendingButton;
        currentTurnIndex = 0;
        isMissionActive = true;

        pendingMission = null;
        pendingButton = null;

        Log($"Mission started: {currentMission.missionName}");

        if (currentMission.turns.Count > 0)
        {
            DisplayTurnIntro();
            ResolveCurrentTurnAndAdvance(); // auto-play: no End Turn click needed to attempt this turn
        }
    }

    // Hook — or wired automatically in code — to the confirmation popup's "No" button
    public void ConfirmNo()
    {
        CancelPendingMission();
    }

    // Destroys the just-spawned (but never activated) mission panel and clears pending state.
    private void CancelPendingMission()
    {
        if (activeMissionInstance != null) Destroy(activeMissionInstance);
        activeMissionInstance = null;
        activeMissionUIRefs = null;
        pendingMission = null;
        pendingButton = null;
    }

    private void DisplayTurnIntro()
    {
        if (!isMissionActive || currentTurnIndex >= currentMission.turns.Count) return;

        MissionTurn turn = currentMission.turns[currentTurnIndex];
        SetEventText(turn.turnDescription);
    }

    // --- TURN FLOW ---

    // Hook this to your End Turn button
    public void ResolveCurrentTurnAndAdvance()
    {
        if (!isMissionActive || waitingOnSkillCheckPopup) return;

        MissionTurn turn = currentMission.turns[currentTurnIndex];
        pendingTurn = turn;

        if (!turn.requiresCheck)
        {
            FinishTurnResolution(true, new List<AgentStats>(), skipOutcomes: true);
            return;
        }

        List<AgentStats> agents = GetAvailableAgents();

        if (agents.Count == 0)
        {
            Log("No available agents — cannot attempt this turn.");
            pendingRollAgents = agents;
            FinishTurnResolution(false, agents, skipOutcomes: false);
            return;
        }

        ResolveThreeSkillCheck(turn, agents);
    }

    // Rolls Conflict, Guile, and Rhetoric separately. The turn succeeds if at least 2 of 3 pass.
    private void ResolveThreeSkillCheck(MissionTurn turn, List<AgentStats> agents)
    {
        (bool conflictSuccess, string conflictBreakdown) = RollSkill(agents, "Conflict", turn.conflictTarget);
        (bool guileSuccess, string guileBreakdown) = RollSkill(agents, "Guile", turn.guileTarget);
        (bool rhetoricSuccess, string rhetoricBreakdown) = RollSkill(agents, "Rhetoric", turn.rhetoricTarget);

        int successCount = (conflictSuccess ? 1 : 0) + (guileSuccess ? 1 : 0) + (rhetoricSuccess ? 1 : 0);
        bool overallSuccess = successCount >= 2;

        Log($"Skill checks — Conflict: {(conflictSuccess ? "Success" : "Failure")}, Guile: {(guileSuccess ? "Success" : "Failure")}, Rhetoric: {(rhetoricSuccess ? "Success" : "Failure")} ({successCount}/3 passed). Turn result: {(overallSuccess ? "Success" : "Failure")}.");

        pendingRollAgents = agents;
        pendingRollSuccess = overallSuccess;

        ShowSkillCheckPopup(overallSuccess, conflictBreakdown, guileBreakdown, rhetoricBreakdown);
    }

    // Rolls one skill: modifier = assigned agents' combined stat - target value. Roll = d20 + modifier.
    private (bool success, string breakdown) RollSkill(List<AgentStats> agents, string statName, int targetValue)
    {
        int combinedStat = agents.Sum(agent => GetAgentStatValue(agent, statName));
        int modifier = combinedStat - targetValue;
        int roll = Random.Range(1, 21); // 1-20 inclusive
        int total = roll + modifier;
        bool success = total >= difficultyThreshold;

        string modifierText = modifier >= 0 ? $"+{modifier}" : modifier.ToString();
        string breakdown = $"{statName}: {roll} {modifierText} = {total} ({(success ? "Success" : "Failure")})";

        Log($"{statName} check — combined {statName} {combinedStat} vs target {targetValue} (modifier {modifierText}). Roll {roll} {modifierText} = {total} vs difficulty {difficultyThreshold} — {(success ? "Success" : "Failure")}.");

        return (success, breakdown);
    }

    private void ShowSkillCheckPopup(bool success, string conflictBreakdown, string guileBreakdown, string rhetoricBreakdown)
    {
        PlaySound(success ? skillCheckSuccessSound : skillCheckFailureSound);

        if (skillCheckPopup == null)
        {
            // No popup assigned — just proceed immediately
            FinishTurnResolution(success, pendingRollAgents, skipOutcomes: false);
            return;
        }

        waitingOnSkillCheckPopup = true;
        skillCheckPopup.SetActive(true);

        if (conflictResultText != null) conflictResultText.text = conflictBreakdown;
        if (guileResultText != null) guileResultText.text = guileBreakdown;
        if (rhetoricResultText != null) rhetoricResultText.text = rhetoricBreakdown;
    }

    // Hook this to the skill check popup's "Continue" button
    public void ContinueAfterSkillCheck()
    {
        if (skillCheckPopup != null) skillCheckPopup.SetActive(false);
        waitingOnSkillCheckPopup = false;
        FinishTurnResolution(pendingRollSuccess, pendingRollAgents, skipOutcomes: false);
    }

    // skipOutcomes is true only for no-check turns, which just display text and move on
    private void FinishTurnResolution(bool success, List<AgentStats> agents, bool skipOutcomes)
    {
        MissionTurn turn = pendingTurn;
        bool missionEndedThisTurn = false;

        if (!skipOutcomes)
        {
            SetEventText(success ? turn.successText : turn.failText);
            ApplyPlayerOutcomes(success ? turn.successOutcomes : turn.failureOutcomes);
            ApplyAgentOutcomes(agents, success ? turn.successAgentOutcomes : turn.failureAgentOutcomes);

            if (!success && turn.failureKillsAgent && agents.Count > 0)
            {
                AgentStats victim = agents[Random.Range(0, agents.Count)];
                KillAgent(victim);
            }

            if (!success && turn.endsMissionOnFailure)
            {
                missionEndedThisTurn = true;
            }
        }

        if (turnCounter != null) turnCounter.EndTurn();

        if (missionEndedThisTurn)
        {
            FailMission();
            return;
        }

        currentTurnIndex++;

        if (currentTurnIndex >= currentMission.turns.Count)
        {
            CompleteMission();
        }
        else
        {
            DisplayTurnIntro();
            ResolveCurrentTurnAndAdvance(); // auto-play: chain straight into the next turn
        }
    }

    // --- AGENT POOLING (automatic, based on scene availability) ---

    private List<AgentStats> GetAvailableAgents()
    {
        return Object.FindObjectsByType<AgentStats>(FindObjectsSortMode.None)
            .Where(agent => agent.IsAvailable)
            .ToList();
    }

    // --- REFLECTION HELPERS (work with any int field on AgentStats) ---

    private int GetAgentStatValue(AgentStats agent, string statName)
    {
        FieldInfo field = typeof(AgentStats).GetField(statName, BindingFlags.Public | BindingFlags.Instance);

        if (field == null || field.FieldType != typeof(int))
        {
            Debug.LogWarning($"MissionManager: '{statName}' is not a valid public int field on AgentStats. Check spelling/capitalization.");
            return 0;
        }

        return (int)field.GetValue(agent);
    }

    private void ModifyAgentStatValue(AgentStats agent, string statName, int amount)
    {
        FieldInfo field = typeof(AgentStats).GetField(statName, BindingFlags.Public | BindingFlags.Instance);

        if (field == null || field.FieldType != typeof(int))
        {
            Debug.LogWarning($"MissionManager: '{statName}' is not a valid public int field on AgentStats. Check spelling/capitalization.");
            return;
        }

        int currentValue = (int)field.GetValue(agent);
        field.SetValue(agent, currentValue + amount);
    }

    // --- OUTCOMES ---

    private void ApplyPlayerOutcomes(List<StatOutcome> outcomes)
    {
        foreach (StatOutcome outcome in outcomes)
        {
            OnPlayerStatChange?.Invoke(outcome.statName, outcome.amount);
            Log($"Player {outcome.statName} {(outcome.amount >= 0 ? "+" : "")}{outcome.amount}");
        }
    }

    private void ApplyAgentOutcomes(List<AgentStats> agents, List<AgentStatOutcome> outcomes)
    {
        if (outcomes.Count == 0) return;

        foreach (AgentStats agent in agents)
        {
            foreach (AgentStatOutcome outcome in outcomes)
            {
                ModifyAgentStatValue(agent, outcome.statName, outcome.amount);
            }
            agent.UpdateAllText();
            Log($"{agent.AgentName}: " + string.Join(", ", outcomes.Select(o => $"{o.statName} {(o.amount >= 0 ? "+" : "")}{o.amount}")));
        }
    }

    private void KillAgent(AgentStats agent)
    {
        Log($"{agent.AgentName} was struck down!");
        OnAgentDied?.Invoke(agent); // fire BEFORE Die(), in case Die() destroys the object
        agent.Die();
    }

    // --- MISSION END ---

    private void CompleteMission()
    {
        Log($"Mission complete: {currentMission.missionName}");
        OnMissionComplete?.Invoke();
        OnMissionEnded?.Invoke();
        AgentStats.UnassignAllAgents();
        AgentStats.HealAllUnassignedInjuredAgents();
        LockCompletedMissionIfNeeded();
        UnlockRewardMissions();
        DespawnMission();
    }

    private void FailMission()
    {
        Log($"Mission failed: {currentMission.missionName}");
        OnMissionFailed?.Invoke();
        OnMissionEnded?.Invoke();
        AgentStats.HealAllUnassignedInjuredAgents(); // heal anyone already resting from a PREVIOUS mission first
        InjureAssignedAgentsOnFailure(); // then apply THIS mission's casualties
        AgentStats.UnassignAllAgents();
        DespawnMission();
    }

    // Every agent still assigned to the mission gets injured (or killed, if they were
    // already injured coming into this mission) when the mission ends in failure.
    private void InjureAssignedAgentsOnFailure()
    {
        List<AgentStats> assignedAgents = GetAvailableAgents();
        foreach (AgentStats agent in assignedAgents)
        {
            agent.Die();
        }
    }

    // Locks the button that launched this mission back to unavailable, if it's flagged as one-time.
    // Runs BEFORE UnlockRewardMissions(), so a mission can never accidentally re-unlock itself.
    private void LockCompletedMissionIfNeeded()
    {
        if (currentMissionButton != null && currentMissionButton.lockAfterCompletion)
        {
            currentMissionButton.isAvailable = false;
        }
    }

    // Unlocks any mission buttons listed on the button that launched the just-completed mission.
    // Only called on SUCCESS (from CompleteMission), not on failure.
    private void UnlockRewardMissions()
    {
        if (currentMissionButton == null) return;

        foreach (MissionButton button in currentMissionButton.missionsToUnlock)
        {
            if (button != null && !button.isAvailable)
            {
                button.isAvailable = true;
                Log($"New mission unlocked: {button.mission.missionName}");
            }
        }
    }

    // Destroys the active mission's spawned UI and resets state. Called immediately
    // after CompleteMission()/FailMission() — no separate results screen or button click needed,
    // since the Skill Check Popup already showed the full breakdown for the deciding turn.
    private void DespawnMission()
    {
        if (activeMissionInstance != null) Destroy(activeMissionInstance);
        activeMissionInstance = null;
        activeMissionUIRefs = null;
        currentMission = null;
        currentMissionButton = null;
        isMissionActive = false;
    }

    // --- HELPERS ---

    private void SetEventText(string text)
    {
        if (activeMissionUIRefs != null && activeMissionUIRefs.eventText != null)
        {
            activeMissionUIRefs.eventText.text = text;
        }
        Log(text);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Public so MissionButton (and anything else) can route messages into the same log.
    public void Log(string message)
    {
        OnLogEntry?.Invoke(message);
        Debug.Log(message);
    }
}