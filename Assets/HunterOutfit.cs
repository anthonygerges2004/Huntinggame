using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dresses the player's skinned mannequin as a hunter. Instead of gluing boxes onto bones, the
/// body mesh itself is split into clothing regions by which bone drives each vertex:
/// camouflage jacket with a raised collar, canvas trousers, leather boots, gloves, and skin only
/// on the face. The clothes bend with the body in every animation. Fitted accessories (brimmed
/// hat, beard, backpack with straps, belt with buckle and pouches) are sized from the model's own
/// measurements and ride on the right bones.
/// </summary>
public static class HunterOutfit
{
    enum Region { Skin, Jacket, Gloves, Trousers, Boots, Vest }
    const int RegionCount = 6;

    sealed class Body
    {
        public Transform[] bones;
        public Matrix4x4[] bind;
        public Vector3[] verts;
        public int[] dominant;
        public Vector3 forward, right, up;
        public float H(Vector3 meshPoint) => Vector3.Dot(meshPoint, up); // height along the body

        public int Bone(string name)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null)
                    continue;
                string n = bones[i].name;
                int colon = n.LastIndexOf(':');
                if ((colon >= 0 ? n.Substring(colon + 1) : n) == name)
                    return i;
            }
            return -1;
        }

        public Vector3 BonePos(int i) => i < 0 ? Vector3.zero : bind[i].inverse.MultiplyPoint3x4(Vector3.zero);
        public string BoneName(int i)
        {
            if (i < 0 || bones[i] == null) return "";
            string n = bones[i].name;
            int colon = n.LastIndexOf(':');
            return colon >= 0 ? n.Substring(colon + 1) : n;
        }
    }

    /// <summary>Returns false (and changes nothing) if the body mesh cannot be read.</summary>
    public static bool Apply(Transform player)
    {
        SkinnedMeshRenderer skin = FindBody(player);
        if (skin == null || skin.sharedMesh == null || !skin.sharedMesh.isReadable)
            return false;

        Mesh source = skin.sharedMesh;
        Body b = new Body
        {
            bones = skin.bones,
            bind = source.bindposes,
            verts = source.vertices,
        };
        if (b.bones == null || b.bind == null || b.bind.Length != b.bones.Length || b.verts.Length == 0)
            return false;

        BoneWeight[] weights = source.boneWeights;
        if (weights == null || weights.Length != b.verts.Length)
            return false;

        b.dominant = new int[b.verts.Length];
        for (int i = 0; i < weights.Length; i++)
            b.dominant[i] = weights[i].boneIndex0;

        // Axes in mesh space, taken from the skeleton (imported models are often Z-up inside):
        // up from hips to head, forward from ankle to toes.
        int hipsBone = b.Bone("Hips"), headBone = b.Bone("Head");
        Vector3 up = hipsBone >= 0 && headBone >= 0 ? b.BonePos(headBone) - b.BonePos(hipsBone) : Vector3.up;
        b.up = up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.up;
        int foot = b.Bone("LeftFoot"), toe = b.Bone("LeftToeBase");
        Vector3 fwd = foot >= 0 && toe >= 0 ? b.BonePos(toe) - b.BonePos(foot) : Vector3.forward;
        fwd = Vector3.ProjectOnPlane(fwd, b.up);
        if (fwd.sqrMagnitude < 1e-8f)
            fwd = Vector3.ProjectOnPlane(Vector3.forward, b.up);
        b.forward = fwd.normalized;
        b.right = Vector3.Cross(b.up, b.forward).normalized;

        Mesh dressed = Object.Instantiate(source);
        dressed.name = source.name + " (hunter)";
        SplitIntoRegions(b, source, dressed);
        skin.sharedMesh = dressed;
        // A readable hunter: hunter-orange vest over a brown waxed-canvas jacket, olive trousers,
        // dark boots, tan gloves. Distinct tones so the silhouette reads at any distance.
        skin.sharedMaterials = new[]
        {
            MeshForge.Lit("HunterSkin", new Color(0.80f, 0.60f, 0.47f), 0.32f),
            JacketMaterial(),
            MeshForge.Lit("HunterGloves", new Color(0.46f, 0.32f, 0.19f), 0.42f),
            Tiled(MeshForge.Lit("HunterTrousers", Color.white, 0.1f, 0f, MeshForge.Fabric(new Color(0.27f, 0.27f, 0.17f), "Canvas")), 4f),
            MeshForge.Lit("HunterBoots", new Color(0.17f, 0.11f, 0.07f), 0.5f),
            Tiled(MeshForge.Lit("HunterVest", Color.white, 0.22f, 0f, MeshForge.Fabric(new Color(0.98f, 0.40f, 0.06f), "BlazeOrange")), 4f),
        };

        AddHatAndFace(b);
        AddBackpack(b);
        AddBelt(b);
        return true;
    }

    /// <summary>Brown waxed canvas, shared with the first-person sleeves.</summary>
    public static Material JacketMaterial()
    {
        return Tiled(MeshForge.Lit("HunterJacket", Color.white, 0.3f, 0f, MeshForge.Fabric(new Color(0.40f, 0.29f, 0.18f), "WaxedCanvas")), 3f);
    }

    static Material Tiled(Material m, float tiling)
    {
        m.SetTextureScale("_BaseMap", Vector2.one * tiling);
        m.mainTextureScale = Vector2.one * tiling;
        return m;
    }

    static SkinnedMeshRenderer FindBody(Transform player)
    {
        SkinnedMeshRenderer best = null;
        foreach (SkinnedMeshRenderer s in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (s.name.IndexOf("Joints", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (best == null || (s.sharedMesh != null && best.sharedMesh != null && s.sharedMesh.vertexCount > best.sharedMesh.vertexCount))
                best = s;
        }
        return best;
    }

    // ---------------------------------------------------------------- clothing regions

    static void SplitIntoRegions(Body b, Mesh source, Mesh dressed)
    {
        float hipsY = b.H(b.BonePos(b.Bone("Hips")));
        float kneeY = b.H(b.BonePos(b.Bone("LeftLeg")));
        float ankleY = b.H(b.BonePos(b.Bone("LeftFoot")));
        float neckY = b.H(b.BonePos(b.Bone("Neck")));
        float headY = b.H(b.BonePos(b.Bone("Head")));
        float bootTop = ankleY + (kneeY - ankleY) * 0.38f;
        float hemY = hipsY - (hipsY - kneeY) * 0.16f;
        float collarTop = neckY + (headY - neckY) * 0.5f;

        var region = new Region[b.verts.Length];
        for (int i = 0; i < b.verts.Length; i++)
        {
            string bone = b.BoneName(b.dominant[i]);
            float y = b.H(b.verts[i]);
            Region r;
            if (bone.Contains("Hand") || bone.Contains("Thumb") || bone.Contains("Index") || bone.Contains("Middle")
                || bone.Contains("Ring") || bone.Contains("Pinky"))
                r = Region.Gloves;
            else if (bone.StartsWith("Head"))
                r = Region.Skin;
            else if (bone == "Neck")
                r = y < collarTop ? Region.Jacket : Region.Skin;
            else if (bone.Contains("Foot") || bone.Contains("Toe"))
                r = Region.Boots;
            else if (bone.Contains("UpLeg") || bone.EndsWith("Leg"))
                r = y < bootTop ? Region.Boots : Region.Trousers;
            else if (bone == "Hips")
                r = y > hemY ? Region.Vest : Region.Trousers;
            else if (bone.StartsWith("Spine"))
                r = Region.Vest;
            else if (bone.Contains("ForeArm"))
            {
                // Gauntlet cuffs: the last stretch of forearm before the hand is glove.
                int hand = b.Bone(bone.StartsWith("Left") ? "LeftHand" : "RightHand");
                int fore = b.dominant[i];
                float len = Vector3.Distance(b.BonePos(fore), b.BonePos(hand));
                r = hand >= 0 && Vector3.Distance(b.verts[i], b.BonePos(hand)) < len * 0.2f ? Region.Gloves : Region.Jacket;
            }
            else
                r = Region.Jacket;
            region[i] = r;
        }

        var lists = new List<int>[RegionCount];
        for (int k = 0; k < RegionCount; k++)
            lists[k] = new List<int>();

        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] tris = source.GetTriangles(sub);
            for (int t = 0; t < tris.Length; t += 3)
            {
                Region ra = region[tris[t]], rb = region[tris[t + 1]], rc = region[tris[t + 2]];
                Region r = ra == rb || ra == rc ? ra : (rb == rc ? rb : ra);
                List<int> l = lists[(int)r];
                l.Add(tris[t]); l.Add(tris[t + 1]); l.Add(tris[t + 2]);
            }
        }

        dressed.subMeshCount = RegionCount;
        for (int k = 0; k < RegionCount; k++)
            dressed.SetTriangles(lists[k], k);
    }

    // ---------------------------------------------------------------- measurement

    struct Extent
    {
        public Vector3 centre;   // mesh space
        public float width, height, depth; // along right, up, forward
        public float front, back;          // forward offsets of the surface from the centre
    }

    static Extent Measure(Body b, System.Func<string, bool> boneFilter, System.Func<Vector3, bool> pointFilter = null)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = -min;
        Vector3 sum = Vector3.zero;
        int n = 0;
        for (int i = 0; i < b.verts.Length; i++)
        {
            if (!boneFilter(b.BoneName(b.dominant[i])))
                continue;
            Vector3 v = b.verts[i];
            if (pointFilter != null && !pointFilter(v))
                continue;
            Vector3 c = new Vector3(Vector3.Dot(v, b.right), b.H(v), Vector3.Dot(v, b.forward));
            min = Vector3.Min(min, c);
            max = Vector3.Max(max, c);
            sum += v;
            n++;
        }

        Extent e = new Extent();
        if (n == 0)
            return e;
        Vector3 mid = (min + max) * 0.5f;
        e.centre = b.right * mid.x + b.up * mid.y + b.forward * mid.z;
        e.width = max.x - min.x;
        e.height = max.y - min.y;
        e.depth = max.z - min.z;
        e.front = max.z - mid.z;
        e.back = mid.z - min.z;
        return e;
    }

    /// <summary>
    /// A holder under a bone whose local axes are the character's (x right, y up, z forward)
    /// in mesh units, placed at a mesh-space point. Parts built inside it follow the bone.
    /// </summary>
    static Transform Mount(Body b, int bone, string name, Vector3 meshPoint)
    {
        Matrix4x4 m = b.bind[bone];
        Transform holder = new GameObject(name).transform;
        holder.SetParent(b.bones[bone], false);
        holder.localPosition = m.MultiplyPoint3x4(meshPoint);
        Vector3 f = m.MultiplyVector(b.forward), u = m.MultiplyVector(b.up);
        holder.localRotation = Quaternion.LookRotation(f.normalized, u.normalized);
        holder.localScale = Vector3.one * u.magnitude;
        return holder;
    }

    // ---------------------------------------------------------------- accessories

    static void AddHatAndFace(Body b)
    {
        int head = b.Bone("Head");
        if (head < 0)
            return;

        Extent h = Measure(b, n => n.StartsWith("Head"));
        if (h.height <= 0f)
            return;

        Transform root = Mount(b, head, "_HunterHead", h.centre);
        float r = Mathf.Max(h.width, h.depth) * 0.5f;
        Material felt = MeshForge.Lit("HatFelt", new Color(0.96f, 0.42f, 0.08f), 0.2f);
        Material band = MeshForge.Lit("HatBand", new Color(0.12f, 0.08f, 0.05f), 0.3f);
        Material hair = MeshForge.Lit("HunterHair", new Color(0.17f, 0.11f, 0.07f), 0.2f);
        float top = h.height * 0.5f;

        // Crown: a rounded dome, turned upright.
        Mesh crown = MeshForge.Lathe(new[]
        {
            new Vector2(r * 1.07f, 0f), new Vector2(r * 1.05f, r * 0.3f), new Vector2(r * 0.97f, r * 0.58f),
            new Vector2(r * 0.78f, r * 0.80f), new Vector2(r * 0.42f, r * 0.92f), new Vector2(0f, r * 0.95f),
        }, 24, true, false);
        Vector3 crownBase = new Vector3(0f, top - h.height * 0.34f, -h.depth * 0.03f);
        MeshForge.Part(root, "HatCrown", crown, felt, crownBase, Quaternion.Euler(-90f, 0f, 0f));
        Mesh bandMesh = MeshForge.Lathe(new[] { new Vector2(r * 1.085f, 0f), new Vector2(r * 1.075f, r * 0.2f) }, 24);
        MeshForge.Part(root, "HatBand", bandMesh, band, crownBase + Vector3.up * r * 0.02f, Quaternion.Euler(-90f, 0f, 0f));

        // Brim: a wide flat disc, tipped slightly forward like a worn hunting hat.
        GameObject brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(brim.GetComponent<Collider>());
        brim.name = "HatBrim";
        brim.transform.SetParent(root, false);
        brim.transform.localPosition = crownBase + Vector3.up * r * 0.01f;
        brim.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
        brim.transform.localScale = new Vector3(r * 3.5f, r * 0.03f, r * 3.6f);
        brim.GetComponent<Renderer>().sharedMaterial = felt;

        // A little hair at the back of the head under the brim.
        MeshForge.Ball(root, "Hair", new Vector3(0f, top - h.height * 0.46f, -h.depth * 0.2f),
                       new Vector3(h.width * 0.96f, h.height * 0.42f, h.depth * 0.7f), hair);
    }

    static void AddBackpack(Body b)
    {
        int spine = b.Bone("Spine2");
        if (spine < 0)
            spine = b.Bone("Spine1");
        if (spine < 0)
            return;

        Extent torso = Measure(b, n => n.StartsWith("Spine"));
        if (torso.height <= 0f)
            return;

        float w = torso.width * 0.62f, h = torso.height * 0.72f, d = torso.depth * 0.42f;
        Vector3 centre = torso.centre + b.up * torso.height * 0.08f - b.forward * (torso.back + d * 0.45f);
        Transform root = Mount(b, spine, "_HunterPack", centre);

        Material canvas = Tiled(MeshForge.Lit("PackCanvas", Color.white, 0.12f, 0f, MeshForge.Fabric(new Color(0.27f, 0.29f, 0.18f), "PackFabric")), 2f);
        Material leather = MeshForge.Lit("PackLeather", new Color(0.20f, 0.13f, 0.08f), 0.35f);

        MeshForge.Part(root, "PackBody", MeshForge.Extrude(MeshForge.RoundedRect(new Vector2(-d * 0.5f, -h * 0.5f), new Vector2(d * 0.5f, h * 0.5f), d * 0.35f),
                                                         w * 0.5f, d * 0.12f), canvas, Vector3.zero, Quaternion.identity);
        MeshForge.Part(root, "PackFlap", MeshForge.Extrude(MeshForge.RoundedRect(new Vector2(-d * 0.55f, h * 0.18f), new Vector2(d * 0.1f, h * 0.53f), d * 0.2f),
                                                         w * 0.52f, d * 0.08f), canvas, Vector3.zero, Quaternion.identity);
        MeshForge.Part(root, "PackPocket", MeshForge.Extrude(MeshForge.RoundedRect(new Vector2(-d * 0.75f, -h * 0.4f), new Vector2(-d * 0.3f, h * 0.05f), d * 0.15f),
                                                           w * 0.3f, d * 0.06f), canvas, Vector3.zero, Quaternion.identity);
        for (int side = -1; side <= 1; side += 2)
            MeshForge.Rod(root, "FlapStrap", new Vector3(side * w * 0.22f, h * 0.53f, -d * 0.2f), new Vector3(side * w * 0.22f, h * 0.05f, -d * 0.62f), d * 0.04f, leather);

        // Shoulder straps: over the top of the shoulder and down the chest.
        int shoulder = b.Bone("LeftArm");
        float shoulderY = shoulder >= 0 ? b.H(b.BonePos(shoulder)) : b.H(torso.centre) + torso.height * 0.4f;
        float topY = shoulderY - b.H(centre) + torso.height * 0.06f;
        float frontZ = torso.front + torso.back + d * 0.45f + torso.depth * 0.04f;
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * torso.width * 0.2f;
            Vector3 packTop = new Vector3(x, h * 0.45f, d * 0.45f);
            Vector3 overShoulder = new Vector3(x, topY, (frontZ + d * 0.45f) * 0.5f);
            Vector3 chest = new Vector3(x * 1.1f, topY - torso.height * 0.45f, frontZ);
            MeshForge.Rod(root, "Strap", packTop, overShoulder, d * 0.07f, leather);
            MeshForge.Rod(root, "Strap", overShoulder, chest, d * 0.07f, leather);
        }
    }

    static void AddBelt(Body b)
    {
        int hips = b.Bone("Hips");
        if (hips < 0)
            return;

        float hipsY = b.H(b.BonePos(hips));
        float kneeY = b.H(b.BonePos(b.Bone("LeftLeg")));
        float beltY = hipsY - (hipsY - kneeY) * 0.12f;
        float band = (hipsY - kneeY) * 0.1f;
        Extent waist = Measure(b, n => n == "Hips" || n == "Spine" || n.Contains("UpLeg"), v => Mathf.Abs(b.H(v) - beltY) < band);
        if (waist.width <= 0f)
            return;

        Transform root = Mount(b, hips, "_HunterBelt", waist.centre + b.up * (beltY - b.H(waist.centre)));
        Material leather = MeshForge.Lit("BeltLeather", new Color(0.19f, 0.12f, 0.07f), 0.38f);

        Mesh ring = MeshForge.Lathe(new[] { new Vector2(1f, -0.5f), new Vector2(1f, 0.5f) }, 28, false, false);
        GameObject beltRing = MeshForge.Part(root, "Belt", ring, leather, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
        beltRing.transform.localScale = new Vector3(waist.width * 0.53f, waist.depth * 0.55f, band * 1.1f);

        MeshForge.Part(root, "Buckle", MeshForge.Extrude(MeshForge.RoundedRect(new Vector2(-band * 0.2f, -band * 0.7f), new Vector2(band * 0.2f, band * 0.7f), band * 0.15f),
                                                       band * 0.65f, band * 0.08f), MeshForge.Brass,
                       new Vector3(0f, 0f, waist.depth * 0.55f), Quaternion.identity);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 at = new Vector3(side * waist.width * 0.42f, -band * 1.2f, waist.depth * 0.2f);
            MeshForge.Part(root, "Pouch", MeshForge.Extrude(MeshForge.RoundedRect(new Vector2(-band * 1.1f, -band * 1.6f), new Vector2(band * 1.1f, band * 0.9f), band * 0.4f),
                                                          band * 0.7f, band * 0.15f), leather, at, Quaternion.Euler(0f, side * 70f, 0f));
        }
    }
}
