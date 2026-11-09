using HuntingGame.Wind;
using UnityEngine;

/// <summary>
/// Shows wind direction/strength and how scent behaves. Stronger hints near a lit campfire.
/// </summary>
public class ScentWindForecast : MonoBehaviour
{
    public float campHintRadius = 14f;

    static GUIStyle labelStyle;

    void OnGUI()
    {
        // The HUD now shows wind all the time next to the compass. This old box sat on top of
        // the vitals panel, so it only draws when there is no HUD.
        if (GameHUD.Instance != null)
            return;

        if (Inventory.IsOpen || BirdChallenge.Active)
            return;

        if (Campfire.Instance == null || !Campfire.Instance.isLit)
            return;

        if (Vector3.Distance(PlayerView.Position(), CampfireFirePosition()) > campHintRadius)
            return;

        Vector3 wind = WindSystem.Current != null ? WindSystem.Current.Direction : Vector3.forward;
        wind.y = 0f;
        if (wind.sqrMagnitude < 0.0001f)
            wind = Vector3.forward;
        wind.Normalize();

        float strength = WindSystem.Current != null ? WindSystem.Current.Strength : 0.5f;

        EnsureStyle();
        float scale = ScreenText.Scale;
        float pad = 16f * scale;
        float w = 210f * scale;
        float h = 56f * scale;
        Rect box = new Rect(pad, Screen.height - h - pad, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;

        string dir = WindLabel(wind);
        string line1 = "Wind " + dir + "  (" + Mathf.RoundToInt(strength * 100f) + "%)";
        string line2 = "Scent drifts with smoke — stalk downwind.";

        GUI.Label(new Rect(box.x + 10f * scale, box.y + 8f * scale, box.width - 20f * scale, 22f * scale), line1, labelStyle);
        GUI.Label(new Rect(box.x + 10f * scale, box.y + 30f * scale, box.width - 20f * scale, 22f * scale), line2, labelStyle);
    }

    static Vector3 CampfireFirePosition()
    {
        return Campfire.Instance != null ? Campfire.Instance.FireWorldPosition() : Vector3.zero;
    }

    static string WindLabel(Vector3 wind)
    {
        float yaw = Mathf.Atan2(wind.x, wind.z) * Mathf.Rad2Deg;
        if (yaw < 0f)
            yaw += 360f;
        if (yaw >= 337.5f || yaw < 22.5f) return "N";
        if (yaw < 67.5f) return "NE";
        if (yaw < 112.5f) return "E";
        if (yaw < 157.5f) return "SE";
        if (yaw < 202.5f) return "S";
        if (yaw < 247.5f) return "SW";
        if (yaw < 292.5f) return "W";
        return "NW";
    }

    static void EnsureStyle()
    {
        if (labelStyle != null)
            return;
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            normal = { textColor = Color.white },
            wordWrap = true
        };
    }
}
