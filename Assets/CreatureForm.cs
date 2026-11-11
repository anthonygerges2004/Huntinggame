using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared smooth parts for birds and ground game. One mesh per shape, lit materials
/// cached by colour, so a flock does not build a new sphere every feather.
/// </summary>
public static class CreatureForm
{
    public enum Shape
    {
        Ball,
        Beak,
        Feather,
        Limb
    }

    static Mesh ball;
    static Mesh beak;
    static Mesh feather;
    static Mesh limb;
    static Shader shader;
    static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();

    public static Material Fur(Color color)
    {
        return Coat(color, 0.07f, false);
    }

    public static Material Keratin(Color color)
    {
        return Coat(color, 0.4f, false);
    }

    public static Material Eye(Color color)
    {
        return Coat(color, 0.82f, false);
    }

    public static Material FeatherMat(Color color)
    {
        return Coat(color, 0.14f, true);
    }

    public static GameObject Add(Transform parent, Shape shape, Material mat, string name, Vector3 pos, Vector3 scale)
    {
        return Add(parent, shape, mat, name, pos, Quaternion.identity, scale);
    }

    public static GameObject Add(Transform parent, Shape shape, Material mat, string name, Vector3 pos, Quaternion rotation, Vector3 scale)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localRotation = rotation;
        part.transform.localScale = scale;
        MeshFilter filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = MeshFor(shape);
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return part;
    }

    static Material Coat(Color color, float smooth, bool twoSided)
    {
        int key = (Mathf.RoundToInt(color.r * 255f) << 24)
            ^ (Mathf.RoundToInt(color.g * 255f) << 16)
            ^ (Mathf.RoundToInt(color.b * 255f) << 8)
            ^ Mathf.RoundToInt(smooth * 100f)
            ^ (twoSided ? 1 << 7 : 0);

        if (materials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
        }

        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", smooth);
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", smooth);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0f);
        if (twoSided && mat.HasProperty("_Cull"))
            mat.SetFloat("_Cull", 0f);
        materials[key] = mat;
        return mat;
    }

    static Mesh MeshFor(Shape shape)
    {
        switch (shape)
        {
            case Shape.Beak: return beak != null ? beak : (beak = BuildBeak());
            case Shape.Feather: return feather != null ? feather : (feather = BuildFeather());
            case Shape.Limb: return limb != null ? limb : (limb = BuildLimb());
            default: return ball != null ? ball : (ball = BuildBall());
        }
    }

    static Mesh BuildBall()
    {
        const int stacks = 9;
        const int slices = 12;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        for (int y = 0; y <= stacks; y++)
        {
            float phi = Mathf.PI * y / stacks;
            for (int x = 0; x <= slices; x++)
            {
                float theta = Mathf.PI * 2f * x / slices;
                Vector3 n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                norms.Add(n);
                verts.Add(n * 0.5f);
            }
        }

        int row = slices + 1;
        for (int y = 0; y < stacks; y++)
        {
            for (int x = 0; x < slices; x++)
            {
                int a = y * row + x;
                int b = a + row;
                tris.Add(a);
                tris.Add(b);
                tris.Add(a + 1);
                tris.Add(a + 1);
                tris.Add(b);
                tris.Add(b + 1);
            }
        }

        return Finish("CreatureBall", verts, norms, tris);
    }

    static Mesh BuildBeak()
    {
        const int sides = 6;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        verts.Add(new Vector3(0f, 0f, 1f));
        norms.Add(Vector3.forward);
        for (int i = 0; i <= sides; i++)
        {
            float a = Mathf.PI * 2f * i / sides;
            Vector3 side = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            verts.Add(side * 0.5f);
            norms.Add(new Vector3(side.x, side.y, 0.35f).normalized);
        }

        for (int i = 0; i < sides; i++)
        {
            tris.Add(0);
            tris.Add(i + 1);
            tris.Add(i + 2);
        }

        int cap = verts.Count;
        verts.Add(Vector3.zero);
        norms.Add(Vector3.back);
        for (int i = 0; i < sides; i++)
        {
            tris.Add(cap);
            tris.Add(i + 2);
            tris.Add(i + 1);
        }

        return Finish("CreatureBeak", verts, norms, tris);
    }

    static Mesh BuildLimb()
    {
        const int sides = 6;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        for (int y = 0; y <= 1; y++)
        {
            for (int i = 0; i <= sides; i++)
            {
                float a = Mathf.PI * 2f * i / sides;
                Vector3 n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                norms.Add(n);
                verts.Add(n * 0.5f + Vector3.down * y);
            }
        }

        int row = sides + 1;
        for (int i = 0; i < sides; i++)
        {
            int a = i;
            int b = i + row;
            tris.Add(a);
            tris.Add(a + 1);
            tris.Add(b);
            tris.Add(a + 1);
            tris.Add(b + 1);
            tris.Add(b);
        }

        return Finish("CreatureLimb", verts, norms, tris);
    }

    static Mesh BuildFeather()
    {
        Vector3[] front =
        {
            new Vector3(0f, 0f, -0.5f),
            new Vector3(0.04f, 0.22f, 0f),
            new Vector3(0f, 0f, 0.5f),
            new Vector3(0.48f, 0.16f, -0.24f),
            new Vector3(0.48f, 0.16f, 0.24f),
            new Vector3(1f, 0.02f, 0f)
        };
        int[] faces =
        {
            0, 1, 3,
            1, 4, 3,
            1, 2, 4,
            3, 4, 5
        };

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = verts.Count;
            Vector3 n = pass == 0 ? Vector3.up : Vector3.down;
            for (int i = 0; i < front.Length; i++)
            {
                verts.Add(front[i]);
                norms.Add(n);
            }

            for (int i = 0; i < faces.Length; i += 3)
            {
                if (pass == 0)
                {
                    tris.Add(start + faces[i]);
                    tris.Add(start + faces[i + 1]);
                    tris.Add(start + faces[i + 2]);
                }
                else
                {
                    tris.Add(start + faces[i]);
                    tris.Add(start + faces[i + 2]);
                    tris.Add(start + faces[i + 1]);
                }
            }
        }

        return Finish("CreatureFeather", verts, norms, tris);
    }

    static Mesh Finish(string name, List<Vector3> verts, List<Vector3> norms, List<int> tris)
    {
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
