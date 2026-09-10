using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;
    public TMP_Text scoreText;

    int score;

    void Awake()
    {
        Instance = this;
        UpdateText();
    }

    public void AddScore(int amount)
    {
        score += amount;
        UpdateText();
    }

    void UpdateText()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score;
    }
}
