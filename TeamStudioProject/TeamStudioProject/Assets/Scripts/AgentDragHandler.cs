using UnityEngine;
using UnityEngine.EventSystems;

public class AgentDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private AgentStats agentStats;

    [Header("Drag Icon (this agent's own icon)")]
    [SerializeField] private DragIconController dragIconController;
    [SerializeField] private Canvas parentCanvas;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (dragIconController != null)
        {
            dragIconController.Show();
            UpdateDragIconPosition(eventData);
        }
        Cursor.visible = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateDragIconPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Cursor.visible = true;

        bool droppedOnValidZone = eventData.pointerEnter != null
            && eventData.pointerEnter.GetComponentInParent<AgentDropZone>() != null;

        if (dragIconController != null)
        {
            dragIconController.FinishIncomingDrag(droppedOnValidZone);
        }
    }

    private void UpdateDragIconPosition(PointerEventData eventData)
    {
        if (dragIconController == null || parentCanvas == null) return;
        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, cam, out Vector2 localPoint))
        {
            dragIconController.SetPosition(localPoint);
        }
    }

    public AgentStats GetAgentStats() => agentStats;
}