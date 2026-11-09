using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Pause menu (Esc). Its own canvas above the HUD, so it stays up while the HUD fades out.
//   left    title, day and time, Resume / Hunt log / Settings / Controls / Quit
//   right   the chosen page
public partial class GameHUD
{
    static readonly string[,] ControlKeys =
    {
        { "WASD", "Move" }, { "Shift", "Sprint / steady aim" }, { "Ctrl", "Crouch" }, { "Space", "Jump" },
        { "LMB", "Fire" }, { "RMB", "Aim" }, { "R", "Reload" }, { "1-5", "Hotbar" },
        { "G", "Inventory" }, { "F", "Light fire" }, { "C", "Cook" }, { "Q", "Eat" },
        { "Z", "Drink" }, { "X", "Place bait" }, { "E", "Chop / talk" }, { "B", "Build" },
        { "V", "Camera" }, { "T", "Bird cage" }, { "Y", "Sniper post" }, { "H", "Controls card" },
        { "Tab", "Hunter's Instinct" }, { "P", "Photo mode" }, { "J", "Journal" }, { "Esc", "Pause" },
    };

    Canvas pauseCanvas;
    CanvasGroup pauseGroup;
    TMP_Text pauseSubtitle;
    readonly List<(Button button, TMP_Text label, RectTransform page)> pauseTabs = new List<(Button, TMP_Text, RectTransform)>();
    int pauseTab;
    readonly List<Image> pauseTabMarkers = new List<Image>();

    readonly List<(TMP_Text value, System.Func<string> read)> logRows = new List<(TMP_Text, System.Func<string>)>();
    Slider sensSlider, fovSlider, volumeSlider;
    TMP_Text sensValue, fovValue, volumeValue, invertValue;

    // ---------------------------------------------------------------- build

