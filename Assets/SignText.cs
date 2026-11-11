using TMPro;
using UnityEngine;

/// <summary>
/// Readable world-space sign lettering. The legacy TextMesh draws on top of everything,
/// so the mirrored text on a sign's back face showed through the front and the two
/// scrambled together. TextMeshPro is depth-tested like normal geometry, stays sharp at
/// any distance, and sizes itself to fit the board.
/// </summary>
public static class SignText
{
    /// <summary>Lettering on one face of a board. Text reads toward the parent's -Z after rotation, like TextMesh.</summary>
    public static TextMeshPro Make(Transform parent, string text, Vector3 localPosition, Quaternion localRotation,
                                   Vector2 size, Color color)
    {
        GameObject go = new GameObject("Sign text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.4f;
        tmp.fontSizeMax = 5f;
        tmp.characterSpacing = 4f;
        tmp.rectTransform.sizeDelta = size;
        return tmp;
    }
}
