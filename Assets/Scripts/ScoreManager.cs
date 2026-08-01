using UnityEngine;
using TMPro;
using System.Collections;
using Unity.VisualScripting.Antlr3.Runtime;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Game State")]
    public int currentScore = 0;
    public int currentCombo = 0;
    public int multiplier = 1;
    public float currentHealth = 100f;
    public float maxHealth = 100f;

    [Header("Balancing")]
    public int scorePerPerfect = 100;
    public int scorePerGood = 70;
    public int scorePerBad = 30;
    public float healthPenalty = 10f;
    public int scorePerHoldTick = 10;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip hitSound;
    public AudioClip missSound;

    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI addScoreText;
    public TextMeshProUGUI comboText;
    public TextMeshProUGUI feedbackText;
    public UnityEngine.UI.Slider healthBar;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        UpdateUI();
        feedbackText.text = "";
        addScoreText.text = "";
        currentHealth = maxHealth;
    }
    public void Hit(string rank)
    {
        int baseScore = 0;
        int addScore = 0;

        switch (rank)
        {
            case "Perfect":
                baseScore = scorePerPerfect;
                feedbackText.text = "<color=#ffd700>PERFECT</color>";
                feedbackText.fontSize = 70;

                break;
            case "Good":
                baseScore = scorePerGood;
                feedbackText.text = "<color=#6495ed>GOOD</color>";
                feedbackText.fontSize = 65;
                break;
        }

        currentCombo++;
        currentHealth++;

        if (currentCombo > 100) multiplier = 4;
        if (currentCombo > 20) multiplier = 2;
        else multiplier = 1;

        addScore = baseScore * multiplier;
        currentScore += addScore;
        addScoreText.text = "+" + addScore;

        if (sfxSource != null && hitSound != null)
            sfxSource.PlayOneShot(hitSound);

        StopAllCoroutines();
        StartCoroutine(PulseScoreUI(scoreText.transform));
        StartCoroutine(PulseComboUI(comboText.transform));
        StartCoroutine(FadeFeedbackText(feedbackText));
        StartCoroutine(FadeAddScoreText(addScoreText));

        UpdateUI();
    }

    public void Bad()
    {
        int baseScore = 0;
        baseScore = scorePerBad;
        feedbackText.text = "<color=#ff6347>BAD</color>";
        feedbackText.fontSize = 60;
        currentScore += baseScore;
        addScoreText.text = "+" + baseScore;
        currentCombo = 0;
        multiplier = 1;
        currentHealth -= healthPenalty;

        if (sfxSource != null && missSound != null)
            sfxSource.PlayOneShot(missSound);

        UpdateUI();
    }

    public void HoldTick()
    {
        int tickScore = scorePerHoldTick * multiplier;
        currentScore += tickScore;
        addScoreText.text = "+" + tickScore;
        UpdateUI();
    }

    public void Miss()
    {
        currentCombo = 0;
        multiplier = 1;
        feedbackText.text = "<color=#ffffff>MISS</color>";
        feedbackText.fontSize = 50;
        currentHealth -= healthPenalty;

        if (sfxSource != null && missSound != null)
            sfxSource.PlayOneShot(missSound);

        UpdateUI();

        if (currentHealth <= 0f)
        {
            Debug.Log("GAME OVER");
        }
    }

    void UpdateUI()
    {
        scoreText.text = currentScore.ToString("000000");

        if (currentCombo >= 5)
            comboText.text = "" + currentCombo;
        else comboText.text = "";

        if (healthBar != null) healthBar.value = currentHealth / maxHealth;
    }

    IEnumerator PulseComboUI(Transform target)
    {
        target.localScale = Vector3.one * 1.5f;

        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * 2f;
            target.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, t);
            yield return null;
        }
        target.localScale = Vector3.one;
    }
    IEnumerator PulseScoreUI(Transform target)
    {
        target.localScale = Vector3.one * 1.5f;

        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * 5f;
            target.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, t);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    IEnumerator FadeFeedbackText(TextMeshProUGUI target)
    {
        target.alpha = 1;
        yield return new WaitForSeconds(0.1f);

        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * 5f;
            target.alpha = Mathf.Lerp(1, 0, t);
            yield return null;
        }
    }
    IEnumerator FadeAddScoreText(TextMeshProUGUI target)
    {
        target.alpha = 1;
        yield return new WaitForSeconds(0.1f);

        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * 1f;
            target.alpha = Mathf.Lerp(1, 0, t);
            yield return null;
        }
    }
}
