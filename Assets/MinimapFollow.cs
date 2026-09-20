using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    public Transform player;
    void LateUpdate()
    {
        if(player == null)
        {
            return;
        }
        Vector3 pos = player.position;
        pos.y = transform.position.y;
        transform.position = pos;
    }

    }