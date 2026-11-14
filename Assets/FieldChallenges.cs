using System.Collections;
using System.Collections.Generic;
using HuntingGame.Survival;
using UnityEngine;
using UnityEngine.InputSystem;

// Local hunting contracts. Rewards and completion last for the current play session,
// like the existing career/challenge state. Only kills after accepting count.
public class FieldChallenges : MonoBehaviour
{
    public sealed class Post
    {
        public string title, objective, reward;
        public Vector3 position;
        public int goal, progress;
        public bool complete;
        public readonly HashSet<AnimalKind> species = new HashSet<AnimalKind>();
        public Renderer flag;
    }
    public static FieldChallenges Instance { get; private set; }
    public readonly List<Post> Posts = new List<Post>();
    public int WaterBottles { get; private set; }
    public int HeatPacks { get; private set; }
    public int ActiveIndex { get; private set; } = -1;
    public const float HuntRadius = 250f;
    float remaining;
    Transform player;
    GameObject markers;
    Material postMaterial, flagMaterial, doneMaterial;
    const float Duration = 300f;

    IEnumerator Start()
    {
        if (Instance != null && Instance != this) { Destroy(this); yield break; }
        Instance = this;
        // World dressing and animal placement finish before the posts are distributed.
        yield return new WaitForSeconds(1f);
        PlayerMove move = FindAnyObjectByType<PlayerMove>();
        if (move == null) yield break;
        player = move.transform;
        postMaterial = MakeMaterial(new Color(.22f, .14f, .07f));
        flagMaterial = MakeMaterial(new Color(.95f, .65f, .16f));
        doneMaterial = MakeMaterial(new Color(.25f, .7f, .4f));
        markers = new GameObject("Field challenge posts");
        string[] titles = { "Ridge marksman", "Silent stalker", "Bird grounds", "Night watch", "Mixed bag" };
        string[] objectives = { "Hunt an animal from at least 50 m", "Hunt a deer while crouching", "Hunt 2 birds", "Hunt an animal at night", "Hunt 2 different species" };
        string[] rewards = { "2 water bottles (+40 water each)", "2 heat packs (+35 warmth each)", "2 cooked meals", "1 heat pack + 40 coins", "1 water bottle + 60 coins" };
        Animal[] animals = FindObjectsByType<Animal>();
        for (int i = 0; i < titles.Length; i++)
        {
            if (!FindPostPosition(i, animals, out Vector3 position)) continue;
            Post post = new Post { title = titles[i], objective = objectives[i], reward = rewards[i], position = position, goal = i == 2 || i == 4 ? 2 : 1 };
            // Keep the rule index independent of missing placements.
            ruleIndices.Add(i);
            Posts.Add(post);
            BuildPost(post);
        }
    }
    readonly List<int> ruleIndices = new List<int>();

    bool FindPostPosition(int index, Animal[] animals, out Vector3 position)
    {
        float best = float.MaxValue;
        position = default;
        bool found = false;
        for (int attempt = 0; attempt < animals.Length + 80; attempt++)
        {
            Vector3 candidate;
            if (attempt < animals.Length)
            {
                Animal animal = animals[attempt];
                if (animal == null || (index == 1 && animal.kind != AnimalKind.Deer) || (index == 2 && animal.kind != AnimalKind.Bird)) continue;
                candidate = animal.transform.position + new Vector3(16, 0, 12);
            }
            else
            {
                float angle = (attempt * 137.5f + index * 55) * Mathf.Deg2Rad;
                candidate = player.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (150 + (attempt % 8) * 100);
            }
            if (!WorldGround.TryGetGround(candidate, out candidate, out Vector3 normal) || normal.y < .85f || WorldGround.InsideWater(candidate)) continue;
            float spacing = float.MaxValue;
            foreach (Post other in Posts) spacing = Mathf.Min(spacing, FlatDistance(candidate, other.position));
            if (spacing < 140 || FlatDistance(candidate, player.position) < 60) continue;
            float score = Mathf.Abs(FlatDistance(candidate, player.position) - (160 + index * 200));
            if (score < best) { best = score; position = candidate; found = true; }
        }
        return found;
    }

