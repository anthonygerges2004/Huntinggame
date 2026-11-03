using UnityEngine;

namespace HuntingGame.Animals
{
    public interface IAnimalBrain
    {
        AnimalAiState State { get; }
        SpeciesData Species { get; }
        void Bind();
        void Notice(Vector3 threat, bool urgent);
        void Tick(float delta);
    }
}
