using UnityEngine;

namespace DeerHunt
{
    /// <summary>
    /// Put this on each hitbox collider (head, chest, body, legs).
    /// The weapon multiplies its shot by DamageMultiplier. InstantKill is the head.
    /// </summary>
    public class HitZone : MonoBehaviour
    {
        public enum Zone
        {
            Head,
            Chest,
            Body,
            Legs
        }

        [SerializeField] Zone zone = Zone.Body;
        [SerializeField] float damageMultiplier = 1f;
        [SerializeField] bool instantKill;

        public Zone Kind => zone;
        public float DamageMultiplier => damageMultiplier;
        public bool InstantKill => instantKill;

        public string Label
        {
            get
            {
                switch (zone)
                {
                    case Zone.Head: return "Headshot";
                    case Zone.Chest: return "Heart shot";
                    case Zone.Legs: return "Leg shot";
                    default: return "Body shot";
                }
            }
        }

        public void Configure(Zone kind, float multiplier, bool kill)
        {
            zone = kind;
            damageMultiplier = multiplier;
            instantKill = kill;
        }
    }
}
