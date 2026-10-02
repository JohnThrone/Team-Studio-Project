using UnityEngine;

public class CodexPanel : MonoBehaviour
{
    [Header("Codex Panel")]
    [SerializeField] private GameObject codexPanel;

    // Hook this to the "Historical Codex" button's OnClick()
    public void OpenCodex()
    {
        if (codexPanel != null)
        {
            codexPanel.SetActive(true);
        }
    }

    // Hook this to the panel's own "Close" button OnClick()
    public void CloseCodex()
    {
        if (codexPanel != null)
        {
            codexPanel.SetActive(false);
        }
    }
}