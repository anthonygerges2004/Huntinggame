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

    [Header("Night Survival")]
    public bool enableNightDecay = true;
    public float nightDecayRate = 2f; // health lost per second when night without fire
    public float nightDecayCheckInterval = 1f; // check every second if conditions met
    private float nightDecayTimer;

    private GameObject deathScreenUI;
    private bool deathScreenCreated = false;

    void Awake()
    {
        Instance = this;
        health = maxHealth;
        isDead = false;
        nightDecayTimer = nightDecayCheckInterval;
    }

    void Update()
    {
        if (!isDead && enableNightDecay)
        {
            UpdateNightDecay();
        }
    }

    void UpdateNightDecay()
    {
        nightDecayTimer -= Time.deltaTime;
        if (nightDecayTimer > 0f)
            return;

        nightDecayTimer = nightDecayCheckInterval;

        // Check if it's night and player is not safe by fire
        if (DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight)
        {
            if (!Campfire.PlayerIsSafe())
            {
                // Apply decay damage
                float damageThisCheck = nightDecayRate * nightDecayCheckInterval;
                TakeDamage(damageThisCheck);
                Debug.Log($"Cold/starvation damage: {damageThisCheck}. Health: {health}/{maxHealth}");
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead)
            return;

        health -= amount;
        if (health < 0f)
            health = 0f;

        Debug.Log($"Player took {amount} damage. Health: {health}/{maxHealth}");

        if (health <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead)
            return;

        health += amount;
        if (health > maxHealth)
            health = maxHealth;

        Debug.Log($"Player healed {amount}. Health: {health}/{maxHealth}");
    }

    void Die()
    {
        isDead = true;
        Debug.Log("You died. Light a fire at night and keep your health up.");
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

        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();

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
        deathText.text = "YOU DIED\n\nLight a fire at night and keep your health up!";
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

        deathScreenUI = canvasObj;
    }
}
