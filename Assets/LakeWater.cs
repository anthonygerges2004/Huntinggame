using System.Collections.Generic;
using UnityEngine;

public class LakeWater : MonoBehaviour
{
    public static LakeWater Instance;

    public int fishCount = 40;
    public float swimDepth = 1.4f;
    public float respawnSeconds = 20f;

    readonly List<LakeFish> fish = new List<LakeFish>();
    Camera playerCam;

    public float SurfaceY => transform.position.y + transform.lossyScale.y;
    public float Radius => transform.lossyScale.x * 0.5f;
    public Vector3 Center => transform.position;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        PlayerMove move = FindAnyObjectByType<PlayerMove>();
        if (move != null)
            playerCam = move.GetComponentInChildren<Camera>();

        int count = Mathf.Clamp(Mathf.RoundToInt(Radius * 0.35f), 6, fishCount);
        for (int i = 0; i < count; i++)
            SpawnFish();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool Inside(Vector3 position, float margin)
    {
        Vector3 offset = position - Center;
        offset.y = 0f;
        return offset.magnitude < Radius - margin;
    }

    public float GroundY(Vector3 position)
    {
        Terrain terrain = WorldGround.TerrainAt(position);
        if (terrain == null)
            return SurfaceY - 10f;
        return terrain.SampleHeight(position) + terrain.transform.position.y;
    }

    public static bool TryGetSwim(Vector3 position, out float surfaceY)
    {
        surfaceY = 0f;
        if (Instance == null)
            return false;

        surfaceY = Instance.SurfaceY;
        if (!Instance.Inside(position, 0.5f) || position.y > surfaceY + 0.2f)
            return false;

        return Instance.GroundY(position) < surfaceY - Instance.swimDepth;
    }

    public static bool CameraUnderwater()
    {
        if (Instance == null || Instance.playerCam == null)
            return false;

        Vector3 p = Instance.playerCam.transform.position;
        return p.y < Instance.SurfaceY && Instance.Inside(p, 0f) && Instance.GroundY(p) < p.y;
    }

    public bool RandomUnderwaterPoint(out Vector3 point)
    {
        for (int i = 0; i < 25; i++)
        {
            Vector2 r = Random.insideUnitCircle * Radius * 0.85f;
            Vector3 p = Center + new Vector3(r.x, 0f, r.y);
            float ground = GroundY(p);
            if (ground > SurfaceY - 1f)
                continue;

            float low = Mathf.Max(ground + 0.4f, SurfaceY - 1.8f);
            p.y = Random.Range(low, SurfaceY - 0.35f);
            point = p;
            return true;
        }

        point = Center + Vector3.up * (SurfaceY - Center.y - 0.6f);
        return false;
    }

    public LakeFish ClosestFreeFish(Vector3 point)
    {
        LakeFish best = null;
        float bestDistance = float.MaxValue;
        foreach (LakeFish f in fish)
        {
            if (f == null || f.Busy)
                continue;
            float d = (f.transform.position - point).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = f;
            }
        }
        return best;
    }

    public void Catch(LakeFish caught)
    {
        if (caught == null)
            return;

        fish.Remove(caught);
        Destroy(caught.gameObject);
        Invoke(nameof(SpawnFish), respawnSeconds);
    }

    void SpawnFish()
    {
        if (!RandomUnderwaterPoint(out Vector3 point))
            return;
        fish.Add(LakeFish.Spawn(this, point));
    }

    void OnGUI()
    {
        if (!CameraUnderwater())
            return;

        GUI.color = new Color(0.1f, 0.32f, 0.45f, 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