    static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    static Material MakeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader) { color = color };
    }
    void BuildPost(Post post)
    {
        GameObject root = new GameObject(post.title);
        root.transform.SetParent(markers.transform);
        root.transform.position = post.position;
        root.transform.rotation = Quaternion.LookRotation(new Vector3(player.position.x - post.position.x, 0, player.position.z - post.position.z));
        MakePart(root.transform, new Vector3(0, 1.3f, 0), new Vector3(.14f, 2.6f, .14f), postMaterial);
        post.flag = MakePart(root.transform, new Vector3(0, 2.4f, 0), new Vector3(2.4f, 1.4f, .12f), flagMaterial);
        SignText.Make(root.transform, post.title.ToUpperInvariant() + "\nK: START CHALLENGE", new Vector3(0, 2.4f, .075f), Quaternion.identity, new Vector2(2.2f, 1.1f), Color.black);
        SignText.Make(root.transform, post.title.ToUpperInvariant() + "\nK: START CHALLENGE", new Vector3(0, 2.4f, -.075f), Quaternion.Euler(0, 180, 0), new Vector2(2.2f, 1.1f), Color.black);
    }
    static Renderer MakePart(Transform root, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.transform.SetParent(root, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(part.GetComponent<Collider>());
        return part.GetComponent<Renderer>();
    }

    public string HudStatus
    {
        get
        {
            string supplies = $"SUPPLIES   L: water {WaterBottles}   |   O: heat packs {HeatPacks}";
            if (player == null) return "";
            if (ActiveIndex >= 0)
            {
                Post p = Posts[ActiveIndex];
                return $"{p.title.ToUpperInvariant()}   {p.progress}/{p.goal}   {Mathf.CeilToInt(remaining)}s\n{p.objective}\nPost: {FlatDistance(player.position, p.position):0}/{HuntRadius:0} m | K: cancel\nReward: {p.reward}\n{supplies}";
            }
            foreach (Post p in Posts)
                if (FlatDistance(player.position, p.position) <= 8)
                    return p.complete ? $"{p.title}: COMPLETED\n{supplies}" : $"K: START {p.title.ToUpperInvariant()}\n{p.objective} | 5 min | {HuntRadius:0} m area\nReward: {p.reward}\n{supplies}";
            return supplies;
        }
    }

    void Update()
    {
        if (player == null || Inventory.IsOpen) return;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead) { ActiveIndex = -1; return; }
        Keyboard key = Keyboard.current;
        if (ActiveIndex >= 0)
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0 || FlatDistance(player.position, Posts[ActiveIndex].position) > HuntRadius)
            { Notice("Challenge ended: time expired or you left the hunting area. Return to retry."); ActiveIndex = -1; }
        }
        if (key == null) return;
        if (key.kKey.wasPressedThisFrame)
        {
            if (ActiveIndex >= 0) { ActiveIndex = -1; Notice("Challenge cancelled. Return to a post to retry."); }
            else if (!BirdChallenge.Active && !DeerChallenge.Active && !WoodBuilder.IsBuilding)
                for (int i = 0; i < Posts.Count; i++)
                    if (!Posts[i].complete && FlatDistance(player.position, Posts[i].position) <= 8)
                    { ActiveIndex = i; Posts[i].progress = 0; Posts[i].species.Clear(); remaining = Duration; Notice(Posts[i].objective + " — " + Posts[i].reward); break; }
        }
        SurvivalStats stats = SurvivalStats.Current;
        if (stats == null) return;
        if (key.lKey.wasPressedThisFrame && WaterBottles > 0 && stats.Water01 < .999f)
        { WaterBottles--; stats.Drink(.4f); Notice("Drank a reward water bottle: +40 water."); }
        if (key.oKey.wasPressedThisFrame && HeatPacks > 0 && stats.Warmth01 < .999f)
        { HeatPacks--; stats.Warm(.35f); Notice("Used a reward heat pack: +35 warmth."); }
    }

    public static void ReportKill(Animal animal, float distance)
    {
        FieldChallenges self = Instance;
        if (self == null || self.ActiveIndex < 0 || animal == null || BirdChallenge.Active || DeerChallenge.Active) return;
        Post post = self.Posts[self.ActiveIndex];
        if (self.remaining <= 0 || FlatDistance(animal.transform.position, post.position) > HuntRadius || FlatDistance(self.player.position, post.position) > HuntRadius) return;
        int rule = self.ruleIndices[self.ActiveIndex];
        bool qualifies = rule == 0 ? distance >= 50 : rule == 1 ? animal.kind == AnimalKind.Deer && PlayerMove.IsCrouching : rule == 2 ? animal.kind == AnimalKind.Bird : rule == 3 ? DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight : post.species.Add(animal.kind);
        if (!qualifies) return;
        post.progress++;
        if (post.progress < post.goal) { Notice($"{post.title}: {post.progress}/{post.goal}"); return; }
        Inventory bag = Inventory.Instance;
        if (bag == null) return;
        // Cooked meals remain claimable if the bag is full; no reward is silently lost.
        if (rule == 2)
        {
            int added = bag.GiveCooked(2);
            if (added < 2) self.pendingMeals += 2 - added;
        }
        if (rule == 0) self.WaterBottles += 2;
        if (rule == 1) self.HeatPacks += 2;
        if (rule == 3) { self.HeatPacks++; bag.AddCoins(40); }
        if (rule == 4) { self.WaterBottles++; bag.AddCoins(60); }
        post.complete = true;
        if (post.flag != null) post.flag.sharedMaterial = self.doneMaterial;
        self.ActiveIndex = -1;
        HuntFeedback.PushBanner("FIELD CHALLENGE COMPLETE", post.title + " — " + post.reward, true);
        Notice("Reward received: " + post.reward + (self.pendingMeals > 0 ? ". Make bag space; meals will be delivered automatically." : ""));
    }
    int pendingMeals;
    void LateUpdate()
    {
        if (pendingMeals > 0 && Inventory.Instance != null && !Inventory.IsOpen)
            pendingMeals -= Inventory.Instance.GiveCooked(pendingMeals);
    }
    static void Notice(string text) { if (Inventory.Instance != null) Inventory.Instance.ShowNotice(text); }
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (markers != null) Destroy(markers);
        if (postMaterial != null) Destroy(postMaterial);
        if (flagMaterial != null) Destroy(flagMaterial);
        if (doneMaterial != null) Destroy(doneMaterial);
    }
}
