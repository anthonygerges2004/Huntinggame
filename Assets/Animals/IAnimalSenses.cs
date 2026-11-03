using HuntingGame.Animals;

namespace HuntingGame.Senses
{
    public interface IAnimalSenses
    {
        float Interval { get; }
        void Tick(AnimalAiBand band);
    }
}
