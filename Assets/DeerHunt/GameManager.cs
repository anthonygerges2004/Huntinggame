using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeerHunt
{
    /// <summary>
    /// Hunt clock, kill count, and score. Win by reaching the quota. Lose when time runs out.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] int quota = 3;
        [SerializeField] float timeLimit = 180f;
        [SerializeField] int killScore = 100;
        [SerializeField] int headshotBonus = 50;
        [SerializeField] int heartBonus = 30;
        [SerializeField] int longRangeBonus = 40;
        [SerializeField] float longRangeMeters = 40f;

        float timeLeft;
        int kills;
        int score;
        string lastRange = "";
        bool finished;
        bool won;
        GUIStyle title;
        GUIStyle body;

        public bool Playing => !finished;

        void Awake()
        {
            Instance = this;
            timeLeft = timeLimit;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (finished)
                return;

            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                Finish(false);
            }
        }

        public void RegisterKill(HitZone zone, float distance)
        {
            if (finished)
                return;

            kills++;
            int gained = killScore;
            if (zone != null && zone.Kind == HitZone.Zone.Head)
                gained += headshotBonus;
            if (zone != null && zone.Kind == HitZone.Zone.Chest)
                gained += heartBonus;
            if (distance >= longRangeMeters)
                gained += longRangeBonus;

            score += gained;
            lastRange = Mathf.RoundToInt(distance) + " m";
            if (kills >= quota)
                Finish(true);
        }

        void Finish(bool success)
        {
            finished = true;
            won = success;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnGUI()
        {
            EnsureStyles();
            if (!finished)
            {
                DrawHud();
                return;
            }

            DrawResults();
        }

        void DrawHud()
        {
            WeaponController weapon = FindAnyObjectByType<WeaponController>();
            string ammo = weapon == null
                ? ""
                : (weapon.Reloading ? "Reloading" : weapon.Loaded + " / " + weapon.Reserve);

            int seconds = Mathf.CeilToInt(timeLeft);
            string clock = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            GUI.Label(new Rect(24f, 20f, 420f, 36f), "Deer  " + kills + " / " + quota, title);
            GUI.Label(new Rect(24f, 56f, 420f, 28f), "Score  " + score + "    " + clock, body);
            GUI.Label(new Rect(24f, 84f, 420f, 28f), ammo, body);
            if (!string.IsNullOrEmpty(lastRange))
                GUI.Label(new Rect(24f, 112f, 420f, 28f), "Last kill  " + lastRange, body);
        }

        void DrawResults()
        {
            Rect panel = new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 120f, 440f, 240f);
            GUI.Box(panel, won ? "Hunt complete" : "Time ran out");
            GUI.Label(new Rect(panel.x + 24f, panel.y + 50f, 400f, 32f),
                "Deer  " + kills + " / " + quota + "     Score  " + score, body);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 86f, 400f, 32f),
                string.IsNullOrEmpty(lastRange) ? "" : "Last shot  " + lastRange, body);
            if (GUI.Button(new Rect(panel.x + 24f, panel.y + 150f, 180f, 40f), "Restart"))
            {
                Time.timeScale = 1f;
                Scene scene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(scene.name);
            }
        }

        void EnsureStyles()
        {
            if (title != null)
                return;

            title = new GUIStyle(GUI.skin.label);
            title.fontSize = 26;
            title.fontStyle = FontStyle.Bold;
            title.normal.textColor = Color.white;
            body = new GUIStyle(GUI.skin.label);
            body.fontSize = 18;
            body.normal.textColor = new Color(0.92f, 0.92f, 0.88f);
        }
    }
}
