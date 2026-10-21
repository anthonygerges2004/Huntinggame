using UnityEngine;

public class MinimapFollow : MonoBehaviour
{

    public Transform target;
    public float height = 80f;
    public bool rotateWithPlayer = true;

    void LateUpdate()
    {
        if(target == null)
        {
            return;
        }
        transform.position = new Vector3(target.position.x, target.position.y + height, target.position.z);

        float yaw = rotateWithPlayer ? target.eulerAngles.y : 0f;
        transform.rotation = Quaternion.Euler(90f, yaw, 0f);
    }
}