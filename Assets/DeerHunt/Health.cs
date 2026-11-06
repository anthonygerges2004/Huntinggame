using System;
using UnityEngine;

namespace DeerHunt
{
    /// <summary>
    /// Hit points for a deer. Damage and death are events so the AI and the HUD
    /// can react without this script knowing about either of them.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;

        public event Action<float, float> OnDamaged;
        public event Action OnDeath;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool Dead { get; private set; }

        void Awake()
        {
            Current = maxHealth;
        }

        public void Damage(float amount)
        {
            if (Dead || amount <= 0f)
                return;

            Current = Mathf.Max(0f, Current - amount);
            OnDamaged?.Invoke(amount, Current);
            if (Current <= 0f)
                Kill();
        }

        public void Kill()
        {
            if (Dead)
                return;

            Dead = true;
            Current = 0f;
            OnDeath?.Invoke();
        }
    }
}
