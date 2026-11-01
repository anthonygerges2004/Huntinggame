using UnityEngine;

public static class PlayerView
{
    public static Transform Body()
    {
        if (PlayerHealth.Instance != null)
            return PlayerHealth.Instance.transform;
        if (CameraViewSwitcher.Instance != null)
            return CameraViewSwitcher.Instance.transform;
        return null;
    }

    public static Vector3 Position()
    {
        Transform body = Body();
        return body != null ? body.position : Vector3.zero;
    }

    public static bool IsFirstPerson()
    {
        return CameraViewSwitcher.Instance != null && CameraViewSwitcher.Instance.IsFirstPerson;
    }

    public static Vector3 AimOrigin()
    {
        if (IsFirstPerson())
        {
            Camera cam = Camera.main;
            if (cam != null)
                return cam.transform.position;
        }

        Transform body = Body();
        if (body != null)
            return body.position + Vector3.up * 1.5f;

        Camera fallback = Camera.main;
        return fallback != null ? fallback.transform.position : Vector3.zero;
    }

    public static Vector3 AimDirection()
    {
        if (IsFirstPerson())
        {
            Camera cam = Camera.main;
            if (cam != null)
                return cam.transform.forward;
        }

        Transform body = Body();
        if (body == null)
        {
            Camera cam = Camera.main;
            return cam != null ? cam.transform.forward : Vector3.forward;
        }

        float pitch = 0f;
        PlayerMove move = body.GetComponent<PlayerMove>();
        if (move != null)
            pitch = move.LookPitch;

        return Quaternion.Euler(pitch, body.eulerAngles.y, 0f) * Vector3.forward;
    }

    public static Ray AimRay()
    {
        return new Ray(AimOrigin(), AimDirection());
    }
}
