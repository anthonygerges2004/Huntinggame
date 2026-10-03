using UnityEngine;
using UnityEngine.InputSystem;

public class WoodBuilder : MonoBehaviour
{
    public GameObject woodBlockPrefab;
    public float placeDistance = 4f;
    public float blockSize = 1f;
    public int woodCost = 1;

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.bKey.wasPressedThisFrame)
            return;

        if (woodBlockPrefab == null || Inventory.Instance == null)
            return;

        if (!Inventory.Instance.UseWood(woodCost))
        {
            Debug.Log("Need wood to place a block.");
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 point = Physics.Raycast(ray, out RaycastHit hit, placeDistance)
            ? hit.point + hit.normal * (blockSize * 0.5f)
            : ray.GetPoint(placeDistance);

        float s = blockSize;
        point = new Vector3(
            Mathf.Round(point.x / s) * s,
            Mathf.Round(point.y / s) * s,
            Mathf.Round(point.z / s) * s);

        Instantiate(woodBlockPrefab, point, Quaternion.identity);
    }
}