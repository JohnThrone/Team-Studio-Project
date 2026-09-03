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

// A stat change applied to the AGENT(S) who took part in the roll.
// statName must exactly match a public int field name on AgentStats
// (e.g. "Conflict", "Rhetoric", "Guile", "SkillPoints", or any new field you add later).
[System.Serializable]
public class AgentStatOutcome
{
    public string statName;
    public int amount;
}

// One beat within a mission
[System.Serializable]
public class MissionTurn
{
    [TextArea] public string turnDescription;

    [Header("Roll (leave 'Requires Roll' off for a no-check turn)")]
    public bool requiresRoll;

    [Tooltip("Must exactly match a public int field name on AgentStats (e.g. Conflict, Rhetoric, Guile).")]
    public string statToTest;
    public int difficultyClass = 10;

    [Header("Result Text")]
    [TextArea] public string successText;
    [TextArea] public string failText;

    [Header("Player Stat Effects")]
    public List<StatOutcome> successOutcomes = new List<StatOutcome>();
    public List<StatOutcome> failureOutcomes = new List<StatOutcome>();

    [Header("Agent Stat Effects (applied to every agent who took part in the roll)")]
    public List<AgentStatOutcome> successAgentOutcomes = new List<AgentStatOutcome>();
    public List<AgentStatOutcome> failureAgentOutcomes = new List<AgentStatOutcome>();

    [Header("Other Effects")]
    public bool failureKillsAgent;
    public bool endsMissionOnFailure;
}

