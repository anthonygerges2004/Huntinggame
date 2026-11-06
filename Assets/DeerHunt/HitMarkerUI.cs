using System.Collections.Generic;
using UnityEngine;

namespace DeerHunt
{
    /// <summary>
    /// Crosshair, hit flash (white = hit, red = kill), and the floating zone name.
    /// </summary>
    public class HitMarkerUI : MonoBehaviour
    {
        public static HitMarkerUI Instance { get; private set; }

        struct Floater
        {
            public string text;
            public Vector3 world;
            public float until;
        }

        [SerializeField] float markerSeconds = 0.18f;
        [SerializeField] float floatSeconds = 1.3f;

        readonly List<Floater> floaters = new List<Floater>();
        float markerUntil;
        bool markerKill;
        GUIStyle label;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void Mark(bool kill, string zoneLabel, Vector3 worldPoint, float distance)
        {
            if (Instance == null)
                return;

            Instance.markerKill = kill;
            Instance.markerUntil = Time.time + Instance.markerSeconds;
            Instance.floaters.Add(new Floater
            {
                text = zoneLabel + "  " + Mathf.RoundToInt(distance) + " m",
                world = worldPoint + Vector3.up * 0.4f,
                until = Time.time + Instance.floatSeconds
            });
        }

        void OnGUI()
        {
            EnsureStyle();
            DrawCrosshair();
            DrawMarker();
            DrawFloaters();
        }

        void DrawCrosshair()
        {
            WeaponController weapon = FindAnyObjectByType<WeaponController>();
            if (weapon != null && weapon.IsZoomed)
                return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(cx - 10f, cy - 1f, 6f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4f, cy - 1f, 6f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 10f, 2f, 6f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy + 4f, 2f, 6f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawMarker()
        {
            if (Time.time > markerUntil)
                return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color prev = GUI.color;
            GUI.color = markerKill ? new Color(0.85f, 0.12f, 0.1f, 0.95f) : new Color(1f, 1f, 1f, 0.95f);
            const float s = 14f;
            GUI.DrawTexture(new Rect(cx - 22f, cy - 22f, s, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 22f, cy - 22f, 3f, s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8f, cy - 22f, s, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 19f, cy - 22f, 3f, s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 22f, cy + 19f, s, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 22f, cy + 8f, 3f, s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8f, cy + 19f, s, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 19f, cy + 8f, 3f, s), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawFloaters()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                if (Time.time > floaters[i].until)
                {
                    floaters.RemoveAt(i);
                    continue;
                }

                Vector3 screen = cam.WorldToScreenPoint(floaters[i].world);
                if (screen.z < 0f)
                    continue;

                Rect rect = new Rect(screen.x - 80f, Screen.height - screen.y - 18f, 160f, 28f);
                GUI.Label(rect, floaters[i].text, label);
            }
        }

        void EnsureStyle()
        {
            if (label != null)
                return;

            label = new GUIStyle(GUI.skin.label);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 18;
            label.fontStyle = FontStyle.Bold;
            label.normal.textColor = new Color(1f, 0.92f, 0.75f);
        }
    }
}
