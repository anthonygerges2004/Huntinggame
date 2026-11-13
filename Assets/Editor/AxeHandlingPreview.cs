using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Visual QA of the actual visible humanoid and axe; never saves changes to the scene.
[InitializeOnLoad]
public static class AxeHandlingPreview
{
    const string Request = "Temp/axe-handling-preview-request.txt";
    const string Active = "Hunt.AxePreview.Active";
    const string Started = "Hunt.AxePreview.Started";
    const string Folder = "Temp/AxeHandlingPreview";
    static Camera camera;
    static PlayerMove player;
    static Animator avatar;
    static int phase;
    static int stableFrame;
    static bool configured;
    static double nextPoll;
    static readonly string[] Names = { "ready", "windup", "contact", "extraction", "recovered" };
    static readonly Vector3[] Poses = { Vector3.zero, new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 0.35f), new Vector3(1, 1, 1) };

    static AxeHandlingPreview() { EditorApplication.update += Tick; }

    static void Tick()
    {
        if (!SessionState.GetBool(Active, false))
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (!File.Exists(Request) || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(Request);
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/measurements.txt", "Third-person axe visual QA\n");
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Started, (float)EditorApplication.timeSinceStartup);
            nextPoll = 0;
            EditorApplication.EnterPlaymode();
            EditorApplication.isPaused = false;
            return;
        }
        try
        {
            double age = EditorApplication.timeSinceStartup - SessionState.GetFloat(Started, 0);
            if (age > 100) { Finish("FAIL: preview timed out"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (!configured)
            {
                player = UnityEngine.Object.FindAnyObjectByType<PlayerMove>();
                if (age >= nextPoll)
                {
                    SurvivalistBody probe = player != null ? player.GetComponent<SurvivalistBody>() : null;
                    File.AppendAllText(Folder + "/measurements.txt", "Waiting: player=" + (player != null) + ", inventory=" + (Inventory.Instance != null) + ", grip=" + (HandAxeGrip.Instance != null)
                        + ", visible body=" + (probe != null) + ", avatar=" + (probe != null && probe.VisibleAnimator != null) + ", paused=" + EditorApplication.isPaused + "\n");
                    nextPoll = age + 5;
                }
                if (age < 8 || player == null || Inventory.Instance == null || HandAxeGrip.Instance == null) return;
                SurvivalistBody visible = player.GetComponent<SurvivalistBody>();
                if (visible == null || visible.VisibleAnimator == null) return;
                avatar = visible.VisibleAnimator;
                Inventory.Instance.CloseBag();
                Inventory.Instance.SelectHotbar(1, false);
                player.enabled = false;
                PlayerMove.SetGait(false, false, false, 1f);
                if (CameraViewSwitcher.Instance != null) CameraViewSwitcher.Instance.PreferThirdPerson();
                AxeSwing.Instance.StopChopping();
                GamePause.Set(false);
                camera = new GameObject("Axe QA camera").AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.17f, 0.19f, 0.21f);
                camera.fieldOfView = 40;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 150;
                camera.transform.position = player.transform.position + player.transform.right * 2.2f + player.transform.forward * 3f + Vector3.up * 1.6f;
                camera.transform.LookAt(player.transform.position + Vector3.up * 1.1f);
                configured = true;
                phase = 0;
                stableFrame = Time.frameCount + 8;
            }
            if (phase >= Poses.Length) { Finish("PASS: captured five third-person handling poses"); return; }
            PlayerBodyAnimation.SetChopPose(Poses[phase]);
            if (Time.frameCount < stableFrame) return;
            Capture(Names[phase]);
            phase++;
            stableFrame = Time.frameCount + 8;
        }
        catch (Exception e) { Finish("FAIL: " + e); }
    }

    static void Capture(string name)
    {
        RenderTexture rt = new RenderTexture(1100, 1100, 24, RenderTextureFormat.ARGB32);
        Texture2D pixels = new Texture2D(1100, 1100, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else { camera.targetTexture = rt; camera.Render(); camera.targetTexture = null; }
            RenderTexture.active = rt;
            pixels.ReadPixels(new Rect(0, 0, 1100, 1100), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Folder + "/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
        Transform hand = avatar.GetBoneTransform(HumanBodyBones.RightHand);
        HandAxeGrip.TryPalmFrame(hand, avatar.GetBoneTransform(HumanBodyBones.RightIndexProximal),
            avatar.GetBoneTransform(HumanBodyBones.RightMiddleProximal), avatar.GetBoneTransform(HumanBodyBones.RightLittleProximal), out Vector3 palm, out Quaternion frame);
        HandAxeGrip axe = HandAxeGrip.Instance;
        float error = Vector3.Distance(palm, axe.transform.TransformPoint(axe.GripLocalPoint));
        float angle = Quaternion.Angle(frame, axe.transform.rotation);
        File.AppendAllText(Folder + "/measurements.txt", name + ": palm grip error=" + error.ToString("F5") + " m, frame error=" + angle.ToString("F2") + " degrees\n");
    }

    static void Finish(string result)
    {
        Directory.CreateDirectory(Folder);
        File.AppendAllText(Folder + "/measurements.txt", result + "\n");
        SessionState.SetBool(Active, false);
        PlayerBodyAnimation.EndChop();
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        configured = false;
        Debug.Log(result);
    }
}
