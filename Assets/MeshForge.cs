using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small procedural modelling kit for props that have no art asset: turned (lathe) parts like
/// barrels and scopes, extruded profiles with softened edges like stocks, and the materials and
/// generated textures (walnut grain, camouflage, fabric) that make them read as real objects.
/// </summary>
public static class MeshForge
{
    // ---------------------------------------------------------------- lathe

    /// <summary>
    /// Revolves a profile around the Z axis. Each point is (radius, z). Ends are capped when the
    /// first or last radius is above zero. Smooth around, crisp where the profile turns sharply.
    /// </summary>
    public static Mesh Lathe(Vector2[] profile, int segments = 20, bool capStart = true, bool capEnd = true)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        // Split the profile into runs at sharp corners so edges stay crisp.
        List<List<Vector2>> runs = SplitAtCorners(profile, 40f);
        foreach (List<Vector2> run in runs)
        {
            int baseIndex = verts.Count;
            for (int i = 0; i < run.Count; i++)
            {
                Vector2 prev = run[Mathf.Max(0, i - 1)];
                Vector2 next = run[Mathf.Min(run.Count - 1, i + 1)];
                Vector2 tangent = (next - prev).normalized; // in (r, z)
                Vector2 n2 = new Vector2(tangent.y, -tangent.x); // outward in (r, z)
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    verts.Add(new Vector3(c * run[i].x, sn * run[i].x, run[i].y));
                    norms.Add(new Vector3(c * n2.x, sn * n2.x, n2.y).normalized);
                    uvs.Add(new Vector2(s / (float)segments, run[i].y * 4f));
                }
            }

