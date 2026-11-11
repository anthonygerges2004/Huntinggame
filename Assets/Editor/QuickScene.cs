using UnityEditor;
using UnityEngine;

namespace HuntingGame.EditorTools
{
    [CreateAssetMenu(fileName = "QuickScenes", menuName = "Hunting Game/Quick Scene Shortcuts")]
    public sealed class QuickScene : ScriptableObject
    {
        [Tooltip("Drag scene assets here. Shortcuts survive scene moves and renames.")]
        public SceneAsset[] scenes = new SceneAsset[0];
    }
}