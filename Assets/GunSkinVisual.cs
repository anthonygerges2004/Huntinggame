using UnityEngine;

public sealed class GunSkinVisual : MonoBehaviour
{
    Renderer[] renderers;
    MaterialPropertyBlock[] original;
    MaterialPropertyBlock block;
    int baseColor;
    int colorId;

    void Awake() => Initialize();

    void Initialize()
    {
        if (block != null) return;
        block = new MaterialPropertyBlock();
        baseColor = Shader.PropertyToID("_BaseColor");
        colorId = Shader.PropertyToID("_Color");
    }

    public void Apply(int skin)
    {
        // Apply can also be called on an inactive gun before Unity invokes Awake.
        Initialize();
        if (renderers == null)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            original = new MaterialPropertyBlock[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                original[i] = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(original[i]);
            }
        }
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (skin == 0) { renderers[i].SetPropertyBlock(original[i]); continue; }
            renderers[i].GetPropertyBlock(block);
            block.SetColor(baseColor, GunSkins.Tint(skin));
            block.SetColor(colorId, GunSkins.Tint(skin));
            renderers[i].SetPropertyBlock(block);
            block.Clear();
        }
    }
}
