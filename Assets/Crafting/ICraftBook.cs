namespace HuntingGame.Crafting
{
    public interface ICraftBook
    {
        CraftResult TryCookMeat();
        int WoodCost(int piece);
    }
}
