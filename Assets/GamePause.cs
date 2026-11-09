using UnityEngine;

/// <summary>
/// Pauses the world: stops time and sound and frees the cursor. Gameplay input is blocked
/// because Inventory.IsOpen reports true while paused, and nearly every input handler checks it.
/// </summary>
public static class GamePause
{
    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlayMode()
    {
        Active = false;
    }

    public static void Set(bool on)
    {
        if (on == Active)
            return;

        // The death screen owns time and the cursor; never pause on top of it.
        if (on && PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        Active = on;
        Time.timeScale = on ? 0f : 1f;
        AudioListener.pause = on;
        if (on)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            GameSettings.Save();
        }
    }
}