// A full mission: name, description, and its own ordered sequence of turns.
// Each MissionButton holds one of these — missions are no longer a fixed
// auto-advancing list, they're spawned individually on demand.
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
    [Tooltip("Prefab with a MissionUIReferences component on its root, showing the active mission's name/description/event text.")]
    [SerializeField] private GameObject missionUIPrefab;
    [SerializeField] private Transform missionSpawnPoint;

    [Header("Turn Tracking")]
    [SerializeField] private TurnCounter turnCounter; // global counter, never reset

    [Header("Confirmation Popup (shown before activating a mission)")]
    [SerializeField] private GameObject confirmationPopup;
    [SerializeField] private TextMeshProUGUI confirmationText;
    // Wire the popup's Yes/No buttons in the Inspector to ConfirmYes() / ConfirmNo()

    [Header("Skill Check Popup (shown after every roll)")]
    [SerializeField] private GameObject skillCheckPopup;
    [SerializeField] private TextMeshProUGUI skillCheckStatText;
    [SerializeField] private TextMeshProUGUI skillCheckRollText;
    [SerializeField] private TextMeshProUGUI skillCheckResultText;
    // Wire the popup's Continue button in the Inspector to ContinueAfterSkillCheck()

    [Header("Results Screen (shown when a mission ends)")]
    [SerializeField] private GameObject resultsScreen;
    [SerializeField] private TextMeshProUGUI resultsText;
    // Wire the screen's OK button in the Inspector to CloseResultsScreen()

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

        if (confirmationPopup != null)
        {
            if (confirmationText != null) confirmationText.text = $"Activate mission: {pendingMission.missionName}?";
            confirmationPopup.SetActive(true);
        }
        else
        {
            ConfirmYes();
        }
    }

    // Hook to the confirmation popup's "Yes" button
    public void ConfirmYes()
    {
        if (confirmationPopup != null) confirmationPopup.SetActive(false);

        List<AgentStats> assignedAgents = GetAvailableAgents();
        if (assignedAgents.Count == 0)
        {
            Log("At least one agent must be assigned before activating a mission.");
            pendingMission = null;
            pendingButton = null;
            return;
        }

        currentMissionButton = pendingButton; // remember who launched this, for locking/unlocking rewards later
        SpawnMission(pendingMission);
        pendingMission = null;
        pendingButton = null;
    }

    // Hook to the confirmation popup's "No" button
    public void ConfirmNo()
    {
        if (confirmationPopup != null) confirmationPopup.SetActive(false);
        pendingMission = null;
        pendingButton = null;
    }

    private void SpawnMission(Mission mission)
    {
        if (missionUIPrefab == null || missionSpawnPoint == null)
        {
            Debug.LogWarning("MissionManager: Mission UI Prefab or Spawn Point not assigned.");
            return;
        }

        activeMissionInstance = Instantiate(missionUIPrefab, missionSpawnPoint);
        activeMissionUIRefs = activeMissionInstance.GetComponent<MissionUIReferences>();

        currentMission = mission;
        currentTurnIndex = 0;
        isMissionActive = true;

        if (activeMissionUIRefs != null)
        {
            if (activeMissionUIRefs.missionNameText != null) activeMissionUIRefs.missionNameText.text = mission.missionName;
            if (activeMissionUIRefs.missionDescriptionText != null) activeMissionUIRefs.missionDescriptionText.text = mission.missionDescription;
        }

        Log($"Mission started: {mission.missionName}");

        if (mission.turns.Count > 0)
        {
            DisplayTurnIntro();
        }
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

        if (!turn.requiresRoll)
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

        int combinedStat = agents.Sum(agent => GetAgentStatValue(agent, turn.statToTest));
        int roll = Random.Range(1, 21); // 1-20 inclusive
        int total = roll + combinedStat;
        bool success = total >= turn.difficultyClass;

        string names = string.Join(", ", agents.Select(a => a.AgentName));
        Log($"Rolled {roll} + combined {turn.statToTest} ({combinedStat} from {names}) = {total} vs DC {turn.difficultyClass} — {(success ? "Success" : "Failure")}.");

        pendingRollAgents = agents;
        pendingRollSuccess = success;
        ShowSkillCheckPopup(turn, roll, combinedStat, total, success);
    }

    private void ShowSkillCheckPopup(MissionTurn turn, int roll, int combinedStat, int total, bool success)
    {
        if (skillCheckPopup == null)
        {
            // No popup assigned — just proceed immediately
            FinishTurnResolution(success, pendingRollAgents, skipOutcomes: false);
            return;
        }

        waitingOnSkillCheckPopup = true;
        skillCheckPopup.SetActive(true);

        if (skillCheckStatText != null) skillCheckStatText.text = $"{turn.statToTest} Check (DC {turn.difficultyClass})";
        if (skillCheckRollText != null) skillCheckRollText.text = $"Roll: {roll} + {combinedStat} = {total}";
        if (skillCheckResultText != null) skillCheckResultText.text = success ? "Success" : "Failure";
    }

    // Hook this to the skill check popup's "Continue" button
    public void ContinueAfterSkillCheck()
    {
        if (skillCheckPopup != null) skillCheckPopup.SetActive(false);
        waitingOnSkillCheckPopup = false;
        FinishTurnResolution(pendingRollSuccess, pendingRollAgents, skipOutcomes: false);
    }

    // skipOutcomes is true only for no-roll turns, which just display text and move on
    private void FinishTurnResolution(bool success, List<AgentStats> agents, bool skipOutcomes)
    {
        MissionTurn turn = pendingTurn;
        bool missionEndedThisTurn = false;

        if (!skipOutcomes)
        {
            if (success)
            {
                SetEventText(turn.successText);
                ApplyPlayerOutcomes(turn.successOutcomes);
                ApplyAgentOutcomes(agents, turn.successAgentOutcomes);
            }
            else
            {
                SetEventText(turn.failText);
                ApplyPlayerOutcomes(turn.failureOutcomes);
                ApplyAgentOutcomes(agents, turn.failureAgentOutcomes);

                if (turn.failureKillsAgent && agents.Count > 0)
                {
                    AgentStats victim = agents[Random.Range(0, agents.Count)];
                    KillAgent(victim);
                }

                if (turn.endsMissionOnFailure)
                {
                    missionEndedThisTurn = true;
                }
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
        ShowResultsScreen(true);
    }

    private void FailMission()
    {
        Log($"Mission failed: {currentMission.missionName}");
        OnMissionFailed?.Invoke();
        OnMissionEnded?.Invoke();
        AgentStats.UnassignAllAgents();
        AgentStats.HealAllUnassignedInjuredAgents();
        ShowResultsScreen(false);
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

    private void ShowResultsScreen(bool success)
    {
        if (resultsScreen == null)
        {
            CloseResultsScreen();
            return;
        }

        resultsScreen.SetActive(true);
        if (resultsText != null)
        {
            resultsText.text = success
                ? $"Mission Success: {currentMission.missionName}"
                : $"Mission Failed: {currentMission.missionName}";
        }
    }

    // Hook this to the results screen's "OK" button
    public void CloseResultsScreen()
    {
        if (resultsScreen != null) resultsScreen.SetActive(false);

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

    // Public so MissionButton (and anything else) can route messages into the same log.
    public void Log(string message)
    {
        OnLogEntry?.Invoke(message);
        Debug.Log(message);
    }
}