using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class AnimalVisuals
{
    public const string ModelChildName = "_AnimalModel";

    public static void Attach(Animal animal)
    {
        if (animal == null)
            return;

        HidePlaceholderMesh(animal);
        NormalizeParentScale(animal);

        if (animal.kind == AnimalKind.Bird)
        {
            if (animal.transform.Find(BirdVisual.BodyName) == null)
                BirdVisual.Build(animal.transform, animal.flyer, out _, out _);
            FitCollider(animal);
            return;
        }

        if (animal.kind == AnimalKind.Game)
        {
            if (animal.quarry == QuarryKind.None)
                animal.quarry = QuarryKind.Pig;
            if (animal.transform.Find(GroundCritterVisual.BodyName) == null)
                GroundCritterVisual.Build(animal.transform, animal.quarry);
            FitCollider(animal);
            return;
        }

        if (HasSkinnedModel(animal))
        {
            StripImportedControllers(animal.gameObject);
            FixAnimalShading(animal.gameObject);
            EnsureAvatar(animal.gameObject, animal.kind);
            FitCollider(animal);
            AnimalGrounding.Ensure(animal);
            return;
        }

        GameObject prefab = LoadPrefab(animal.kind);
        if (prefab == null)
        {
            Debug.LogWarning($"No visual prefab found for {animal.kind} on {animal.name}.");
            return;
        }

        GameObject visual = CreateVisual(prefab, animal.transform);
        visual.name = ModelChildName;
        visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        visual.transform.localScale = VisualScale(animal.kind);

        StripImportedControllers(visual);
        FixAnimalShading(visual);
        EnsureAvatar(visual, animal.kind);
        FitCollider(animal);
        AnimalGrounding.Ensure(animal);
    }

    static void FixAnimalShading(GameObject visual)
    {
        SkinnedMeshRenderer[] skinned = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
        {
            if (skinned[i] == null)
                continue;
            skinned[i].forceMeshLod = 0;
            skinned[i].updateWhenOffscreen = true;
        }
    }

    public static void AttachAllInScene()
    {
        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < animals.Length; i++)
            Attach(animals[i]);
    }

    static GameObject CreateVisual(GameObject prefab, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#endif
        return Object.Instantiate(prefab, parent);
    }

    static GameObject LoadPrefab(AnimalKind kind)
    {
#if UNITY_EDITOR
        GameObject fromProject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(kind));
        if (fromProject != null)
            return fromProject;
#endif
        return Resources.Load<GameObject>("AnimalModels/" + PrefabFileName(kind));
    }

    static string PrefabPath(AnimalKind kind)
    {
        return "Assets/ithappy/Animals_FREE/Prefabs/" + PrefabFileName(kind) + ".prefab";
    }

    static string PrefabFileName(AnimalKind kind)
    {
        switch (kind)
        {
            case AnimalKind.Bird:
                return "Chicken_001";
            case AnimalKind.Large:
                return "Horse_001";
            case AnimalKind.Legendary:
                return "Tiger_001";
            default:
                return "Deer_001";
        }
    }

    static Vector3 VisualScale(AnimalKind kind)
    {
        switch (kind)
        {
            case AnimalKind.Bird:
                return Vector3.one * 0.85f;
            case AnimalKind.Large:
                return Vector3.one * 1.2f;
            case AnimalKind.Legendary:
                return Vector3.one * 1.1f;
            default:
                return Vector3.one;
        }
    }

    static bool HasSkinnedModel(Animal animal)
    {
        return animal.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
    }

    static void HidePlaceholderMesh(Animal animal)
    {
        MeshRenderer renderer = animal.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.enabled = false;
    }

    static void NormalizeParentScale(Animal animal)
    {
        Vector3 scale = animal.transform.localScale;
        bool stretched = Mathf.Abs(scale.x - scale.z) > 0.05f ||
                         Mathf.Abs(scale.x - scale.y) > 0.05f ||
                         scale.x > 1.5f;

        if (stretched || animal.kind == AnimalKind.Bird)
            animal.transform.localScale = Vector3.one;
    }

    static void FitCollider(Animal animal)
    {
        CapsuleCollider capsule = animal.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            SphereCollider sphere = animal.GetComponent<SphereCollider>();
            if (sphere != null && animal.kind == AnimalKind.Bird)
            {
                float radius = BirdVisual.ColliderRadius(animal.flyer);
                sphere.radius = radius;
                sphere.center = new Vector3(0f, 0.12f, 0f);
            }
            return;
        }

        switch (animal.kind)
        {
            case AnimalKind.Bird:
                float radius = BirdVisual.ColliderRadius(animal.flyer);
                capsule.radius = radius;
                capsule.height = radius * 2.2f;
                capsule.center = new Vector3(0f, 0.12f, 0f);
                capsule.direction = 2;
                break;
            case AnimalKind.Game:
                QuarryProfile game = QuarryProfile.Of(animal.quarry);
                capsule.radius = game.capsuleRadius;
                capsule.height = game.capsuleHeight;
                capsule.center = game.capsuleCenter;
                capsule.direction = 2;
                break;
            case AnimalKind.Large:
                capsule.radius = 0.55f;
                capsule.height = 2.2f;
                capsule.center = new Vector3(0f, 1.1f, 0f);
                break;
            case AnimalKind.Legendary:
                capsule.radius = 0.5f;
                capsule.height = 1.6f;
                capsule.center = new Vector3(0f, 0.8f, 0f);
                break;
            default:
                // Lengthwise along the deer, covering the chest and hindquarters the mesh actually occupies.
                capsule.direction = 2;
                capsule.radius = 0.55f;
                capsule.height = 1.7f;
                capsule.center = new Vector3(0f, 1.05f, -0.17f);
                break;
        }
    }

    static void EnsureAvatar(GameObject visual, AnimalKind kind)
    {
        Animator animator = visual.GetComponent<Animator>();
        if (animator == null)
            animator = visual.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.keepAnimatorStateOnDisable = true;
        if (animator.avatar != null)
            return;

        Avatar avatar = LoadAvatar(kind);
        if (avatar == null)
            return;

        animator.avatar = avatar;
        animator.Rebind();
        animator.Update(0f);
    }

    static Avatar LoadAvatar(AnimalKind kind)
    {
#if UNITY_EDITOR
        string path = "Assets/ithappy/Animals_FREE/Meshes/" + PrefabFileName(kind) + ".fbx";
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Avatar found)
                return found;
        }
#else
        _ = kind;
#endif
        return null;
    }

    static void StripImportedControllers(GameObject visual)
    {
        MonoBehaviour[] behaviours = visual.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().FullName;
            if (typeName == "ithappy.Animals_FREE.MovePlayerInput" ||
                typeName == "ithappy.Animals_FREE.CreatureMover" ||
                typeName == "ithappy.Animals_FREE.PlayerCamera" ||
                typeName == "ithappy.Animals_FREE.ThirdPersonCamera")
            {
                behaviour.enabled = false;
            }
        }

        CharacterController[] controllers = visual.GetComponentsInChildren<CharacterController>(true);
        for (int i = 0; i < controllers.Length; i++)
            controllers[i].enabled = false;

        Camera[] cameras = visual.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
            cameras[i].enabled = false;
    }
}
