using HuntingGame.Survival;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance;
    public float maxHealth = 100f;
    public float health = 100f;
    public bool isDead { get; private set; }

    // Time of the last damage; natural healing waits a while after being hurt.
    public float LastDamageTime { get; private set; } = -999f;
    public string CauseOfDeath { get; private set; } = "";

    private bool deathScreenCreated = false;
    private string lastCause = "Injuries";

    void Awake()
    {
        Time.timeScale = 1f;
        Instance = this;
        health = maxHealth;
        isDead = false;
        if (GetComponent<SurvivalStats>() == null)
            gameObject.AddComponent<SurvivalStats>();
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, "Injuries");
    }

    public void TakeDamage(float amount, string cause)
    {
        if (isDead || amount <= 0f)
            return;

        health -= amount;
        if (health < 0f)
            health = 0f;
        LastDamageTime = Time.time;
        lastCause = string.IsNullOrEmpty(cause) ? "Injuries" : cause;

        if (health <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        health += amount;
        if (health > maxHealth)
            health = maxHealth;
    }

    void Die()
    {
        isDead = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        CauseOfDeath = lastCause;
        Debug.Log("You died: " + CauseOfDeath);
        CreateDeathScreen();
    }

    void CreateDeathScreen()
    {
        if (deathScreenCreated)
            return;

        deathScreenCreated = true;
        Time.timeScale = 0f;

        // Create canvas
        GameObject canvasObj = new GameObject("DeathScreenCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        canvasObj.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        // Create background panel
        GameObject panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Create death text
        GameObject textObj = new GameObject("DeathText");
        textObj.transform.SetParent(panelObj.transform, false);
        TextMeshProUGUI deathText = textObj.AddComponent<TextMeshProUGUI>();
        deathText.text = "YOU DIED\n<size=45%><color=#D8D2C4>" + CauseOfDeath + "</color></size>";
        deathText.alignment = TextAlignmentOptions.Center;
        deathText.fontSize = 60;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchoredPosition = new Vector2(0, 50);
        textRect.sizeDelta = new Vector2(800, 300);

        // Create restart button
        GameObject buttonObj = new GameObject("RestartButton");
        buttonObj.transform.SetParent(panelObj.transform, false);
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.6f, 0.2f, 1f);

        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(0, -80);
        buttonRect.sizeDelta = new Vector2(200, 60);

        // Button text
        GameObject buttonTextObj = new GameObject("Text");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = "RESTART";
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.fontSize = 40;

        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        // Button click handler
        button.onClick.AddListener(() => {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        });
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        if (Instance == this)
            Instance = null;
    }
}
