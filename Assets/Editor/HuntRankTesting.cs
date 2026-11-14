using UnityEditor;
using UnityEngine;

/// <summary>Test career unlocks in the current scene without earning every rank.</summary>
[InitializeOnLoad]
public static class HuntRankTesting
{
    const string Root = "Hunting Game/Test Rank/";
    const string StartRankKey = "HuntingGame.TestStartRank";
    static bool applyStartingRank;
    public static int StartingRank => SessionState.GetInt(StartRankKey, -1);

    static HuntRankTesting()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            applyStartingRank = state == PlayModeStateChange.EnteredPlayMode;
        };
        EditorApplication.update += ApplyStartingRank;
    }

    static void ApplyStartingRank()
    {
        if (!applyStartingRank || !EditorApplication.isPlaying || Inventory.Instance == null) return;
        applyStartingRank = false;
        if (StartingRank >= 0) SetRank(StartingRank);
    }

    public static void ChooseRank(int index)
    {
        index = Mathf.Clamp(index, 0, HuntProgress.Ranks.Length - 1);
        if (EditorApplication.isPlaying) SetRank(index);
        else SessionState.SetInt(StartRankKey, index);
        SceneView.RepaintAll();
    }

    public static void NormalStart()
    {
        SessionState.SetInt(StartRankKey, -1);
        SceneView.RepaintAll();
    }

    [MenuItem(Root + "1 - Greenhorn (0 XP)")]
    static void Greenhorn() => SetRank(0);

    [MenuItem(Root + "2 - Tracker (150 XP)")]
    static void Tracker() => SetRank(1);

    [MenuItem(Root + "3 - Stalker (400 XP)")]
    static void Stalker() => SetRank(2);

    [MenuItem(Root + "4 - Butcher (800 XP)")]
    static void Butcher() => SetRank(3);

    [MenuItem(Root + "5 - Marksman (1400 XP)")]
    static void Marksman() => SetRank(4);

    [MenuItem(Root + "6 - Apex Hunter (2200 XP)")]
    static void Apex() => SetRank(5);

    [MenuItem(Root + "1 - Greenhorn (0 XP)", true)]
    [MenuItem(Root + "2 - Tracker (150 XP)", true)]
    [MenuItem(Root + "3 - Stalker (400 XP)", true)]
    [MenuItem(Root + "4 - Butcher (800 XP)", true)]
    [MenuItem(Root + "5 - Marksman (1400 XP)", true)]
    [MenuItem(Root + "6 - Apex Hunter (2200 XP)", true)]
    static bool CanSetRank() => EditorApplication.isPlaying && !EditorApplication.isCompiling;

    static void SetRank(int index)
    {
        HuntProgress.SetRankForTesting(index);
        Debug.Log("Test rank set to " + HuntProgress.Current.title + ". Existing inventory is preserved.");
    }
}
