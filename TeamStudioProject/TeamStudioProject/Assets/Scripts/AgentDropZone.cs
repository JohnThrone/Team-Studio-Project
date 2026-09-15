using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class AgentDropZone : MonoBehaviour, IDropHandler
{
    [Header("Capacity")]
    [Tooltip("Maximum number of agents allowed in this drop zone at once. Dropping a new agent while already at this limit resets everyone currently here first.")]
    [SerializeField] private int maxAgents = 3;

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        AgentDragHandler dragHandler = eventData.pointerDrag.GetComponent<AgentDragHandler>();
        if (dragHandler == null) return; // ignore drops from a parked icon being moved — DragIconController handles those

        AgentStats agent = dragHandler.GetAgentStats();
        if (agent == null) return;

        List<AgentStats> currentAgents = GetAgentsInZone();

        if (currentAgents.Count >= maxAgents)
        {
            ResetAllAgentsInZone(currentAgents);
        }

        agent.IsAvailable = true;
        agent.UpdateAllText();
        Debug.Log($"{agent.AgentName} is now available.");
    }

    // "In this zone" is read from AgentStats.IsAvailable, since that's the flag
    // the drag/drop system already uses everywhere else to mean "assigned."
    private List<AgentStats> GetAgentsInZone()
    {
        return Object.FindObjectsByType<AgentStats>(FindObjectsSortMode.None)
            .Where(agent => agent.IsAvailable)
            .ToList();
    }

    // Un-assigns every agent passed in, and snaps their icons back to their home position.
    private void ResetAllAgentsInZone(List<AgentStats> agents)
    {
        DragIconController[] allIcons = Object.FindObjectsByType<DragIconController>(FindObjectsSortMode.None);

        foreach (AgentStats agent in agents)
        {
            agent.IsAvailable = false;
            agent.UpdateAllText();
            Debug.Log($"{agent.AgentName} was removed from the drop zone (limit reached).");

            DragIconController icon = allIcons.FirstOrDefault(i => i.GetAgentStats() == agent);
            if (icon != null)
            {
                icon.ResetToOriginal();
            }
        }
    }
}