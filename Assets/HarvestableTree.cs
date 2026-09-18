using UnityEngine;
using UnityEngine.InputSystem;

public class HarvestableTree : MonoBehaviour
{
    public int woodAmount = 3;
    public float chopRange = 3f;

    bool chopped;

    void Start()
    {
        if (name.IndexOf("Legendary", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            gameObject.SetActive(false);
            return;
        }

        transform.position = WorldGround.Snap(transform.position);
    }

    void Update()
    {
        if (chopped || Keyboard.current == null)
            return;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (!Keyboard.current.eKey.wasPressedThisFrame)
            return;

        if (Camera.main == null)
            return;

        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, chopRange))
            return;

        HarvestableTree tree = hit.collider.GetComponentInParent<HarvestableTree>();
        if (tree != this)
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
