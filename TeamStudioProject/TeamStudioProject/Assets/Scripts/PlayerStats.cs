using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class PlayerStats : MonoBehaviour
{
    [Header("Player Stats")]
    public int Secrecy;
    [SerializeField] private TextMeshProUGUI secrecyText;
    [SerializeField] private Slider secrecySlider; // set this Slider's Min/Max in the Inspector to match your intended stat range

    public int Legitimacy;
    [SerializeField] private TextMeshProUGUI legitimacyText;
    [SerializeField] private Slider legitimacySlider;

    [Header("Victory / Game Over Thresholds")]
    [Tooltip("Legitimacy reaching this value or higher triggers Victory.")]
    public int victoryLegitimacyThreshold = 100;
    [Tooltip("Legitimacy reaching this value or lower triggers Game Over.")]
    public int gameOverLegitimacyThreshold = 0;
    [Tooltip("Secrecy reaching this value or lower triggers Game Over.")]
    public int gameOverSecrecyThreshold = 0;

    [Header("End Screens")]
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject deathScreen;

    [Header("Sound Effects")]
    [Tooltip("Add an AudioSource component to this GameObject and assign it here.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip legitimacyIncreaseSound;
    [SerializeField] private AudioClip legitimacyDecreaseSound;
    [SerializeField] private AudioClip secrecyIncreaseSound;
    [SerializeField] private AudioClip secrecyDecreaseSound;

    private bool gameEnded = false;

    private void Start()
    {
        UpdateAllText();
    }

    private void OnEnable()
    {
        AgentStats.OnAnyAgentPermanentlyDied += HandleAgentPermanentlyDied;
    }

    private void OnDisable()
    {
        AgentStats.OnAnyAgentPermanentlyDied -= HandleAgentPermanentlyDied;
    }

    public void UpdateAllText()
    {
        if (secrecyText != null) secrecyText.text = Secrecy.ToString();
        if (legitimacyText != null) legitimacyText.text = Legitimacy.ToString();
        if (secrecySlider != null) secrecySlider.value = Secrecy;
        if (legitimacySlider != null) legitimacySlider.value = Legitimacy;
    }

    // Hook this to MissionManager's On Player Stat Change event (Dynamic string, int)
    public void ApplyStatChange(string statName, int amount)
    {
        if (gameEnded) return;

        if (statName == "Secrecy")
        {
            Secrecy += amount;
            PlaySound(amount >= 0 ? secrecyIncreaseSound : secrecyDecreaseSound);
        }
        else if (statName == "Legitimacy")
        {
            Legitimacy += amount;
            PlaySound(amount >= 0 ? legitimacyIncreaseSound : legitimacyDecreaseSound);
        }
        else
        {
            Debug.LogWarning($"PlayerStats: '{statName}' does not match a known stat.");
            return;
        }

        UpdateAllText();
        CheckGameEndConditions();
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Called whenever an agent is permanently killed (not just injured).
    // If that was the last agent in the scene, Legitimacy drops to zero.
    private void HandleAgentPermanentlyDied(AgentStats agent)
    {
        CheckAgentPopulation();
    }

    private void CheckAgentPopulation()
    {
        AgentStats[] remainingAgents = Object.FindObjectsByType<AgentStats>(FindObjectsSortMode.None);
        if (remainingAgents.Length == 0)
        {
            Legitimacy = 0;
            UpdateAllText();
            CheckGameEndConditions();
        }
    }

    private void CheckGameEndConditions()
    {
        if (gameEnded) return;

        if (Legitimacy >= victoryLegitimacyThreshold)
        {
            TriggerVictory();
        }
        else if (Legitimacy <= gameOverLegitimacyThreshold || Secrecy <= gameOverSecrecyThreshold)
        {
            TriggerGameOver();
        }
    }

    private void TriggerVictory()
    {
        gameEnded = true;
        Time.timeScale = 0f;
        if (victoryScreen != null) victoryScreen.SetActive(true);
    }

    private void TriggerGameOver()
    {
        gameEnded = true;
        Time.timeScale = 0f;
        if (deathScreen != null) deathScreen.SetActive(true);
    }

    // Hook this to the Restart button on BOTH the victory and death screens
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}