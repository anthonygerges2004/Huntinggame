using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance;
    public float maxHealth = 100f;
    public float health = 100f;

    void Awake()
    {
        Instance = this;
        health = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health < 0f)
            health = 0f;

        if (health <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        health += amount;
        if (health > maxHealth)
            health = maxHealth;
    }

    void Die()
    {
        Debug.Log("You died. Light a fire at night and keep your health up.");
        Time.timeScale = 0f;
    }
}
