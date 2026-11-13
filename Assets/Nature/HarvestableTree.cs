using System.Collections;
using UnityEngine;

public class HarvestableTree : MonoBehaviour
{
    public int woodAmount = 3;
    public int hitsToFall = 3;
    public float chopRange = 2.2f;
    [Min(0.3f)] public float fallDuration = 2.1f;
    [Min(1)] public int hitsToSplitLog = 3;
    int hits;
    bool chopped, fallen;
    public bool CanBeChopped => isActiveAndEnabled && !chopped;
    float cutHeight;
    Vector3 cutDirection, logStart, logEnd;
    static Material cutMaterial;
    static int promptFrame = -1;
    sealed class CutSite
    {
        public Vector3 localPoint;
        public int hits;
        public Mesh mesh;
        public Transform mark;
    }
    readonly System.Collections.Generic.List<CutSite> cuts = new System.Collections.Generic.List<CutSite>();

    CutSite CutAt(Vector3 point, Vector3 normal)
    {
        foreach (CutSite cut in cuts)
            if (Vector3.Distance(transform.TransformPoint(cut.localPoint), point) < 0.3f) return cut;
        GameObject mark = new GameObject("Blade cut");
        mark.transform.SetPositionAndRotation(point + normal * 0.003f, Quaternion.LookRotation(normal));
        mark.transform.SetParent(transform, true);
        Mesh mesh = new Mesh { name = "Progressive axe notch" };
        mark.AddComponent<MeshFilter>().sharedMesh = mesh;
        mark.AddComponent<MeshRenderer>().sharedMaterial = CutMaterial();
        var site = new CutSite { localPoint = transform.InverseTransformPoint(point), mesh = mesh, mark = mark.transform };
        cuts.Add(site);
        cutMeshes.Add(mesh);
        return site;
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || promptFrame == Time.frameCount) return;
        promptFrame = Time.frameCount;
        if (Inventory.IsOpen || !Inventory.AxeInHand) return;
        HarvestableTree target = AimedTree();
        if (target == null) return;
        bool active = AxeSwing.Instance != null && AxeSwing.Instance.IsChopping;
        ScreenText.Centered(active ? "Chopping - E to stop; move or look away to cancel"
            : target.fallen ? "E: start splitting this trunk - aim at the same cut" : "E: start chopping this tree - keep your aim on the notch",
            Screen.height * 0.5f + 90f * ScreenText.Scale);
    }

    public static HarvestableTree AimedTree()
    {
        Transform player = PlayerView.Body();
        float nearest = 2.2f;
        HarvestableTree best = null;
        foreach (RaycastHit hit in Physics.RaycastAll(PlayerView.AimRay(), nearest, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (player != null && (hit.transform == player || hit.transform.IsChildOf(player))) continue;
            if (hit.distance > nearest) continue;
            nearest = hit.distance;
            best = hit.collider.GetComponentInParent<HarvestableTree>();
        }
        return best != null && best.CanBeChopped && nearest <= best.chopRange ? best : null;
    }

    public bool InChopRange() { return !chopped && AimedTree() == this; }
    // Compatibility entry points also require an actual blade contact.
    public static void ChopLookedAtTree() { if (AxeSwing.Instance != null) AxeSwing.Instance.TryTreeContact(); }
    public void Chop() { ChopLookedAtTree(); }

    public void ChopAt(Vector3 point, Vector3 normal)
    {
        if (chopped || Inventory.IsOpen || Inventory.Instance == null || !AxeSwing.HasAxe || !Inventory.AxeInHand) return;
        if (Vector3.Distance(PlayerView.AimOrigin(), point) > Mathf.Min(chopRange, 2.2f) + 0.15f) return;
        normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        CutSite site = CutAt(point, normal);
        hits = ++site.hits;
        ShowImpact(point, normal, site);
        GameAudio.WoodChop(point);
        if (fallen)
        {
            if (hits < Mathf.Max(1, hitsToSplitLog))
            {
                Inventory.Instance.ShowNotice("Split log " + hits + " / " + hitsToSplitLog);
                return;
            }
            chopped = true;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            for (int i = 0; i < woodAmount; i++)
                InventoryWorldDrop.Spawn(Vector3.Lerp(logStart, logEnd, (i + 0.5f) / woodAmount), "Wood", 1);
            Inventory.Instance.ShowNotice("Look at a small log and hold E to pick it up.");
            Destroy(gameObject);
            return;
        }
        if (hits == 1)
        {
            cutHeight = Mathf.Clamp(point.y - transform.position.y, 0.25f, 2f);
            cutDirection = Vector3.ProjectOnPlane(-normal, Vector3.up).normalized;
            if (cutDirection.sqrMagnitude < 0.01f)
                cutDirection = Vector3.ProjectOnPlane(transform.position - PlayerView.AimOrigin(), Vector3.up).normalized;
        }
        if (hits < Mathf.Max(1, hitsToFall))
        {
            Inventory.Instance.ShowNotice("Chop " + hits + " / " + hitsToFall);
            return;
        }
        chopped = true;
        Inventory.Instance.ShowNotice("Timber!");
        StartCoroutine(FallOver());
    }

    static Material CutMaterial()
    {
        if (cutMaterial == null)
            cutMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                { color = new Color(0.68f, 0.43f, 0.20f) };
        return cutMaterial;
    }

    void ShowImpact(Vector3 point, Vector3 normal, CutSite site)
    {
        // Repeated blade contacts open one notch; separate cuts retain their own progress.
        float width = Mathf.Min(0.22f, 0.095f + site.hits * 0.025f);
        float height = Mathf.Min(0.085f, 0.018f + site.hits * 0.012f);
        float depth = Mathf.Min(0.10f, 0.018f + site.hits * 0.018f);
        site.mesh.vertices = new[] { new Vector3(-width, -height, 0), new Vector3(width, -height, 0),
            new Vector3(width, height, 0), new Vector3(-width, height, 0), new Vector3(0, 0, -depth) };
        site.mesh.triangles = new[] { 0, 4, 1, 1, 4, 2, 2, 4, 3, 3, 4, 0 };
        site.mesh.RecalculateNormals();
        site.mesh.RecalculateBounds();
        DentReadableMeshes(point, normal);
        // No temporary colliders or rigidbodies competing with the axe and player.
        for (int i = 0; i < 5; i++) StartCoroutine(Chip(point, normal));
    }

    readonly System.Collections.Generic.List<Mesh> cutMeshes = new System.Collections.Generic.List<Mesh>();
    readonly System.Collections.Generic.Dictionary<MeshFilter, Mesh> deformed = new System.Collections.Generic.Dictionary<MeshFilter, Mesh>();
    void DentReadableMeshes(Vector3 point, Vector3 normal)
    {
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
        {
            if (filter.gameObject.name == "Blade cut" || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer != null && renderer.bounds.SqrDistance(point) > 0.16f * 0.16f) continue;
            if (!deformed.TryGetValue(filter, out Mesh mesh))
            {
                mesh = Instantiate(filter.sharedMesh);
                filter.sharedMesh = mesh;
                deformed.Add(filter, mesh);
                cutMeshes.Add(mesh);
            }
            Vector3[] vertices = mesh.vertices;
            bool changed = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = filter.transform.TransformPoint(vertices[i]);
                float distance = Vector3.Distance(world, point);
                if (distance > 0.16f) continue;
                world -= normal * (0.04f * (1f - distance / 0.16f));
                vertices[i] = filter.transform.InverseTransformPoint(world);
                changed = true;
            }
            if (changed) { mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds(); }
        }
    }
    void OnDestroy() { foreach (Mesh mesh in cutMeshes) if (mesh != null) Destroy(mesh); }

    IEnumerator Chip(Vector3 point, Vector3 normal)
    {
        GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(chip, 0.6f);
        Collider collider = chip.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        chip.transform.localScale = new Vector3(0.04f, 0.012f, 0.02f);
        chip.GetComponent<Renderer>().sharedMaterial = CutMaterial();
        Vector3 velocity = normal * Random.Range(1f, 2f) + Vector3.up + Random.insideUnitSphere;
        for (float t = 0; t < 0.45f; t += Time.deltaTime)
        {
            chip.transform.position = point + velocity * t + Physics.gravity * (t * t * 0.5f);
            chip.transform.Rotate(Vector3.one * (400f * Time.deltaTime));
            yield return null;
        }
        Destroy(chip);
    }

    IEnumerator FallOver()
    {
        Vector3 basePoint = transform.position;
        float height = 3f;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) height = Mathf.Max(height, r.bounds.max.y - basePoint.y);
        float length = Mathf.Clamp((height - cutHeight) * 0.8f, 2f, 16f);
        float radius = 0.2f;
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            Bounds b = c.bounds;
            if (b.min.y <= basePoint.y + 0.5f) radius = Mathf.Max(radius, Mathf.Min(b.extents.x, b.extents.z));
            c.enabled = false;
        }
        radius = Mathf.Clamp(radius, 0.15f, 0.65f);
        GameObject stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stump.name = "Chopped stump";
        stump.transform.position = basePoint + Vector3.up * cutHeight * 0.5f;
        stump.transform.localScale = new Vector3(radius * 2, cutHeight * 0.5f, radius * 2);
        stump.GetComponent<Renderer>().sharedMaterial = CutMaterial();
        // Stump is decorative: it must not appear inside the player and push them away.
        stump.GetComponent<Collider>().enabled = false;
        Vector3 direction = cutDirection.sqrMagnitude > 0.01f ? cutDirection : Vector3.forward;
        Vector3 pivot = basePoint + Vector3.up * cutHeight;
        float rise = 0;
        for (int i = 1; i <= 16; i++)
        {
            float distance = length * i / 16f;
            if (WorldGround.TryGetGround(pivot + direction * distance, out Vector3 ground, out _))
                rise = Mathf.Max(rise, (ground.y + radius - pivot.y) / distance);
        }
        Vector3 axis = Vector3.Cross(Vector3.up, direction).normalized;
        float angle = Mathf.Clamp(90f - Mathf.Atan(rise) * Mathf.Rad2Deg, 5f, 90f);
        Quaternion start = transform.rotation;
        Vector3 offset = transform.position - pivot;
        for (float t = 0; t < 1f;)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.3f, fallDuration));
            Quaternion turn = Quaternion.AngleAxis(angle * t * t, axis);
            transform.SetPositionAndRotation(pivot + turn * offset, turn * start);
            yield return null;
        }
        Vector3 along = Quaternion.AngleAxis(angle, axis) * Vector3.up;
        logStart = pivot;
        logEnd = pivot + along * length;
        // Retain the original model, without swapping to a different-sized cylinder.
        GameObject surface = new GameObject("Fallen trunk surface");
        surface.transform.SetPositionAndRotation((logStart + logEnd) * 0.5f, Quaternion.LookRotation(along, Vector3.up));
        surface.transform.SetParent(transform, true);
        BoxCollider bridge = surface.AddComponent<BoxCollider>();
        Vector3 scale = surface.transform.lossyScale;
        bridge.size = new Vector3(radius * 1.8f / Mathf.Abs(scale.x), radius * 1.5f / Mathf.Abs(scale.y), length / Mathf.Abs(scale.z));
        Physics.SyncTransforms();
        fallen = true;
        chopped = false;
        hits = 0;
        cuts.Clear(); // Standing-tree cuts must not count as log-splitting blows.
        GameAudio.Hit();
        if (Inventory.Instance != null) Inventory.Instance.ShowNotice("Trunk is solid. Chop it for small logs.");
    }
}
