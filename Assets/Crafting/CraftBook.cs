using Unity.Profiling;
using UnityEngine;

namespace HuntingGame.Crafting
{
    public class CraftBook : MonoBehaviour, ICraftBook
    {
        static readonly ProfilerMarker CookMarker = new ProfilerMarker("HuntingGame.Crafting");

        public static CraftBook Current { get; private set; }

        [SerializeField] CraftSettings settings;

        int cookRawMeat = 1;
        int foundationWood = 2;
        int wallWood = 2;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                enabled = false;
                return;
            }

            Current = this;
            if (settings == null)
                settings = ScriptableObject.CreateInstance<CraftSettings>();

            cookRawMeat = settings.cookRawMeat < 1 ? 1 : settings.cookRawMeat;
            foundationWood = settings.foundationWood < 1 ? 1 : settings.foundationWood;
            wallWood = settings.wallWood < 1 ? 1 : settings.wallWood;
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public int WoodCost(int piece)
        {
            return piece == 0 ? foundationWood : wallWood;
        }

        public CraftResult TryCookMeat()
        {
            CookMarker.Begin();
            CraftResult result = Cook();
            CookMarker.End();
            return result;
        }

        CraftResult Cook()
        {
            if (!CastleShop.AtButchery())
            {
                if (Campfire.Instance == null || !Campfire.Instance.isLit)
                    return CraftResult.NeedFire;

                if (!Campfire.PlayerIsSafe())
                    return CraftResult.TooFar;
            }

            if (Inventory.Instance == null)
                return CraftResult.Missing;

            int made = 0;
            for (int i = 0; i < cookRawMeat; i++)
            {
                if (!Inventory.Instance.CookOneMeat())
                    break;
                made++;
            }

            return made > 0 ? CraftResult.Done : CraftResult.Missing;
        }
    }
}
