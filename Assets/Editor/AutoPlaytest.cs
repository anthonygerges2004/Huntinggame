using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Plays the real game scene headless and walks the pelt economy end to end:
/// kill a deer, take the carcass, open the bag, visit the tanner, skin, sell, visit the butcher,
/// skin a second kill by hand, and open the pause menu. Every step is checked and photographed
/// (HUD included) into the output folder.
///
/// Run:  Unity.exe -batchmode -projectPath . -executeMethod AutoPlaytest.Run -playtestOut <folder> -logFile <log>
/// Exit code 0 when every check passes.
/// </summary>
[InitializeOnLoad]
public static class AutoPlaytest
{
    // Unity's own services can force a script reload shortly after start-up, which wipes static
    // state. The run is remembered in SessionState and resumes from the top after any reload.
    const string OutKey = "HG.Playtest.Out";
    const string StartKey = "HG.Playtest.Start";

    static AutoPlaytest()
    {
        EditorApplication.delayCall += Resume;
    }

    static void Resume()
    {
        string saved = SessionState.GetString(OutKey, string.Empty);
        if (string.IsNullOrEmpty(saved) || routine != null)
            return;

        outDir = saved;
        startedAt = SessionState.GetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
        AssetDatabase.DisallowAutoRefresh();
        EditorApplication.update -= Watchdog;
        EditorApplication.update += Watchdog;
        if (EditorApplication.isPlaying)
        {
            Log("(resumed after a script reload; starting the playthrough from the top)");
            StartSteps();
        }
        else
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    const string ScenePath = "Assets/Scenes/GetStarted_Scene.unity";
    const double TimeoutSeconds = 420;

    static string outDir;
    static IEnumerator routine;
    static double waitUntil;
    static double startedAt;
    static readonly StringBuilder log = new StringBuilder();
    static int checks;
    static int failures;
    static int shot;

    public static void Run()
    {
        outDir = ArgAfter("-playtestOut") ?? Path.Combine(Application.dataPath, "../PlaytestOutput");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "playtest.log"), string.Empty);
        SessionState.SetString(OutKey, outDir);
        SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
        Log("Playtest starting. Output: " + outDir);
        // A re-import while playing would reload scripts mid-run (TMP re-saves a font asset on load).
        AssetDatabase.DisallowAutoRefresh();
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        startedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update += Watchdog;
        EditorApplication.isPlaying = true;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
            return;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        StartSteps();
    }

    static void StartSteps()
    {
        checks = 0;
        failures = 0;
        shot = 0;
        routine = Environment.CommandLine.Contains("-census") ? Census() : Steps();
        waitUntil = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Watchdog()
    {
        if (EditorApplication.timeSinceStartup - startedAt < TimeoutSeconds)
            return;
        Fail("Timed out after " + TimeoutSeconds + " s");
        Finish();
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < waitUntil)
            return;

        try
        {
            if (!routine.MoveNext())
            {
                Finish();
                return;
            }

            if (routine.Current is float seconds)
                waitUntil = EditorApplication.timeSinceStartup + seconds;
        }
        catch (Exception e)
        {
            Fail("Exception: " + e);
            Finish();
        }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update -= Watchdog;
        Log("");
        Log("RESULT: " + (checks - failures) + " / " + checks + " checks passed" + (failures == 0 ? "" : ", " + failures + " FAILED"));
        SessionState.EraseString(OutKey);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    // ---------------------------------------------------------------- the playthrough

    static IEnumerator Steps()
    {
        Log("Waiting for the world to build…");
        yield return 10f;

        PlayerMove player = UnityEngine.Object.FindAnyObjectByType<PlayerMove>();
        Inventory inv = Inventory.Instance;
        Check(player != null, "Player exists");
        Check(inv != null, "Inventory exists");
        Check(GameHUD.Instance != null, "HUD exists");
        if (player == null || inv == null)
            yield break;

        Check(inv.coins == 20, "Start with 20 coins (have " + inv.coins + ")");
        Check(inv.matches == 3, "Start with 3 matches");
        Photo("01_start_hud");

        CastleShop tanner = FindShop(CastleShop.Kind.Tanner);
        CastleShop butcher = FindShop(CastleShop.Kind.Butchery);
        Check(tanner != null, "Tanner stall built in the castle market");
        Check(butcher != null, "Butcher stall still there");

        // ---- a clean kill
        inv.SelectHotbar(2, false); // sniper
        yield return 0.5f;
        Animal deer = SpawnDeer(player.transform, 7f);
        yield return 1.5f;
        Check(deer != null && !deer.Dead, "Test deer spawned");
        AnimalTrophy trophy = deer != null ? deer.GetComponent<AnimalTrophy>() : null;
        Log("  Deer rolled: " + (trophy != null ? trophy.tier + " " + trophy.coat + " " + Mathf.RoundToInt(trophy.weightKg) + " kg" : "no trophy data"));

        KillWithHeadShots(deer, player.transform);
        yield return 1.2f;
        Check(deer != null && deer.Dead, "Deer killed by head shots");
        AnimalCarcass carcass = deer != null ? deer.GetComponent<AnimalCarcass>() : null;
        Check(carcass != null && carcass.goods != null, "Kill left a carcass (not loose meat)");
        Check(UnityEngine.Object.FindAnyObjectByType<MeatPickup>() == null, "No meat dropped on the ground");
        if (carcass == null)
            yield break;

        HuntGoods goods = carcass.goods;
        Log("  Carcass: " + goods.Title + ", " + goods.ConditionLabel + ", " + Mathf.RoundToInt(goods.weightKg) + " kg, "
            + goods.meat + " meat, pelt ~" + goods.PeltValue(Time.time) + " coins");
        Check(goods.condition == PeltCondition.Pristine, "Head shot with the sniper leaves a pristine hide");

        Teleport(player, carcass.transform.position + (player.transform.position - carcass.transform.position).normalized * 1.6f,
                 carcass.transform.position);
        yield return 0.8f;
        Check(AnimalCarcass.Focus == carcass, "Standing at the carcass shows the take / skin prompt");
        Photo("02_carcass_prompt_and_kill_banner");

        int expectedPelt = goods.PeltValue(Time.time);
        inv.TakeCarcass(carcass);
        yield return 0.4f;
        Check(inv.CarcassCount == 1, "Carcass is in the bag");
        Check(carcass == null, "Carcass removed from the world");
        Log("  Hauling " + Mathf.RoundToInt(inv.CarryKg) + " kg, speed x" + Inventory.CarrySpeedMultiplier.ToString("0.00")
            + (Inventory.TooHeavyToSprint ? ", no sprinting" : ""));
        Check(Inventory.CarrySpeedMultiplier < 1f, "A carcass slows you down");

        // ---- the bag
        SetField(inv, "inventoryOpen", true);
        int carcassSlot = FindSlot(inv, "Carcass");
        inv.SlotClicked(carcassSlot);
        yield return 1f;
        Photo("03_backpack_with_carcass");
        SetField(inv, "inventoryOpen", false);
        yield return 0.4f;

        // ---- the tanner
        Teleport(player, tanner.transform.position + tanner.transform.forward * 1.5f, tanner.transform.position);
        yield return 1f;
        Check(ShopShowing(), "Standing at the tanner opens his panel");
        Photo("04_tanner_with_carcass");

        int coinsBefore = inv.coins;
        int skinned = inv.SkinAllCarcasses(out int meat);
        yield return 0.6f;
        Check(skinned == 1 && inv.PeltCount == 1 && inv.CarcassCount == 0, "Tanner turned the carcass into a pelt");
        Check(meat == goods.meat && inv.meat >= meat, "Tanner gave all the meat (" + meat + ")");
        Check(inv.TotalPeltValue() == expectedPelt, "Pelt is worth what was appraised (" + inv.TotalPeltValue() + ")");
        Photo("05_tanner_after_skinning");

        int earned = inv.SellAllPelts(out int sold);
        yield return 0.8f;
        Check(sold == 1 && earned == expectedPelt, "Sold the pelt for " + earned + " coins");
        Check(inv.coins == coinsBefore + earned, "Coins went from " + coinsBefore + " to " + inv.coins);
        Photo("06_tanner_after_selling");

        // ---- the butcher next door
        Teleport(player, butcher.transform.position + butcher.transform.forward * 1.5f, butcher.transform.position);
        yield return 1f;
        Check(ShopShowing(), "Butcher panel shows");
        Photo("07_butcher");

        // ---- a rough job: shotgun up close, skinned by hand
        inv.SelectHotbar(0, false); // shotgun
        yield return 0.5f;
        Animal second = SpawnDeer(player.transform, 6f);
        yield return 1.5f;
        KillWithHeadShots(second, player.transform);
        yield return 1f;
        AnimalCarcass rough = second != null ? second.GetComponent<AnimalCarcass>() : null;
        Check(rough != null, "Second kill left a carcass");
        if (rough != null)
        {
            Check(rough.goods.condition == PeltCondition.Torn, "Buckshot at close range tears the hide (" + rough.goods.condition + ")");
            int meatBefore = inv.meat;
            int fullMeat = rough.goods.meat;
            inv.FieldSkin(rough);
            yield return 0.6f;
            Check(inv.PeltCount == 1, "Skinning by hand gave a pelt");
            Check(inv.meat - meatBefore == Mathf.CeilToInt(fullMeat * HuntGoods.RoughMeatShare), "Hand skinning keeps only part of the meat");
            Log("  Rough pelt worth " + inv.TotalPeltValue() + " coins");
            SetField(inv, "inventoryOpen", true);
            inv.SlotClicked(FindSlot(inv, "Pelt"));
            yield return 1f;
            Photo("08_backpack_with_rough_pelt");
            SetField(inv, "inventoryOpen", false);
            yield return 0.3f;
        }

        // ---- buying with coins
        int coinsNow = inv.coins;
        CastleShop guns = FindShop(CastleShop.Kind.Guns);
        Check(guns != null, "Gun shop exists");
        if (guns != null)
        {
            Teleport(player, guns.transform.position + guns.transform.forward * 1.5f, guns.transform.position);
            yield return 1f;
            Photo("09_gun_shop_prices_in_coins");
            int shells = PlayerShotgun.Instance.CartridgeCount(HuntWeapon.Shotgun);
            Invoke(guns, "BuyCartridges", HuntWeapon.Shotgun, 8, 2, "shotgun");
            yield return 0.3f;
            Check(inv.coins == coinsNow - 6, "8 shotgun shells cost 6 coins (coins " + coinsNow + " -> " + inv.coins + ")");
            Check(PlayerShotgun.Instance.CartridgeCount(HuntWeapon.Shotgun) == shells + 8, "Shells were added");
        }

        // ---- pause menu hunt log
        GamePause.Set(true);
        yield return 0.8f;
        Photo("10_pause_hunt_log");
        GamePause.Set(false);
        yield return 0.3f;
        Check(HuntProgress.PeltsSold == 1 && HuntProgress.CoinsEarned >= earned, "Hunt log counts pelts and coins");
    }

    // ---------------------------------------------------------------- census (diagnostics)

    /// <summary>Lists every animal and every broken-shader renderer, and photographs the start.</summary>
    static IEnumerator Census()
    {
        Log("Census: waiting for the world…");
        yield return 25f;
        PlayerMove player = UnityEngine.Object.FindAnyObjectByType<PlayerMove>();
        Vector3 me = player != null ? player.transform.position : Vector3.zero;
        Log("Player at " + me);

        var counts = new System.Collections.Generic.Dictionary<string, int>();
        foreach (Animal a in UnityEngine.Object.FindObjectsByType<Animal>(FindObjectsInactive.Include))
        {
            string key = a.kind + (a.kind == AnimalKind.Game ? "/" + a.quarry : a.kind == AnimalKind.Bird ? "/" + a.flyer : "");
            counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            int shown = 0, total = 0;
            Bounds bounds = new Bounds(a.transform.position, Vector3.zero);
            foreach (Renderer r in a.GetComponentsInChildren<Renderer>(true))
            {
                total++;
                if (r.enabled && r.gameObject.activeInHierarchy) { shown++; bounds.Encapsulate(r.bounds); }
            }
            WorldGround.TryGetGround(a.transform.position, out Vector3 ground, out _);
            float feet = shown > 0 ? bounds.min.y - ground.y : float.NaN;
            Log(string.Format("  {0,-22} active={1} present={2} dead={3} dist={4,6:0} rootAboveGround={5,6:0.00} lowestMeshAboveGround={6,6:0.00} renderers={7}/{8} size={9}",
                key, a.gameObject.activeInHierarchy, a.IsPresent(), a.Dead, Vector3.Distance(me, a.transform.position),
                a.transform.position.y - ground.y, feet, shown, total, bounds.size));
        }
        foreach (var kv in counts)
            Log("COUNT " + kv.Key + " = " + kv.Value);

        foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || m.shader == null || !m.shader.isSupported || m.shader.name.Contains("InternalError"))
                {
                    Log("BROKEN SHADER: " + HierarchyPath(r.transform) + " shader=" + (m == null ? "null material" : m.shader == null ? "null" : m.shader.name)
                        + " at " + r.transform.position + " dist " + Vector3.Distance(me, r.transform.position).ToString("0"));
                    break;
                }
            }
        }

        Photo("census_first_person");
        if (CameraViewSwitcher.Instance != null)
        {
            SetField(CameraViewSwitcher.Instance, "isFirstPerson", false); Invoke(CameraViewSwitcher.Instance, "ApplyParenting"); Invoke(CameraViewSwitcher.Instance, "ApplyBodyVisibility"); Invoke(CameraViewSwitcher.Instance, "SnapCamera");
            yield return 1f;
            Photo("census_third_person");
        }
    }

    static string HierarchyPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    // ---------------------------------------------------------------- helpers

    static CastleShop FindShop(CastleShop.Kind kind)
    {
        foreach (CastleShop s in UnityEngine.Object.FindObjectsByType<CastleShop>(FindObjectsInactive.Exclude))
            if (s.ShopKind == kind)
                return s;
        return null;
    }

    static Animal SpawnDeer(Transform player, float distance)
    {
        Vector3 spot = WorldGround.Snap(player.position + player.forward * distance);
        GameObject body = new GameObject("PlaytestDeer");
        body.transform.position = spot;
        body.transform.rotation = Quaternion.LookRotation(-player.forward);
        body.AddComponent<CapsuleCollider>();
        Animal a = body.AddComponent<Animal>();
        a.kind = AnimalKind.Deer;
        a.meatAmount = 2;
        return a;
    }

    static void KillWithHeadShots(Animal a, Transform player)
    {
        if (a == null)
            return;
        Transform head = null;
        foreach (Transform t in a.GetComponentsInChildren<Transform>())
            if (t.name == "scull") { head = t; break; }
        Vector3 point = head != null ? head.position : a.transform.position + Vector3.up * 1.6f;
        Vector3 dir = (point - player.position).normalized;
        for (int i = 0; i < 6 && !a.Dead; i++)
            a.Hit(dir, point, -dir);
    }

    static void Teleport(PlayerMove player, Vector3 position, Vector3 lookAt)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.position = WorldGround.Snap(position) + Vector3.up * 0.1f;
        Vector3 face = lookAt - player.transform.position;
        face.y = 0f;
        if (face.sqrMagnitude > 0.01f)
            player.transform.rotation = Quaternion.LookRotation(face);
        if (cc != null) cc.enabled = true;
    }

    static bool ShopShowing()
    {
        FieldInfo f = typeof(GameHUD).GetField("shopAskedFrame", BindingFlags.NonPublic | BindingFlags.Instance);
        int frame = f != null ? (int)f.GetValue(GameHUD.Instance) : -100;
        return frame >= Time.frameCount - 2;
    }

    static int FindSlot(Inventory inv, string kindName)
    {
        FieldInfo slotsField = typeof(Inventory).GetField("slots", BindingFlags.NonPublic | BindingFlags.Instance);
        Array slots = (Array)slotsField.GetValue(inv);
        for (int i = 0; i < slots.Length; i++)
        {
            object slot = slots.GetValue(i);
            object kind = slot.GetType().GetField("kind").GetValue(slot);
            int count = (int)slot.GetType().GetField("count").GetValue(slot);
            if (count > 0 && kind.ToString() == kindName)
                return i;
        }
        return -1;
    }

    static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }

    static void Invoke(object target, string method, params object[] args)
    {
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }

    /// <summary>Renders the player camera with every overlay canvas moved onto it, so the HUD is in the picture.</summary>
    static void Photo(string name)
    {
        Camera cam = PlayerView.GetCamera();
        if (cam == null)
        {
            Fail("No camera for photo " + name);
            return;
        }

        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        var moved = new System.Collections.Generic.List<Canvas>();
        foreach (Canvas c in canvases)
        {
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.enabled)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.3f) + c.sortingOrder * 0.0001f;
                moved.Add(c);
            }
        }

        // Higher sorting order must draw on top: give it a nearer plane.
        foreach (Canvas c in moved)
            c.planeDistance = 1f - c.sortingOrder * 0.005f;
        Canvas.ForceUpdateCanvases();

        RenderTexture rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        RenderTexture prevTarget = cam.targetTexture;
        RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, request))
            RenderPipeline.SubmitRenderRequest(cam, request);
        else
        {
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;
        }

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        foreach (Canvas c in moved)
        {
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.worldCamera = null;
        }

        string file = Path.Combine(outDir, (++shot).ToString("00") + "_" + name + ".png");
        File.WriteAllBytes(file, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        Log("  [photo] " + Path.GetFileName(file));
    }

    static void Check(bool ok, string what)
    {
        checks++;
        if (!ok)
            failures++;
        Log((ok ? "PASS  " : "FAIL  ") + what);
    }

    static void Fail(string what)
    {
        checks++;
        failures++;
        Log("FAIL  " + what);
    }

    static void Log(string line)
    {
        log.AppendLine(line);
        // Written as we go, so a reload or a hang still leaves a readable record.
        if (!string.IsNullOrEmpty(outDir))
            File.AppendAllText(Path.Combine(outDir, "playtest.log"), line + Environment.NewLine);
        Debug.Log("[Playtest] " + line);
    }

    static string ArgAfter(string flag)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == flag)
                return args[i + 1];
        return null;
    }
}
