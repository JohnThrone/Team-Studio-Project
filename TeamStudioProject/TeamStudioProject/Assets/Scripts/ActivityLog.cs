using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActivityLog : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private TextMeshProUGUI logText; // lives inside the Scroll View's Content

    [Header("Settings")]
    [SerializeField] private int maxEntries = 200; // prevents the log growing forever in long sessions
    private int entryCount = 0;

    // Hook this to MissionManager's On Log Entry () event (Dynamic string)
    public void AddLogEntry(string message)
    {
        if (logText == null) return;

        if (entryCount >= maxEntries)
        {
            TrimOldestEntry();
        }

        logText.text += (string.IsNullOrEmpty(logText.text) ? "" : "\n") + message;
        entryCount++;

        ScrollToBottom();
    }

    private void TrimOldestEntry()
    {
        int firstNewline = logText.text.IndexOf('\n');
        if (firstNewline >= 0)
        {
            logText.text = logText.text.Substring(firstNewline + 1);
        }
    }

    private void ScrollToBottom()
    {
        // Wait a frame so the ContentSizeFitter/Layout has resized before scrolling,
        // otherwise the scroll position is calculated against the old (shorter) height.
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // Optional: call this if you want a "clear log" button somewhere
    public void ClearLog()
    {
        if (logText != null) logText.text = "";
        entryCount = 0;
    }
}