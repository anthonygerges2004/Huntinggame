using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeerHunt.EditorTools
{
    /// <summary>
    /// Builds Assets/DeerHunt/DeerHuntPrototype.unity with terrain, trees, player, rifle, and deer spawner.
    /// </summary>
    public static class DeerHuntSetup
    {
        const string ScenePath = "Assets/DeerHunt/DeerHuntPrototype.unity";
        const string TerrainPath = "Assets/DeerHunt/PrototypeTerrain.asset";
        const string DeerPath = "Assets/ithappy/Animals_FREE/Prefabs/Deer_001.prefab";
        const string NavPath = "Assets/DeerHunt/PrototypeNavMesh.asset";
        const string AnimatorPath = "Assets/DeerHunt/DeerPrototype.controller";

        [MenuItem("Deer Hunt/Create Prototype Scene")]
        public static void CreatePrototype()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory("Assets/DeerHunt");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Terrain terrain = CreateTerrain();
            GameObject environment = new GameObject("Environment");
            terrain.transform.SetParent(environment.transform, true);
            GameObject obstacles = new GameObject("Obstacles");
            obstacles.transform.SetParent(environment.transform, false);
            ScatterTrees(terrain, obstacles.transform);
            ScatterGrass(terrain, environment.transform);

            DeerHuntNavMesh nav = environment.AddComponent<DeerHuntNavMesh>();
            SerializedObject navObject = new SerializedObject(nav);
            navObject.FindProperty("obstacles").objectReferenceValue = obstacles.transform;
            navObject.ApplyModifiedPropertiesWithoutUndo();
            BakeNavMesh(nav);

            Vector3 spawn = SurfacePoint(terrain, 80f, 80f) + Vector3.up * 0.2f;
            CreatePlayer(spawn);
            CreateHunt(spawn);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.55f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.14f);

            GameObject sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.86f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Deer hunt prototype",
                "Scene saved to Assets/DeerHunt/DeerHuntPrototype.unity.\nPress Play. WASD to move, Shift sprint, Ctrl crouch, left click fire, right click scope, R reload.",
                "OK");
        }

        static Terrain CreateTerrain()
        {
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath) != null)
                AssetDatabase.DeleteAsset(TerrainPath);

            TerrainData data = new TerrainData();
            data.heightmapResolution = 129;
            data.size = new Vector3(160f, 14f, 160f);
            int resolution = data.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float nx = x / (float)(resolution - 1);
                    float ny = y / (float)(resolution - 1);
                    heights[y, x] = Mathf.PerlinNoise(nx * 3.2f, ny * 3.2f) * 0.07f
                        + Mathf.PerlinNoise(nx * 8f, ny * 8f) * 0.02f;
                }
            }

            data.SetHeights(0, 0, heights);
            data.terrainLayers = new[] { GroundLayer() };
            AssetDatabase.CreateAsset(data, TerrainPath);
            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Ground";
            return go.GetComponent<Terrain>();
        }

        static void ScatterTrees(Terrain terrain, Transform parent)
        {
            Material bark = LitAsset("Assets/DeerHunt/Bark.mat", new Color(0.32f, 0.22f, 0.12f));
            Material leaf = LitAsset("Assets/DeerHunt/Leaf.mat", new Color(0.18f, 0.38f, 0.16f));
            for (int i = 0; i < 22; i++)
            {
                float x = Random.Range(12f, 148f);
                float z = Random.Range(12f, 148f);
                if (Vector2.Distance(new Vector2(x, z), new Vector2(80f, 80f)) < 14f)
                    continue;

                Vector3 point = SurfacePoint(terrain, x, z);
                GameObject tree = new GameObject("Tree");
                tree.transform.SetParent(parent, false);
                tree.transform.position = point;

                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.transform.SetParent(tree.transform, false);
                trunk.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                trunk.transform.localScale = new Vector3(0.5f, 2.2f, 0.5f);
                trunk.GetComponent<Renderer>().sharedMaterial = bark;

                GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.transform.SetParent(tree.transform, false);
                crown.transform.localPosition = new Vector3(0f, 4.6f, 0f);
                crown.transform.localScale = new Vector3(3.2f, 2.4f, 3.2f);
                crown.GetComponent<Renderer>().sharedMaterial = leaf;
                Object.DestroyImmediate(crown.GetComponent<Collider>());
            }
        }

        static void ScatterGrass(Terrain terrain, Transform parent)
        {
            GameObject root = new GameObject("Grass");
            root.transform.SetParent(parent, false);
            Material green = LitAsset("Assets/DeerHunt/Grass.mat", new Color(0.25f, 0.48f, 0.18f));
            for (int i = 0; i < 40; i++)
            {
                float x = Random.Range(8f, 152f);
                float z = Random.Range(8f, 152f);
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(blade.GetComponent<Collider>());
                blade.transform.SetParent(root.transform, false);
                blade.transform.position = SurfacePoint(terrain, x, z) + Vector3.up * 0.35f;
                blade.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
                blade.transform.localScale = new Vector3(0.8f, 0.7f, 1f);
                blade.GetComponent<Renderer>().sharedMaterial = green;
            }
        }

        static void CreatePlayer(Vector3 position)
        {
            GameObject player = new GameObject("Player");
            player.transform.position = position + Vector3.up;
            CharacterController body = player.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.radius = 0.35f;
            player.AddComponent<PlayerController>();
            int ignore = LayerMask.NameToLayer("Ignore Raycast");
            if (ignore >= 0)
                player.layer = ignore;

            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.53f, 0.72f, 0.86f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<WeaponController>();
        }

        static void CreateHunt(Vector3 position)
        {
            GameObject hunt = new GameObject("Hunt");
            hunt.transform.position = position;
            hunt.AddComponent<GameManager>();
            hunt.AddComponent<HitMarkerUI>();
            DeerSpawner spawner = hunt.AddComponent<DeerSpawner>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeerPath);
            if (prefab == null)
            {
                Debug.LogWarning("Deer prefab missing at " + DeerPath + ". Assign DeerSpawner.deerPrefab by hand.");
                return;
            }

            SerializedObject so = new SerializedObject(spawner);
            so.FindProperty("deerPrefab").objectReferenceValue = prefab;
            so.FindProperty("deerAnimator").objectReferenceValue = CreateDeerAnimator();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Vector3 SurfacePoint(Terrain terrain, float x, float z)
        {
            Vector3 world = new Vector3(x, 0f, z);
            float y = terrain.SampleHeight(world) + terrain.transform.position.y;
            return new Vector3(x, y, z);
        }

        static Material Lit(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = color;
            return material;
        }

        static Material LitAsset(string path, Color color)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Material material = Lit(color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static TerrainLayer GroundLayer()
        {
            const string texturePath = "Assets/DeerHunt/Ground.png";
            const string layerPath = "Assets/DeerHunt/GroundLayer.terrainlayer";
            TerrainLayer existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (existing != null)
                return existing;

            Texture2D painted = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0.34f, 0.46f, 0.22f);
            painted.SetPixels(pixels);
            painted.Apply();
            File.WriteAllBytes(texturePath, painted.EncodeToPNG());
            Object.DestroyImmediate(painted);
            AssetDatabase.ImportAsset(texturePath);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            TerrainLayer layer = new TerrainLayer();
            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(10f, 10f);
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        static void BakeNavMesh(DeerHuntNavMesh nav)
        {
            NavMeshData data = nav.BuildData();
            if (data == null)
            {
                Debug.LogWarning("Deer hunt NavMesh bake found no colliders.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath) != null)
                AssetDatabase.DeleteAsset(NavPath);
            AssetDatabase.CreateAsset(data, NavPath);

            SerializedObject navObject = new SerializedObject(nav);
            navObject.FindProperty("baked").objectReferenceValue = data;
            navObject.ApplyModifiedPropertiesWithoutUndo();
        }

        static RuntimeAnimatorController CreateDeerAnimator()
        {
            AnimationClip idle = FindClip("Deer_001_idle");
            AnimationClip walk = FindClip("Deer_001_walk");
            AnimationClip run = FindClip("Deer_001_run");
            if (idle == null || walk == null || run == null)
            {
                Debug.LogWarning("Deer clips were not found. The deer will keep its imported controller.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath) != null)
                AssetDatabase.DeleteAsset(AnimatorPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsAlert", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsHit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            BlendTree tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1.6f);
            tree.AddChild(run, 6.4f);
            AssetDatabase.AddObjectToAsset(tree, controller);

            AnimatorState locomotion = machine.AddState("Locomotion");
            locomotion.motion = tree;
            machine.defaultState = locomotion;

            AnimatorState alert = machine.AddState("Alert");
            alert.motion = idle;
            AnimatorState hit = machine.AddState("Hit");
            hit.motion = idle;
            AnimatorState dead = machine.AddState("Dead");
            dead.motion = idle;
            dead.speed = 0f;

            AnimatorStateTransition toDead = machine.AddAnyStateTransition(dead);
            toDead.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
            toDead.hasExitTime = false;
            toDead.duration = 0.05f;
            toDead.canTransitionToSelf = false;

            AnimatorStateTransition toHit = machine.AddAnyStateTransition(hit);
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "IsHit");
            toHit.hasExitTime = false;
            toHit.duration = 0.05f;
            toHit.canTransitionToSelf = false;

            AnimatorStateTransition hitBack = hit.AddTransition(locomotion);
            hitBack.hasExitTime = true;
            hitBack.exitTime = 0.4f;
            hitBack.duration = 0.1f;

            AnimatorStateTransition toAlert = locomotion.AddTransition(alert);
            toAlert.AddCondition(AnimatorConditionMode.If, 0f, "IsAlert");
            toAlert.hasExitTime = false;
            toAlert.duration = 0.12f;

            AnimatorStateTransition alertBack = alert.AddTransition(locomotion);
            alertBack.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAlert");
            alertBack.hasExitTime = false;
            alertBack.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimationClip FindClip(string clipName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/ithappy/Animals_FREE/Meshes/Deer_001.fbx");
            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip != null && clip.name == clipName)
                    return clip;
            }

            return null;
        }

        static void AddToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == path)
                    return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
