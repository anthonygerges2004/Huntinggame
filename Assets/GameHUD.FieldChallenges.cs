using TMPro;
using UnityEngine;

public partial class GameHUD
{
    TMP_Text fieldChallengeText;
    void BuildFieldChallenges()
    {
        if (GetComponent<FieldChallenges>() == null) gameObject.AddComponent<FieldChallenges>();
        fieldChallengeText = Text(canvas.transform, "", 16, TextMain, TextAlignmentOptions.TopLeft, false);
        Place(fieldChallengeText.rectTransform, new Vector2(0, 1), new Vector2(24, -145), new Vector2(460, 140), new Vector2(0, 1));
    }
    void UpdateFieldChallenges()
    {
        fieldChallengeText.text = FieldChallenges.Instance != null ? FieldChallenges.Instance.HudStatus : "";
    }
}
