namespace HuntingGame.Survival
{
    public interface ISurvivalStats
    {
        float Health01 { get; }
        float Hunger01 { get; }
        float Warmth01 { get; }
        void EatMeal();
    }
}
