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

    public void Begin(int meatAmount)
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

        body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();
        body.mass = 0.35f;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping = 0.55f;
        body.angularDamping = 0.85f;
        body.isKinematic = false;
        body.linearVelocity = transform.forward * 2.2f + Vector3.down * 2f + Random.insideUnitSphere * 0.6f;
        body.angularVelocity = new Vector3(
            Random.Range(-7f, 7f),
            Random.Range(-2.5f, 2.5f),
            Random.Range(-9f, 9f));

        IgnorePlayer(true);
        CacheWings();
        FoldWings(1f);
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
        FoldWings(landed ? 1f : 0.55f + Mathf.Abs(Mathf.Sin(age * 14f)) * 0.45f);

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
            if (Inventory.Instance != null)
                Inventory.Instance.ShowNotice("The bird spoiled.");
            Destroy(gameObject);
            return;
        }

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (!InPickupRange())
            return;

        if (Inventory.Instance == null)
            return;

        Inventory.Instance.AddMeat(meat);
        Inventory.Instance.ShowNotice("+" + meat + " meat  from the bird");
        Destroy(gameObject);
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
        transform.position = ground + Vector3.up * 0.12f;
        transform.rotation = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(transform.forward, Vector3.up).sqrMagnitude > 0.01f
                ? Vector3.ProjectOnPlane(transform.forward, Vector3.up)
                : Vector3.forward,
            Vector3.up);

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
