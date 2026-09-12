using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class DragIconController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private AgentStats agentStats;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private Graphic graphic; // this icon's own Image; toggled so it's only grabbable once parked

    [Header("Sound Effects")]
    [Tooltip("Add an AudioSource component to this GameObject (or any GameObject) and assign it here.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip dropOnZoneSound;
    [SerializeField] private AudioClip dropOffZoneSound;

    private RectTransform rectTransform;
    private Vector2 originalAnchoredPosition;
    private bool isParked = false; // true once successfully dropped and sitting still at the zone

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalAnchoredPosition = rectTransform.anchoredPosition;
        if (graphic == null) graphic = GetComponent<Graphic>();
        gameObject.SetActive(false);
        SetInteractable(false);
    }

    // --- Called by AgentDragHandler during the initial drag IN ---

    public void Show()
    {
        gameObject.SetActive(true);
        SetInteractable(false); // non-blocking while following the mouse to the drop zone
    }

    // Public so AgentDragHandler (which starts the very first pickup, before this
    // icon has even appeared) can trigger the same pickup sound.
    public void PlayPickupSound()
    {
        PlaySound(pickupSound);
    }

    public void SetPosition(Vector2 anchoredPosition)
    {
        rectTransform.anchoredPosition = anchoredPosition;
    }

    public void FinishIncomingDrag(bool droppedOnValidZone)
    {
        if (droppedOnValidZone)
        {
            Park();
        }
        else
        {
            CancelDrag();
        }
    }

    private void Park()
    {
        isParked = true;
        SetInteractable(true); // now grabbable directly, to drag back out
        PlaySound(dropOnZoneSound);
    }

    public void CancelDrag()
    {
        isParked = false;
        rectTransform.anchoredPosition = originalAnchoredPosition;
        gameObject.SetActive(false);
        SetInteractable(false);
        PlaySound(dropOffZoneSound);
    }

    // Called on mission end — forces this icon back to hidden/start regardless of state.
    // No sound here: this is a system-triggered reset, not a player drag action.
    public void ResetToOriginal()
    {
        isParked = false;
        rectTransform.anchoredPosition = originalAnchoredPosition;
        gameObject.SetActive(false);
        SetInteractable(false);
        Cursor.visible = true;
    }

    private void SetInteractable(bool interactable)
    {
        if (graphic != null) graphic.raycastTarget = interactable;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // --- Dragging the icon back OUT once it's parked ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isParked) return;
        SetInteractable(false); // don't block the drop zone raycast underneath while moving
        Cursor.visible = false;
        PlaySound(pickupSound);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isParked) return;
        UpdatePosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isParked) return;

        Cursor.visible = true;

        bool droppedOnValidZone = eventData.pointerEnter != null
            && eventData.pointerEnter.GetComponentInParent<AgentDropZone>() != null;

        if (droppedOnValidZone)
        {
            // Dropped back on the same zone — stays assigned, no change
            Park();
        }
        else
        {
            // Dragged out of the zone — un-assign the agent
            if (agentStats != null)
            {
                agentStats.IsAvailable = false;
                agentStats.UpdateAllText();
                Debug.Log($"{agentStats.AgentName} is no longer available.");
            }
            CancelDrag();
        }
    }

    private void UpdatePosition(PointerEventData eventData)
    {
        if (parentCanvas == null) return;
        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, cam, out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint;
        }
    }
}