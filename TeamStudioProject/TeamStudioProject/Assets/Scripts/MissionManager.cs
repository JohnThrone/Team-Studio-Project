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
// (e.g. "Conflict", "Rhetoric", "Guile", or any new field you add later).
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

    [Tooltip("Must exactly match a public int field name on AgentStats (e.g. Conflict, Rhetoric, Guile). Add new fields to AgentStats and reference them here without touching this script.")]
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
    [Header("Missions (activate in list order, one after another)")]
    public List<Mission> missions = new List<Mission>();
    private int currentMissionIndex = 0;
    private int currentTurnIndex = 0;

    [Header("Mission Settings")]
    public bool haltProgressionOnFailure = false; // if true, stops the whole sequence when a mission fails

    [Header("Turn Tracking")]
    [SerializeField] private TurnCounter turnCounter; // global counter, never reset

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI missionNameText;
    [SerializeField] private TextMeshProUGUI missionDescriptionText;
    [SerializeField] private TextMeshProUGUI eventText;

    [Header("Hooks for Other Scripts (PlayerStats, Activity Log, DragIconController)")]
    public PlayerStatEvent OnPlayerStatChange;
    public LogEvent OnLogEntry;
    public AgentEvent OnAgentDied;
    public UnityEvent OnMissionComplete;
    public UnityEvent OnMissionFailed;
    public UnityEvent OnMissionEnded; // fires after EITHER success or failure
    public UnityEvent OnAllMissionsComplete;

    private bool sequenceEnded = false;

    private void Start()
    {
        if (missions.Count > 0)
        {
            LoadMission(currentMissionIndex);
        }
    }

    // --- MISSION FLOW ---

    private void LoadMission(int index)
    {
        Mission mission = missions[index];

        if (missionNameText != null) missionNameText.text = mission.missionName;
        if (missionDescriptionText != null) missionDescriptionText.text = mission.missionDescription;

        currentTurnIndex = 0;
        Log($"Mission started: {mission.missionName}");

        if (mission.turns.Count > 0)
        {
            DisplayTurnIntro();
        }
    }

    private void DisplayTurnIntro()
    {
        if (sequenceEnded) return;
        Mission mission = missions[currentMissionIndex];
        if (currentTurnIndex >= mission.turns.Count) return;

        MissionTurn turn = mission.turns[currentTurnIndex];
        if (eventText != null) eventText.text = turn.turnDescription;
        Log(turn.turnDescription);
    }

    // Hook this to your End Turn button
    public void ResolveCurrentTurnAndAdvance()
    {
        if (sequenceEnded) return;

        Mission mission = missions[currentMissionIndex];
        if (currentTurnIndex >= mission.turns.Count) return;

        MissionTurn turn = mission.turns[currentTurnIndex];
        bool missionEndedThisTurn = ResolveTurn(mission, turn);

        // Global turn count always advances and never resets, regardless of mission boundaries
        if (turnCounter != null) turnCounter.EndTurn();

        if (missionEndedThisTurn) return; // CompleteMission/FailMission already advanced to next mission

        currentTurnIndex++;

        if (currentTurnIndex >= mission.turns.Count)
        {
            CompleteMission();
        }
        else
        {
            DisplayTurnIntro();
        }
    }

    // Returns true if the mission ended (success or failure) as a result of this turn
    private bool ResolveTurn(Mission mission, MissionTurn turn)
    {
        if (!turn.requiresRoll)
        {
            return false;
        }

        List<AgentStats> availableAgents = GetAvailableAgents();

        if (availableAgents.Count == 0)
        {
            Log("No available agents — cannot attempt this turn.");
            SetEventText(turn.failText);
            ApplyPlayerOutcomes(turn.failureOutcomes);

            if (turn.endsMissionOnFailure)
            {
                FailMission();
                return true;
            }
            return false;
        }

        bool success = RollAgainstCombinedStat(availableAgents, turn.statToTest, turn.difficultyClass);

        if (success)
        {
            SetEventText(turn.successText);
            ApplyPlayerOutcomes(turn.successOutcomes);
            ApplyAgentOutcomes(availableAgents, turn.successAgentOutcomes);
        }
        else
        {
            SetEventText(turn.failText);
            ApplyPlayerOutcomes(turn.failureOutcomes);
            ApplyAgentOutcomes(availableAgents, turn.failureAgentOutcomes);

            if (turn.failureKillsAgent)
            {
                // Stats are pooled, not tied to one "assigned" agent, so this
                // picks one random available agent to die. Change this pick
                // logic if you want a different rule (e.g. lowest stat, or all of them).
                AgentStats victim = availableAgents[Random.Range(0, availableAgents.Count)];
                KillAgent(victim);
            }

            if (turn.endsMissionOnFailure)
            {
                FailMission();
                return true;
            }
        }

        return false;
    }

    // --- AGENT POOLING (automatic, based on scene availability) ---

    private List<AgentStats> GetAvailableAgents()
    {
        return Object.FindObjectsByType<AgentStats>(FindObjectsSortMode.None)
            .Where(agent => agent.IsAvailable)
            .ToList();
    }

    // --- ROLL LOGIC (reflection-based, works with any int field on AgentStats) ---

    private bool RollAgainstCombinedStat(List<AgentStats> agents, string statName, int dc)
    {
        int combinedStat = agents.Sum(agent => GetAgentStatValue(agent, statName));
        int roll = Random.Range(1, 21); // 1-20 inclusive
        int total = roll + combinedStat;

        bool success = total >= dc;
        string names = string.Join(", ", agents.Select(a => a.AgentName));
        Log($"Rolled {roll} + combined {statName} ({combinedStat} from {names}) = {total} vs DC {dc} — {(success ? "Success" : "Failure")}.");
        return success;
    }

    // Reads any public int field on AgentStats by name, e.g. "Conflict", "Rhetoric", "Guile",
    // or any new stat field you add later — no changes to this script required.
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

    // Writes any public int field on AgentStats by name, adding 'amount' to its current value.
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

    // Applies each outcome to every agent who took part in the roll, then refreshes their UI text.
    private void ApplyAgentOutcomes(List<AgentStats> agents, List<AgentStatOutcome> outcomes)
    {
        if (outcomes.Count == 0) return;

        foreach (AgentStats agent in agents)
        {
            foreach (AgentStatOutcome outcome in outcomes)
            {
                ModifyAgentStatValue(agent, outcome.statName, outcome.amount);
            }
            agent.UpdateAllText(); // refreshes the agent's own TMP displays immediately
            Log($"{agent.AgentName}: " + string.Join(", ", outcomes.Select(o => $"{o.statName} {(o.amount >= 0 ? "+" : "")}{o.amount}")));
        }
    }

    private void KillAgent(AgentStats agent)
    {
        Log($"{agent.AgentName} has died.");
        agent.IsAvailable = false;
        agent.UpdateAllText();
        OnAgentDied?.Invoke(agent);
    }

    private void CompleteMission()
    {
        Log($"Mission complete: {missions[currentMissionIndex].missionName}");
        OnMissionComplete?.Invoke();
        OnMissionEnded?.Invoke();
        AdvanceToNextMission();
    }

    private void FailMission()
    {
        Log($"Mission failed: {missions[currentMissionIndex].missionName}");
        OnMissionFailed?.Invoke();
        OnMissionEnded?.Invoke();

        if (haltProgressionOnFailure)
        {
            sequenceEnded = true;
            return;
        }

        AdvanceToNextMission();
    }

    private void AdvanceToNextMission()
    {
        currentMissionIndex++;

        if (currentMissionIndex >= missions.Count)
        {
            sequenceEnded = true;
            Log("All missions complete.");
            OnAllMissionsComplete?.Invoke();
            return;
        }

        LoadMission(currentMissionIndex);
    }

    // --- HELPERS ---

    private void SetEventText(string text)
    {
        if (eventText != null) eventText.text = text;
        Log(text);
    }

    private void Log(string message)
    {
        OnLogEntry?.Invoke(message);
        Debug.Log(message); // remove once your Activity Log script is in place
    }
}