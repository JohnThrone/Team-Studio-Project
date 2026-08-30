using UnityEngine;
using TMPro;

public class TurnCounter : MonoBehaviour
{
    [Header("Turn Settings")]
    public int Turn = 1;

    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI turnText;

    private void Start()
    {
        UpdateTurnText();
    }

    // Hook this up to the End Turn button's OnClick() event in the Inspector
    public void EndTurn()
    {
        Turn++;
        UpdateTurnText();
    }

    private void UpdateTurnText()
    {
        if (turnText != null)
        {
            turnText.text = "Turn: " + Turn;
        }
        else
        {
            Debug.LogWarning("TurnCounter: turnText reference not set in Inspector.");
        }
    }
}