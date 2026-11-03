using UnityEngine;

namespace HuntingGame.Crafting
{
    [CreateAssetMenu(fileName = "CraftSettings", menuName = "Hunting Game/Craft Settings")]
    public class CraftSettings : ScriptableObject
    {
        public int cookRawMeat = 1;
        public int foundationWood = 2;
        public int wallWood = 2;
    }
}
