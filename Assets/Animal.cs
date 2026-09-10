using UnityEngine;

public class Animal : MonoBehaviour
{
    public int points = 10;

    public void Hit()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(points);

        Destroy(gameObject);
    }
}
