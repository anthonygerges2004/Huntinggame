using UnityEngine;

public class FallenBird : MonoBehaviour
{
    public float pickupRange = 2.4f;
    public float spoilSeconds = 75f;

    int meat;
    Rigidbody body;
    Transform wingLeft;
    Transform wingRight;
    bool landed;
    float age;
    Vector3 restPos;
    Transform player;

    public void Begin(int meatAmount, Vector3 shotDirection)
    {
        meat = Mathf.Max(1, meatAmount);
        if (Inventory.Instance != null)
            spoilSeconds = Inventory.Instance.meatSpoilSeconds;

        BirdFlight flight = GetComponent<BirdFlight>();
        if (flight != null)
            flight.enabled = false;

        Collider hitBox = GetComponent<Collider>();
        if (hitBox != null)
            hitBox.isTrigger = false;

        float weight = 0.35f;
        if (hitBox != null)
            weight = Mathf.Clamp(hitBox.bounds.size.magnitude * 0.45f, 0.22f, 1.5f);

        Vector3 shot = shotDirection.sqrMagnitude > 0.01f ? shotDirection.normalized : transform.forward;
        body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();
        body.mass = weight;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping = 0.2f;
        body.angularDamping = 0.45f;
        body.isKinematic = false;
        body.linearVelocity = shot * (2.6f / weight) + Vector3.down * (1.6f + weight) + transform.forward * 0.6f;

        Vector3 spin = Vector3.Cross(Vector3.up, shot);
        if (spin.sqrMagnitude < 0.01f)
            spin = transform.right;
        body.angularVelocity = spin.normalized * (5.5f / weight) + transform.up * Random.Range(-1.2f, 1.2f);

        IgnorePlayer(true);
        CacheWings();
        FoldWings(0.2f);
    }

    void CacheWings()
    {
        wingLeft = transform.Find("WingLeft");
        wingRight = transform.Find("WingRight");
        if (wingLeft != null && wingRight != null)
            return;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "WingLeft")
                wingLeft = children[i];
            else if (children[i].name == "WingRight")
                wingRight = children[i];
        }
    }

    void IgnorePlayer(bool ignore)
    {
        Collider mine = GetComponent<Collider>();
        if (mine == null || PlayerHealth.Instance == null)
            return;

        Collider[] playerCols = PlayerHealth.Instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < playerCols.Length; i++)
        {
            if (playerCols[i] != null)
                Physics.IgnoreCollision(mine, playerCols[i], ignore);
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        FoldWings(landed ? 1f : Mathf.Clamp01(0.25f + age * 0.9f));

        if (!landed)
        {
            if (age > 0.7f && NearGround(1.1f))
                Land();
            else if (age > 7f)
                Land();
            return;
        }

        if (age >= spoilSeconds)
        {
            Vector3 pos = transform.position;
            int kept = meat;
            if (Inventory.Instance != null)
                Inventory.Instance.ShowNotice("The bird spoiled into bait.");
            Destroy(gameObject);
            MeatBait.Place(pos, kept);
            return;
        }

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (!InPickupRange())
            return;

        if (Inventory.Instance == null || Inventory.IsOpen)
            return;

        int collected = Inventory.Instance.CollectItem("RawMeat", meat);
        if (collected <= 0) return;
        meat -= collected;
        Inventory.Instance.ShowNotice("+" + collected + " meat  from the bird");
        if (meat <= 0) Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (landed || collision.collider == null)
            return;
        if (collision.collider.GetComponentInParent<PlayerMove>() != null)
            return;
        if (collision.collider.GetComponentInParent<Animal>() != null)
            return;

        if (collision.collider.GetComponentInParent<HarvestableTree>() != null &&
            !NearGround(2.4f))
            return;

        Land();
    }

    void Land()
    {
        if (landed)
            return;

        landed = true;
        Vector3 ground = WorldGround.Snap(transform.position);
        Vector3 flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (flat.sqrMagnitude < 0.01f)
            flat = Vector3.forward;
        flat.Normalize();
        float side = transform.right.y >= 0f ? 78f : -78f;
        transform.rotation = Quaternion.LookRotation(flat, Vector3.up) * Quaternion.Euler(10f, 0f, side);
        transform.position = ground + Vector3.up * 0.08f;

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        restPos = transform.position;
        IgnorePlayer(true);
        FoldWings(1f);

        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice("Bird down  -  go pick it up");
    }

    bool NearGround(float limit)
    {
        Vector3 ground = WorldGround.Snap(transform.position);
        return transform.position.y <= ground.y + limit;
    }

    bool InPickupRange()
    {
        if (player == null)
        {
            PlayerMove move = FindAnyObjectByType<PlayerMove>();
            if (move == null)
                return false;
            player = move.transform;
        }

        Vector3 toPlayer = player.position - restPos;
        float height = Mathf.Abs(toPlayer.y);
        toPlayer.y = 0f;
        return toPlayer.magnitude <= pickupRange && height <= 2.8f;
    }

    void FoldWings(float limp)
    {
        float drop = Mathf.Lerp(-18f, -78f, limp);
        float flutter = landed ? 0f : Mathf.Sin(age * 16f) * 10f * (1f - limp);
        if (wingLeft != null)
            wingLeft.localRotation = Quaternion.Euler(0f, 0f, drop + flutter);
        if (wingRight != null)
            wingRight.localRotation = Quaternion.Euler(0f, 0f, -(drop + flutter));
    }

    void OnGUI()
    {
        if (!landed || !InPickupRange())
            return;
        ScreenText.Centered("Walk over the bird to pick it up", Screen.height * 0.5f + 70f * ScreenText.Scale);
    }
}
