using UnityEngine;

namespace HuntingGame.Tracking
{
    [CreateAssetMenu(fileName = "TrackSettings", menuName = "Hunting Game/Track Settings")]
    public class TrackSettings : ScriptableObject
    {
        public float spacing = 2.2f;
        public float stepInterval = 0.55f;
        public float largeLifetime = 18f;
        public float legendaryLifetime = 28f;
        public float footprintLifetime = 16f;
        public float bloodLifetime = 34f;
        public float branchLifetime = 46f;
        public float deerSize = 0.28f;
        public float largeSize = 0.42f;
        public float legendarySize = 0.62f;
        public float bloodSize = 0.2f;
        public float branchLength = 0.48f;
        public float branchInterval = 1.4f;
        public int bloodTrail = 5;
        public int poolSize = 48;
        public int maxActive = 64;
    }
}
