using UnityEngine;

public static class ScreenText
{
    static GUIStyle style;

    public static float Scale => Screen.height / 720f;

    public static void Centered(string text, float y, int size = 26)
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
        }

        style.fontSize = Mathf.RoundToInt(size * Scale);
        float h = style.fontSize * 1.6f;
        Rect rect = new Rect(0f, y - h * 0.5f, Screen.width, h);

        style.normal.textColor = Color.black;
        float o = Mathf.Max(1f, 2f * Scale);
        GUI.Label(new Rect(rect.x - o, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + o, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y - o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y + o, rect.width, rect.height), text, style);

        style.normal.textColor = Color.white;
        GUI.Label(rect, text, style);
    }
}
