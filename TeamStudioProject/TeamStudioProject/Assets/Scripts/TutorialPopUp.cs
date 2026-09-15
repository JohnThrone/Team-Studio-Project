using UnityEngine;

public class TutorialPopup : MonoBehaviour
{
    [Header("Tutorial Panel")]
    [SerializeField] private GameObject tutorialPanel;

    private void Start()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
        }
    }

    // Hook this to the "Okay, ready to play" button's OnClick()
    public void CloseTutorial()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
    }
}