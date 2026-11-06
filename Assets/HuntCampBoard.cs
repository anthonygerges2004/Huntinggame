using HuntingGame.Wind;
using UnityEngine;

/// <summary>
/// Rotating "weird hunt" rules posted at camp. One bonus challenge per in-game day.
/// </summary>
public class HuntCampBoard : MonoBehaviour
{
    public static HuntCampBoard Instance { get; private set; }

    public float readRadius = 14f;

    public enum DailyRule
    {
        DownwindDeer,
        OneShotKill,
        AfterDusk,
        CrouchDeer,
        GapCrossingKill
    }

    static readonly string[] RuleText =
    {
        "Downwind deer — take a deer while the wind carries your scent away from it.",
        "One clean shot — only your first shotgun blast today may score the daily hunt.",
        "After dusk — today's bonus needs a kill once night falls.",
        "Still hunter — crouch when you drop a deer.",
        "Funnel deer — drop a deer within 80 m of a gap river."
    };

    int dayIndex = -1;
    DailyRule rule;
    bool dailyDone;
    int shotsToday;
    bool showedBoardToday;
    bool wasNearCamp;

    public DailyRule CurrentRule => rule;
    public bool DailyDone => dailyDone;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        RefreshDay();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        RefreshDay();
        TickCampPrompt();
    }

    void RefreshDay()
    {
        int day = CurrentDayIndex();
        if (day == dayIndex)
            return;

        dayIndex = day;
        dailyDone = false;
        shotsToday = 0;
        showedBoardToday = false;
        rule = (DailyRule)(day % RuleText.Length);
    }

    void TickCampPrompt()
    {
        bool near = NearCamp();
        if (near && !wasNearCamp && !showedBoardToday)
        {
            showedBoardToday = true;
            if (Inventory.Instance != null)
            {
                string msg = "Camp board — " + RuleText[(int)rule];
                if (dailyDone)
                    msg += " (done today)";
                Inventory.Instance.ShowNotice(msg);
            }
        }

        wasNearCamp = near;
    }

    static int CurrentDayIndex()
    {
        if (DayNightCycle.Instance == null)
            return Mathf.FloorToInt(Time.timeSinceLevelLoad / 600f);
        return Mathf.FloorToInt(Time.time / DayNightCycle.Instance.dayLengthSeconds);
    }

    public static void RegisterShotgunBlast()
    {
        if (Instance == null)
            return;
        Instance.shotsToday++;
    }

    public static void TryCompleteDaily(Animal animal)
    {
        if (Instance == null || animal == null || Instance.dailyDone)
            return;

        if (!Instance.RuleMet(animal))
            return;

        Instance.dailyDone = true;
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice("Camp board: today's hunt — done!");
    }

    bool RuleMet(Animal animal)
    {
        switch (rule)
        {
            case DailyRule.DownwindDeer:
                return animal.kind == AnimalKind.Deer && PlayerDownwindOf(animal.transform.position);
            case DailyRule.OneShotKill:
                return shotsToday <= 1;
            case DailyRule.AfterDusk:
                return DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
            case DailyRule.CrouchDeer:
                return animal.kind == AnimalKind.Deer && PlayerMove.IsCrouching;
            case DailyRule.GapCrossingKill:
                return animal.kind == AnimalKind.Deer && NearGapRiver(animal.transform.position);
            default:
                return false;
        }
    }

    bool PlayerDownwindOf(Vector3 animalPosition)
    {
        if (WindSystem.Current == null)
            return false;

        Vector3 wind = WindSystem.Current.Direction;
        wind.y = 0f;
        if (wind.sqrMagnitude < 0.0001f)
            return false;
        wind.Normalize();

        Vector3 toAnimal = animalPosition - PlayerView.Position();
        toAnimal.y = 0f;
        if (toAnimal.sqrMagnitude < 0.25f)
            return false;
        toAnimal.Normalize();
        return Vector3.Dot(toAnimal, wind) > 0.55f;
    }

    bool NearGapRiver(Vector3 position)
    {
        GameObject root = GameObject.Find(GapRiverCrossings.GapRiverRoot);
        if (root == null)
            return false;

        for (int i = 0; i < root.transform.childCount; i++)
        {
            Renderer[] renderers = root.transform.GetChild(i).GetComponentsInChildren<Renderer>();
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] == null)
                    continue;
                Vector3 closest = renderers[r].bounds.ClosestPoint(position);
                closest.y = 0f;
                Vector3 flat = position;
                flat.y = 0f;
                if (Vector3.Distance(flat, closest) <= 80f)
                    return true;
            }
        }

        return false;
    }

    bool NearCamp()
    {
        if (Campfire.Instance == null)
            return false;

        return Vector3.Distance(PlayerView.Position(), Campfire.Instance.FireWorldPosition()) <= readRadius;
    }
}
