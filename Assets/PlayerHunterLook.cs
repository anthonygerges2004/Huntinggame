using UnityEngine;

public class PlayerHunterLook : MonoBehaviour
{
    const string HatName = "_HunterHat";

    static readonly Color Skin = new Color(0.78f, 0.60f, 0.48f, 1f);
    static readonly Color Shirt = new Color(0.23f, 0.34f, 0.18f, 1f);
    static readonly Color Belt = new Color(0.18f, 0.11f, 0.07f, 1f);
    static readonly Color Pants = new Color(0.22f, 0.16f, 0.11f, 1f);
    static readonly Color Boots = new Color(0.10f, 0.08f, 0.06f, 1f);
    static readonly Color HatColor = new Color(0.18f, 0.22f, 0.13f, 1f);

    void Awake()
    {
        HideJoints();
        RecolorSkin();
    }

    void Start()
    {
        DressClothes();
        AddHat();
        CameraViewSwitcher cam = GetComponent<CameraViewSwitcher>();
        if (cam != null)
            cam.RefreshBodyRenderers();
    }

    void HideJoints()
    {
        Transform[] parts = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].name.IndexOf("Joints", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Renderer[] renderers = parts[i].GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
                renderers[r].enabled = false;
        }
    }

    void RecolorSkin()
    {
        SkinnedMeshRenderer[] skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skins.Length; i++)
        {
            SkinnedMeshRenderer skin = skins[i];
            if (skin == null || !skin.enabled)
                continue;
            if (skin.name.IndexOf("Joints", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            Material[] mats = skin.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null)
                    continue;
                mats[m].SetColor("_BaseColor", Skin);
                mats[m].SetColor("_Color", Skin);
                mats[m].SetFloat("_Smoothness", 0.18f);
                mats[m].SetFloat("_Metallic", 0f);
            }

            skin.materials = mats;
        }
    }

    void DressClothes()
    {
        if (transform.Find("_HunterOutfit") != null)
            return;

        GameObject marker = new GameObject("_HunterOutfit");
        marker.transform.SetParent(transform, false);

        Material shirt = MakeMat(Shirt, 0.16f);
        Material pants = MakeMat(Pants, 0.14f);
        Material boots = MakeMat(Boots, 0.08f);
        Material belt = MakeMat(Belt, 0.1f);

        Transform hips = FindBone("mixamorig:Hips");
        Transform spine = FindBone("mixamorig:Spine");
        Transform spine1 = FindBone("mixamorig:Spine1");
        Transform spine2 = FindBone("mixamorig:Spine2");
        Transform neck = FindBone("mixamorig:Neck");
        Transform leftArm = FindBone("mixamorig:LeftArm");
        Transform leftFore = FindBone("mixamorig:LeftForeArm");
        Transform leftHand = FindBone("mixamorig:LeftHand");
        Transform rightArm = FindBone("mixamorig:RightArm");
        Transform rightFore = FindBone("mixamorig:RightForeArm");
        Transform rightHand = FindBone("mixamorig:RightHand");
        Transform leftUp = FindBone("mixamorig:LeftUpLeg");
        Transform leftLeg = FindBone("mixamorig:LeftLeg");
        Transform leftFoot = FindBone("mixamorig:LeftFoot");
        Transform rightUp = FindBone("mixamorig:RightUpLeg");
        Transform rightLeg = FindBone("mixamorig:RightLeg");
        Transform rightFoot = FindBone("mixamorig:RightFoot");

        Cover("_HunterShirt", spine1 != null ? spine1 : spine, neck != null ? neck : spine2, 0.38f, 0.26f, shirt, 1.35f);
        Cover("_HunterTorso", spine, spine1 != null ? spine1 : spine2, 0.40f, 0.28f, shirt, 1.2f);
        Cover("_HunterBelt", hips, spine, 0.36f, 0.24f, belt, 0.7f);
        Cover("_HunterSleeveL", leftArm, leftFore, 0.14f, 0.14f, shirt, 1.05f);
        Cover("_HunterForeL", leftFore, leftHand, 0.12f, 0.12f, shirt, 1.0f);
        Cover("_HunterSleeveR", rightArm, rightFore, 0.14f, 0.14f, shirt, 1.05f);
        Cover("_HunterForeR", rightFore, rightHand, 0.12f, 0.12f, shirt, 1.0f);
        Cover("_HunterThighL", leftUp, leftLeg, 0.18f, 0.18f, pants, 1.05f);
        Cover("_HunterShinL", leftLeg, leftFoot, 0.14f, 0.14f, pants, 1.0f);
        Cover("_HunterThighR", rightUp, rightLeg, 0.18f, 0.18f, pants, 1.05f);
        Cover("_HunterShinR", rightLeg, rightFoot, 0.14f, 0.14f, pants, 1.0f);
        Boot(leftFoot, boots);
        Boot(rightFoot, boots);
    }

    void Cover(string name, Transform from, Transform to, float thickX, float thickZ, Material mat, float extraLen)
    {
        if (from == null || to == null)
            return;

        Vector3 a = from.position;
        Vector3 b = to.position;
        Vector3 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.04f)
            return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        StripCollider(go);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.transform.SetParent(from, true);
        go.transform.position = (a + b) * 0.5f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        PlaceWorld(go.transform, go.transform.position, go.transform.rotation, new Vector3(thickX, len * extraLen, thickZ));
    }

    void Boot(Transform foot, Material mat)
    {
        if (foot == null)
            return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "_HunterBoot";
        StripCollider(go);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.transform.SetParent(foot, false);
        PlaceWorld(go.transform, foot.position + Vector3.up * 0.04f + transform.forward * 0.06f, transform.rotation, new Vector3(0.12f, 0.10f, 0.24f));
    }

    void AddHat()
    {
        if (transform.Find(HatName) != null)
            return;

        Transform head = FindBone("mixamorig:Head");
        if (head == null)
            head = FindBone("Head");
        if (head == null)
            return;

        Material mat = MakeMat(HatColor, 0.12f);
        GameObject hat = new GameObject(HatName);
        hat.transform.SetParent(transform, false);

        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        crown.name = "Crown";
        StripCollider(crown);
        crown.GetComponent<Renderer>().sharedMaterial = mat;
        crown.transform.SetParent(head, false);
        PlaceWorld(crown.transform, head.position + Vector3.up * 0.16f, Quaternion.identity, new Vector3(0.22f, 0.07f, 0.22f));

        GameObject brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        brim.name = "Brim";
        StripCollider(brim);
        brim.GetComponent<Renderer>().sharedMaterial = mat;
        brim.transform.SetParent(head, false);
        PlaceWorld(brim.transform, head.position + Vector3.up * 0.10f, Quaternion.identity, new Vector3(0.32f, 0.018f, 0.32f));
    }

    Transform FindBone(string boneName)
    {
        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i].name == boneName)
                return bones[i];
        }

        return null;
    }

    static Material MakeMat(Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        return mat;
    }

    static void StripCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
    }

    static void PlaceWorld(Transform t, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale)
    {
        t.position = worldPos;
        t.rotation = worldRot;
        Vector3 parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(
            worldScale.x / Mathf.Max(0.0001f, parentScale.x),
            worldScale.y / Mathf.Max(0.0001f, parentScale.y),
            worldScale.z / Mathf.Max(0.0001f, parentScale.z));
    }
}
