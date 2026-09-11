using UnityEngine;
using UnityEngine.InputSystem;

public class HarvestableTree : MonoBehaviour
{
    public int woodAmount = 3;
    public float chopRange = 3f;

    bool chopped;

    void Update()
    {
        if (chopped || Keyboard.current == null)
            return;

        if (!Keyboard.current.eKey.wasPressedThisFrame)
            return;

        if (Camera.main == null)
            return;

        if (Vector3.Distance(transform.position, Camera.main.transform.position) > chopRange)
            return;

        if (Inventory.Instance == null || !Inventory.Instance.hasPickaxe)
        {
            Debug.Log("You need a pickaxe to chop wood.");
            return;
        }

        Inventory.Instance.AddWood(woodAmount);
        chopped = true;
        Destroy(gameObject);
    }
}