            int ring = segments + 1;
            for (int i = 0; i < run.Count - 1; i++)
            {
                for (int s = 0; s < segments; s++)
                {
                    // Wound so faces point outward (profiles run toward +Z).
                    int a = baseIndex + i * ring + s;
                    int b = a + ring;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                }
            }
        }

        if (capStart && profile[0].x > 0.0001f)
            Cap(verts, norms, uvs, tris, profile[0], segments, false);
        if (capEnd && profile[profile.Length - 1].x > 0.0001f)
            Cap(verts, norms, uvs, tris, profile[profile.Length - 1], segments, true);

        return Build("Lathe", verts, norms, uvs, tris);
    }

    static void Cap(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector2 p, int segments, bool end)
    {
        Vector3 n = end ? Vector3.forward : Vector3.back;
        int centre = verts.Count;
        verts.Add(new Vector3(0f, 0f, p.y));
        norms.Add(n);
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int s = 0; s <= segments; s++)
        {
            float a = s / (float)segments * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(a) * p.x, Mathf.Sin(a) * p.x, p.y));
            norms.Add(n);
            uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }

        for (int s = 0; s < segments; s++)
        {
            if (end) { tris.Add(centre); tris.Add(centre + 1 + s); tris.Add(centre + 2 + s); }
            else { tris.Add(centre); tris.Add(centre + 2 + s); tris.Add(centre + 1 + s); }
        }
    }

    static List<List<Vector2>> SplitAtCorners(Vector2[] p, float degrees)
    {
        var runs = new List<List<Vector2>>();
        var run = new List<Vector2> { p[0] };
        for (int i = 1; i < p.Length; i++)
        {
            run.Add(p[i]);
            if (i < p.Length - 1)
            {
                float turn = Vector2.Angle(p[i] - p[i - 1], p[i + 1] - p[i]);
                if (turn > degrees)
                {
                    runs.Add(run);
                    run = new List<Vector2> { p[i] };
                }
            }
        }

        runs.Add(run);
        return runs;
    }

    // ---------------------------------------------------------------- extrude

    /// <summary>
    /// Extrudes a closed outline drawn in the Z/Y plane (side view) across X, from -halfWidth to
    /// +halfWidth, with a bevel that softens every edge. The outline may be concave.
    /// </summary>
    public static Mesh Extrude(Vector2[] outline, float halfWidth, float bevel)
    {
        Vector2[] outer = EnsureCounterClockwise(outline);
        Vector2[] inner = Inset(outer, bevel);
        float flat = Mathf.Max(0.0005f, halfWidth - bevel);

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        // Rings across the width: inner cap edge, outer side, outer side, inner cap edge.
        float[] xs = { -halfWidth, -flat, flat, halfWidth };
        Vector2[][] shapes = { inner, outer, outer, inner };
        int n = outer.Length;

        Vector2[] edgeNormals = OutlineNormals(outer, 35f, out bool[] sharp);
        for (int band = 0; band < 3; band++)
        {
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                // Each edge gets its own quad so sharp corners stay crisp; smooth corners share normals.
                Vector2 nA = sharp[i] ? FaceNormal(outer, i) : edgeNormals[i];
                Vector2 nB = sharp[j] ? FaceNormal(outer, i) : edgeNormals[j];
                float sideA = band == 0 ? -1f : band == 2 ? 1f : 0f;
                float sideB = sideA;
                int b0 = verts.Count;
                AddRingVertex(verts, norms, uvs, shapes[band][i], xs[band], nA, band == 0 ? -0.7f : (band == 2 ? 0f : 0f), sideA);
                AddRingVertex(verts, norms, uvs, shapes[band][j], xs[band], nB, band == 0 ? -0.7f : 0f, sideB);
                AddRingVertex(verts, norms, uvs, shapes[band + 1][i], xs[band + 1], nA, band == 2 ? 0.7f : 0f, sideA);
                AddRingVertex(verts, norms, uvs, shapes[band + 1][j], xs[band + 1], nB, band == 2 ? 0.7f : 0f, sideB);
                tris.Add(b0); tris.Add(b0 + 2); tris.Add(b0 + 1);
                tris.Add(b0 + 1); tris.Add(b0 + 2); tris.Add(b0 + 3);
            }
        }

        // Flat caps on both sides, triangulated from the inset outline.
        List<int> capTris = Triangulate(inner);
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? -halfWidth : halfWidth;
            Vector3 normal = side == 0 ? Vector3.left : Vector3.right;
            int start = verts.Count;
            for (int i = 0; i < n; i++)
            {
                verts.Add(new Vector3(x, inner[i].y, inner[i].x));
                norms.Add(normal);
                uvs.Add(new Vector2(inner[i].x * 4f, inner[i].y * 4f));
            }

            for (int t = 0; t < capTris.Count; t += 3)
            {
                if (side == 0) { tris.Add(start + capTris[t]); tris.Add(start + capTris[t + 1]); tris.Add(start + capTris[t + 2]); }
                else { tris.Add(start + capTris[t]); tris.Add(start + capTris[t + 2]); tris.Add(start + capTris[t + 1]); }
            }
        }

        return Build("Extrude", verts, norms, uvs, tris);
    }

    static void AddRingVertex(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, Vector2 p, float x,
                              Vector2 outward, float lean, float side)
    {
        verts.Add(new Vector3(x, p.y, p.x));
        Vector3 n = new Vector3(lean, outward.y, outward.x);
        norms.Add(n.normalized);
        uvs.Add(new Vector2(p.x * 4f, p.y * 4f + x * 4f));
    }

    static Vector2 FaceNormal(Vector2[] p, int i)
    {
        Vector2 e = p[(i + 1) % p.Length] - p[i];
        return new Vector2(e.y, -e.x).normalized; // outward for a counter-clockwise outline
    }

    static Vector2[] OutlineNormals(Vector2[] p, float sharpDegrees, out bool[] sharp)
    {
        int n = p.Length;
        var normals = new Vector2[n];
        sharp = new bool[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 a = FaceNormal(p, (i - 1 + n) % n);
            Vector2 b = FaceNormal(p, i);
            normals[i] = (a + b).normalized;
            sharp[i] = Vector2.Angle(a, b) > sharpDegrees;
        }

        return normals;
    }

    static Vector2[] EnsureCounterClockwise(Vector2[] p)
    {
        float area = 0f;
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 a = p[i], b = p[(i + 1) % p.Length];
            area += a.x * b.y - b.x * a.y;
        }

        if (area >= 0f)
            return (Vector2[])p.Clone();
        var r = new Vector2[p.Length];
        for (int i = 0; i < p.Length; i++)
            r[i] = p[p.Length - 1 - i];
        return r;
    }

    /// <summary>Moves each point inward along its mitred corner normal.</summary>
    static Vector2[] Inset(Vector2[] p, float d)
    {
        int n = p.Length;
        var r = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 a = FaceNormal(p, (i - 1 + n) % n);
            Vector2 b = FaceNormal(p, i);
            Vector2 m = (a + b).normalized;
            float cos = Mathf.Max(0.35f, Vector2.Dot(m, b));
            r[i] = p[i] - m * (d / cos);
        }

        return r;
    }

    /// <summary>Ear clipping for a simple counter-clockwise polygon.</summary>
    static List<int> Triangulate(Vector2[] p)
    {
        var result = new List<int>();
        var idx = new List<int>();
        for (int i = 0; i < p.Length; i++)
            idx.Add(i);

        int guard = 0;
        while (idx.Count > 3 && guard++ < 5000)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i - 1 + idx.Count) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                Vector2 a = p[ia], b = p[ib], c = p[ic];
                if (Cross(b - a, c - b) <= 0f)
                    continue; // reflex corner
                bool inside = false;
                for (int k = 0; k < idx.Count && !inside; k++)
                {
                    int ik = idx[k];
                    if (ik == ia || ik == ib || ik == ic)
                        continue;
                    inside = PointInTriangle(p[ik], a, b, c);
                }
                if (inside)
                    continue;
                result.Add(ia); result.Add(ib); result.Add(ic);
                idx.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped)
                break; // degenerate outline: give up on the rest rather than loop forever
        }

        if (idx.Count == 3)
        {
            result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]);
        }

        return result;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
        return d1 > 0f && d2 > 0f && d3 > 0f;
    }

    /// <summary>A rounded rectangle outline (side view), for receivers, pads and packs.</summary>
    public static Vector2[] RoundedRect(Vector2 min, Vector2 max, float radius, int cornerSteps = 4)
    {
        radius = Mathf.Min(radius, (max.x - min.x) * 0.5f, (max.y - min.y) * 0.5f);
        var pts = new List<Vector2>();
        Vector2[] centres =
        {
            new Vector2(max.x - radius, min.y + radius),
            new Vector2(max.x - radius, max.y - radius),
            new Vector2(min.x + radius, max.y - radius),
            new Vector2(min.x + radius, min.y + radius),
        };
        for (int c = 0; c < 4; c++)
        {
            float start = -90f + c * 90f;
            for (int s = 0; s <= cornerSteps; s++)
            {
                float a = (start + 90f * s / cornerSteps) * Mathf.Deg2Rad;
                pts.Add(centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        return pts.ToArray();
    }

    // ---------------------------------------------------------------- assembly

    static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
    {
        Mesh mesh = new Mesh { name = name };
        if (v.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    /// <summary>A child object showing one mesh with one material.</summary>
    public static GameObject Part(Transform parent, string name, Mesh mesh, Material mat,
                                  Vector3 localPosition, Quaternion localRotation, Vector3? localScale = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = localScale ?? Vector3.one;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        return go;
    }

    /// <summary>A simple tube between two points, for straps, levers and bipod legs.</summary>
    public static GameObject Rod(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material mat, int segments = 10)
    {
        float len = Vector3.Distance(a, b);
        Mesh m = Lathe(new[] { new Vector2(radius, 0f), new Vector2(radius, len) }, segments);
        Quaternion rot = len > 0.0001f ? Quaternion.LookRotation((b - a) / len) : Quaternion.identity;
        return Part(parent, name, m, mat, a, rot);
    }

    public static GameObject Ball(Transform parent, string name, Vector3 centre, Vector3 size, Material mat)
    {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(s.GetComponent<Collider>());
        s.name = name;
        s.transform.SetParent(parent, false);
        s.transform.localPosition = centre;
        s.transform.localScale = size;
        s.GetComponent<Renderer>().sharedMaterial = mat;
        return s;
    }

    // ---------------------------------------------------------------- materials

    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    public static Material Lit(string key, Color color, float smoothness, float metallic = 0f, Texture2D texture = null)
    {
        if (materials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        Material m = new Material(shader) { name = key };
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (texture != null)
        {
            m.SetTexture("_BaseMap", texture);
            m.SetTexture("_MainTex", texture);
        }

        materials[key] = m;
        return m;
    }

    public static Material Walnut => Lit("Walnut", Color.white, 0.55f, 0f, WoodGrain(new Color(0.42f, 0.22f, 0.10f), new Color(0.24f, 0.11f, 0.05f)));
    public static Material BluedSteel => Lit("BluedSteel", new Color(0.10f, 0.11f, 0.13f), 0.72f, 0.85f);
    public static Material BrightSteel => Lit("BrightSteel", new Color(0.55f, 0.56f, 0.58f), 0.78f, 0.95f);
    public static Material Rubber => Lit("Rubber", new Color(0.05f, 0.05f, 0.05f), 0.18f);
    public static Material Polymer => Lit("Polymer", new Color(0.16f, 0.17f, 0.14f), 0.3f);
    public static Material Glass => Lit("ScopeGlass", new Color(0.06f, 0.12f, 0.18f), 0.96f, 0.3f);
    public static Material Brass => Lit("Brass", new Color(0.78f, 0.62f, 0.30f), 0.75f, 0.9f);

    // ---------------------------------------------------------------- generated textures

    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    /// <summary>Long streaky grain with darker rings, mapped along the length of a part.</summary>
    public static Texture2D WoodGrain(Color light, Color dark)
    {
        string key = "Grain" + light + dark;
        if (textures.TryGetValue(key, out Texture2D t) && t != null)
            return t;

        const int size = 256;
        t = NewTexture(size);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float warp = Mathf.PerlinNoise(u * 2f, v * 6f) * 2.5f;
                float rings = Mathf.Sin((v * 22f + warp) * Mathf.PI);
                float fine = Mathf.PerlinNoise(u * 40f, v * 3f);
                float k = Mathf.Clamp01(0.5f + 0.35f * rings + 0.25f * (fine - 0.5f));
                px[y * size + x] = Color.Lerp(dark, light, k);
            }
        }

        t.SetPixels(px);
        t.Apply(true, true);
        textures[key] = t;
        return t;
    }

    /// <summary>Woodland camouflage blotches in three earth tones over a base.</summary>
    public static Texture2D Camo()
    {
        if (textures.TryGetValue("Camo", out Texture2D t) && t != null)
            return t;

        const int size = 256;
        t = NewTexture(size);
        Color baseC = new Color(0.36f, 0.34f, 0.22f);
        Color olive = new Color(0.24f, 0.27f, 0.15f);
        Color brown = new Color(0.30f, 0.21f, 0.12f);
        Color dark = new Color(0.12f, 0.12f, 0.09f);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                // Tileable noise: sample on a torus.
                float a = Tile(u, v, 3f, 0f), b = Tile(u, v, 4f, 17f), c = Tile(u, v, 6f, 41f);
                Color col = baseC;
                if (a > 0.56f) col = olive;
                if (b > 0.6f) col = brown;
                if (c > 0.66f) col = dark;
                float weave = 0.94f + 0.06f * Mathf.PerlinNoise(x * 0.9f, y * 0.9f);
                px[y * size + x] = col * weave;
            }
        }

        t.SetPixels(px);
        t.Apply(true, true);
        textures["Camo"] = t;
        return t;
    }

    /// <summary>Plain woven fabric: a tint with a faint weave and wear.</summary>
    public static Texture2D Fabric(Color tint, string key)
    {
        if (textures.TryGetValue(key, out Texture2D t) && t != null)
            return t;

        const int size = 128;
        t = NewTexture(size);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float weave = ((x + y) % 2 == 0 ? 0.97f : 1.03f) * (0.9f + 0.1f * Mathf.PerlinNoise(x * 0.08f, y * 0.08f));
                px[y * size + x] = tint * weave;
            }
        }

        t.SetPixels(px);
        t.Apply(true, true);
        textures[key] = t;
        return t;
    }

    static float Tile(float u, float v, float freq, float seed)
    {
        float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
        float x = Mathf.Cos(a) * freq / Mathf.PI, y = Mathf.Sin(a) * freq / Mathf.PI;
        float z = Mathf.Cos(b) * freq / Mathf.PI, w = Mathf.Sin(b) * freq / Mathf.PI;
        return (Mathf.PerlinNoise(x + seed + 10f, z + 5f) + Mathf.PerlinNoise(y + 3f, w + seed + 7f)) * 0.5f;
    }

    static Texture2D NewTexture(int size)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = 4;
        t.hideFlags = HideFlags.DontSave;
        return t;
    }
}
