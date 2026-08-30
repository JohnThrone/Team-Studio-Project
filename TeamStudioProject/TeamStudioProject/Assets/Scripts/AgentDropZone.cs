using UnityEngine;
using UnityEngine.EventSystems;

public class AgentDropZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        AgentDragHandler dragHandler = eventData.pointerDrag.GetComponent<AgentDragHandler>();
        if (dragHandler == null) return; // ignore drops from the parked icon itself — DragIconController handles those

        AgentStats agent = dragHandler.GetAgentStats();
        if (agent == null) return;

        agent.IsAvailable = true;
        agent.UpdateAllText();
        Debug.Log($"{agent.AgentName} is now available.");
    }
}