    void BuildPause()
    {
        EnsureEventSystem();

        GameObject root = new GameObject("PauseMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                         typeof(GraphicRaycaster), typeof(CanvasGroup));
        pauseCanvas = root.GetComponent<Canvas>();
        pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pauseCanvas.sortingOrder = 90;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        pauseGroup = root.GetComponent<CanvasGroup>();
        SetPauseVisible(false, true);

        Image dim = Img(root.transform, "Dim", null, new Color(0.01f, 0.015f, 0.02f, 0.62f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true; // swallow clicks so nothing behind the menu reacts

        const float cardW = 760f;
        const float cardH = 520f;
        RectTransform card = Panel(root.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero,
                                   new Vector2(cardW, cardH), new Color(0.045f, 0.05f, 0.06f, 0.94f), 14f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = Vector2.zero;

        // Left column.
        const float colW = 230f;
        Image divider = Img(card, "Divider", null, new Color(1f, 1f, 1f, 0.07f));
        Place(divider.rectTransform, new Vector2(0f, 0.5f), new Vector2(colW, 0f), new Vector2(1f, cardH - 48f), new Vector2(0.5f, 0.5f));

        TMP_Text title = Text(card, "PAUSED", 30f, TextMain, TextAlignmentOptions.MidlineLeft, true);
        title.characterSpacing = 12f;
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -30f), new Vector2(colW - 40f, 36f), new Vector2(0f, 1f));

        pauseSubtitle = Text(card, "", 12.5f, Gold, TextAlignmentOptions.MidlineLeft, true);
        pauseSubtitle.characterSpacing = 4f;
        Place(pauseSubtitle.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -70f), new Vector2(colW - 40f, 16f), new Vector2(0f, 1f));

        const float pageX = colW + 32f;
        const float pageW = cardW - pageX - 30f;
        string[] tabNames = { "HUNT LOG", "JOURNAL", "SETTINGS", "CONTROLS" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            GameObject pageGo = new GameObject("Page_" + tabNames[i], typeof(RectTransform));
            pageGo.transform.SetParent(card, false);
            RectTransform page = pageGo.GetComponent<RectTransform>();
            Place(page, new Vector2(0f, 1f), new Vector2(pageX, -30f), new Vector2(pageW, cardH - 60f), new Vector2(0f, 1f));

            int index = i;
            Button tab = MakeButton(card, tabNames[i], new Vector2(22f, -120f - i * 50f), new Vector2(colW - 44f, 40f),
                                    () => ShowPauseTab(index), out TMP_Text tabLabel);
            pauseTabs.Add((tab, tabLabel, page));
            Image marker = Img(tab.transform, "Marker", roundSprite, Gold, 2f);
            Place(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(4f, 18f), new Vector2(0f, 0.5f));
            pauseTabMarkers.Add(marker);
        }

        MakeButton(card, "RESUME", new Vector2(22f, -cardH + 112f), new Vector2(colW - 44f, 42f),
                   () => GamePause.Set(false), out TMP_Text resumeLabel);
        resumeLabel.color = Gold;
        MakeButton(card, "QUIT GAME", new Vector2(22f, -cardH + 62f), new Vector2(colW - 44f, 36f), QuitGame, out TMP_Text quitLabel);
        quitLabel.color = TextDim;

        BuildLogPage(pauseTabs[0].page, pageW);
        BuildJournalPage(pauseTabs[1].page, pageW);
        BuildSettingsPage(pauseTabs[2].page, pageW);
        BuildControlsPage(pauseTabs[3].page, pageW);
        ShowPauseTab(0);
    }

    void BuildLogPage(RectTransform page, float width)
    {
        PageTitle(page, "HUNT LOG", width);

        (string label, System.Func<string> read)[] rows =
        {
            ("Days survived", () => DayNightCycle.Instance != null ? DayNightCycle.Instance.Day.ToString() : "1"),
            ("Animals taken", () => HuntFeedback.Kills.ToString()),
            ("   Deer", () => HuntFeedback.KillsOf(AnimalKind.Deer).ToString()),
            ("   Birds", () => HuntFeedback.KillsOf(AnimalKind.Bird).ToString()),
            ("   Wild horses", () => HuntFeedback.KillsOf(AnimalKind.Large).ToString()),
            ("   Legendary", () => HuntFeedback.KillsOf(AnimalKind.Legendary).ToString()),
            ("Longest kill", () => HuntFeedback.LongestKill > 0.5f ? Mathf.RoundToInt(HuntFeedback.LongestKill) + " m" : "—"),
            ("Accuracy", () => HuntFeedback.ShotsFired > 0
                ? Mathf.RoundToInt(HuntFeedback.Accuracy01 * 100f) + "%  (" + HuntFeedback.ShotsLanded + " of " + HuntFeedback.ShotsFired + ")"
                : "—"),
            ("Meat harvested", () => HuntFeedback.MeatTaken.ToString()),
            ("Challenges", () => challengesDone + " / 5"),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            float y = -46f - i * 34f;
            bool sub = rows[i].label.StartsWith(" ");
            if (!sub)
            {
                Image line = Img(page, "Rule", null, new Color(1f, 1f, 1f, 0.05f));
                Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, y + 6f), new Vector2(width, 1f), new Vector2(0f, 1f));
            }

            TMP_Text label = Text(page, rows[i].label.Trim(), sub ? 13.5f : 15f, sub ? TextDim : TextMain,
                                  TextAlignmentOptions.MidlineLeft, !sub);
            Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(sub ? 18f : 0f, y), new Vector2(width * 0.6f, 24f), new Vector2(0f, 1f));
            TMP_Text value = Text(page, "", sub ? 14f : 16f, sub ? TextDim : Gold, TextAlignmentOptions.MidlineRight, true);
            Place(value.rectTransform, new Vector2(1f, 1f), new Vector2(0f, y), new Vector2(width * 0.5f, 24f), new Vector2(1f, 1f));
            logRows.Add((value, rows[i].read));
        }
    }

    void BuildSettingsPage(RectTransform page, float width)
    {
        PageTitle(page, "SETTINGS", width);

        sensSlider = MakeSlider(page, "MOUSE SENSITIVITY", -60f, width, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, out sensValue);
        sensSlider.onValueChanged.AddListener(v => { GameSettings.Sensitivity = v; RefreshSettingLabels(); });

        fovSlider = MakeSlider(page, "FIELD OF VIEW", -130f, width, GameSettings.MinFov, GameSettings.MaxFov, out fovValue);
        fovSlider.wholeNumbers = true;
        fovSlider.onValueChanged.AddListener(v => { GameSettings.Fov = v; RefreshSettingLabels(); });

        volumeSlider = MakeSlider(page, "MASTER VOLUME", -200f, width, 0f, 1f, out volumeValue);
        volumeSlider.onValueChanged.AddListener(v => { GameSettings.Volume = v; RefreshSettingLabels(); });

        TMP_Text invertLabel = Text(page, "INVERT MOUSE Y", 13f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        invertLabel.characterSpacing = 4f;
        Place(invertLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -276f), new Vector2(width * 0.6f, 20f), new Vector2(0f, 1f));
        MakeButton(page, "", new Vector2(width - 90f, -270f), new Vector2(90f, 32f), () =>
        {
            GameSettings.InvertY = !GameSettings.InvertY;
            RefreshSettingLabels();
        }, out invertValue);

        TMP_Text note = Text(page, "Settings are saved automatically.", 12f, TextDim, TextAlignmentOptions.MidlineLeft, false);
        Place(note.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -330f), new Vector2(width, 18f), new Vector2(0f, 1f));
    }

    void BuildControlsPage(RectTransform page, float width)
    {
        PageTitle(page, "CONTROLS", width);
        int rows = (ControlKeys.GetLength(0) + 1) / 2;
        float colW = width * 0.5f;
        for (int i = 0; i < ControlKeys.GetLength(0); i++)
        {
            int col = i / rows;
            int row = i % rows;
            float x = col * colW;
            float y = -48f - row * 34f;
            float capW = KeyCap(page, ControlKeys[i, 0], new Vector2(x, y));
            float textX = Mathf.Max(54f, capW + 10f);
            TMP_Text action = Text(page, ControlKeys[i, 1], 14f, TextMain, TextAlignmentOptions.MidlineLeft, false);
            Place(action.rectTransform, new Vector2(0f, 1f), new Vector2(x + textX, y), new Vector2(colW - textX, 20f), new Vector2(0f, 1f));
        }
    }

    void PageTitle(RectTransform page, string text, float width)
    {
        TMP_Text title = Text(page, text, 13f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        title.characterSpacing = 8f;
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -4f), new Vector2(width, 18f), new Vector2(0f, 1f));
        Image rule = Img(page, "TitleRule", null, Gold);
        Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -26f), new Vector2(36f, 2f), new Vector2(0f, 1f));
    }

    // ---------------------------------------------------------------- update

    void UpdatePause(float dt)
    {
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.isDead;
        if (dead && GamePause.Active)
            GamePause.Set(false);

        // J opens the pause menu straight on the journal.
        if (!dead && !GamePause.Active && Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame
            && (Inventory.Instance == null || !Inventory.Instance.BagOpen))
        {
            GamePause.Set(true);
            if (GamePause.Active)
            {
                OnPauseOpened();
                ShowPauseTab(1);
            }
        }

        if (escape && !dead)
        {
            if (!GamePause.Active && Inventory.Instance != null && Inventory.Instance.BagOpen)
                Inventory.Instance.CloseBag();
            else
            {
                GamePause.Set(!GamePause.Active);
                if (GamePause.Active)
                    OnPauseOpened();
            }
        }

        SetPauseVisible(GamePause.Active, false);
        if (!GamePause.Active)
            return;

        DayNightCycle day = DayNightCycle.Instance;
        string sub = day != null ? "DAY " + day.Day + "  ·  " + day.TimeLabel().ToUpperInvariant() : "";
        if (pauseSubtitle.text != sub)
            pauseSubtitle.text = sub;

        if (pauseTab == 1)
            UpdateJournalPage();

        if (pauseTab == 0)
        {
            for (int i = 0; i < logRows.Count; i++)
            {
                string v = logRows[i].read();
                if (logRows[i].value.text != v)
                    logRows[i].value.text = v;
            }
        }
    }

    void OnPauseOpened()
    {
        float fov = GameSettings.Fov > 1f
            ? GameSettings.Fov
            : (CameraViewSwitcher.Instance != null ? CameraViewSwitcher.Instance.BaseFov : 70f);
        sensSlider.SetValueWithoutNotify(GameSettings.Sensitivity);
        fovSlider.SetValueWithoutNotify(Mathf.Clamp(fov, GameSettings.MinFov, GameSettings.MaxFov));
        volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        RefreshSettingLabels();
        ShowPauseTab(pauseTab);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void RefreshSettingLabels()
    {
        sensValue.text = GameSettings.Sensitivity.ToString("0.00") + "x";
        fovValue.text = Mathf.RoundToInt(fovSlider.value) + "°";
        volumeValue.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
        invertValue.text = GameSettings.InvertY ? "ON" : "OFF";
        invertValue.color = GameSettings.InvertY ? Gold : TextDim;
    }

    void ShowPauseTab(int index)
    {
        pauseTab = index;
        for (int i = 0; i < pauseTabs.Count; i++)
        {
            bool on = i == index;
            pauseTabs[i].page.gameObject.SetActive(on);
            pauseTabs[i].label.color = on ? Gold : TextMain;
            pauseTabMarkers[i].enabled = on;
        }
    }

    void SetPauseVisible(bool on, bool instant)
    {
        if (pauseGroup == null)
            return;
        float target = on ? 1f : 0f;
        pauseGroup.alpha = instant ? target : Mathf.MoveTowards(pauseGroup.alpha, target, Time.unscaledDeltaTime * 8f);
        pauseGroup.interactable = on;
        pauseGroup.blocksRaycasts = on;
    }

    static void QuitGame()
    {
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null)
            return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ---------------------------------------------------------------- widgets

    Button MakeButton(Transform parent, string label, Vector2 topLeft, Vector2 size, UnityEngine.Events.UnityAction onClick,
                      out TMP_Text text)
    {
        Image bg = Img(parent, "Button_" + label, roundSprite, Color.white, 8f);
        bg.raycastTarget = true;
        Place(bg.rectTransform, new Vector2(0f, 1f), topLeft, size, new Vector2(0f, 1f));

        Button button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.06f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.15f);
        colors.pressedColor = new Color(0.96f, 0.77f, 0.40f, 0.45f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.06f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.02f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener(onClick);

        text = Text(bg.rectTransform, label, 14f, TextMain, TextAlignmentOptions.Center, true);
        text.characterSpacing = 6f;
        Stretch(text.rectTransform);
        return button;
    }

    Slider MakeSlider(RectTransform parent, string label, float top, float width, float min, float max, out TMP_Text value)
    {
        TMP_Text name = Text(parent, label, 13f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        name.characterSpacing = 4f;
        Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, top), new Vector2(width * 0.7f, 20f), new Vector2(0f, 1f));
        value = Text(parent, "", 15f, TextMain, TextAlignmentOptions.MidlineRight, true);
        Place(value.rectTransform, new Vector2(1f, 1f), new Vector2(0f, top), new Vector2(width * 0.3f, 20f), new Vector2(1f, 1f));

        GameObject go = new GameObject("Slider_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        Place(rect, new Vector2(0f, 1f), new Vector2(0f, top - 28f), new Vector2(width, 20f), new Vector2(0f, 1f));

        Image track = Img(rect, "Track", roundSprite, TrackColor, 3f);
        track.raycastTarget = true;
        track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        track.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(0f, 6f);
        track.rectTransform.anchoredPosition = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(rect, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.sizeDelta = new Vector2(0f, 6f);
        fillAreaRect.anchoredPosition = Vector2.zero;
        Image fill = Img(fillAreaRect, "Fill", roundSprite, Gold, 3f);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(rect, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(9f, 0f);
        handleAreaRect.offsetMax = new Vector2(-9f, 0f);
        Image handle = Img(handleAreaRect, "Handle", discSprite, TextMain);
        handle.raycastTarget = true;
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(18f, 0f);

        Slider slider = go.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        Navigation nav = slider.navigation;
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;
        return slider;
    }
}
