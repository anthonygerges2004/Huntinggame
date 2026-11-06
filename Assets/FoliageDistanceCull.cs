using UnityEngine;

/// <summary>
/// Turns chop trees back on. Hiding them made them pop.
/// The terrain already stops drawing its own trees past the tree distance.
/// </summary>
public class FoliageDistanceCull : MonoBehaviour
{
    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        HarvestableTree[] trees = Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Include);
        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] == null)
                continue;

            Renderer[] renderers = trees[i].GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] != null)
                    renderers[r].enabled = true;
            }
        }

        enabled = false;
    }
}
