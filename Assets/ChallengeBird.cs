using UnityEngine;

public class ChallengeBird : MonoBehaviour
{
    BirdChallenge owner;
    Transform wingLeft;
    Transform wingRight;
    Vector3 flyFrom;
    Vector3 flyTo;
    float flyT = 1f;
    float flyTime = 1.2f;
    float hopTimer;
    float age;
    bool dead;
    Vector3 lookTarget;

    public int PerchIndex { get; private set; }

    public static ChallengeBird Spawn(BirdChallenge owner, int perchIndex, Vector3 perch, Vector3 lookTarget)
    {
        GameObject root = new GameObject("ChallengeBird");
        Vector3 side = Vector3.Cross(Vector3.up, (perch - lookTarget).normalized);
        root.transform.position = perch + Vector3.up * 6f + side * Random.Range(-8f, 8f);

        SphereCollider hitBox = root.AddComponent<SphereCollider>();
        hitBox.isTrigger = true;
        hitBox.radius = 0.55f;
        hitBox.center = new Vector3(0f, 0.15f, 0f);

        ChallengeBird bird = root.AddComponent<ChallengeBird>();
        bird.owner = owner;
        bird.lookTarget = lookTarget;
        BuildVisual(root.transform, out bird.wingLeft, out bird.wingRight);
        bird.FlyTo(perchIndex, perch);
        return bird;
    }

    public static void BuildVisual(Transform parent, out Transform wingLeft, out Transform wingRight)
    {
        BirdVisual.Build(parent, FlyerKind.Sparrow, out wingLeft, out wingRight);
    }

    public void FlyTo(int perchIndex, Vector3 perch)
    {
        PerchIndex = perchIndex;
        flyFrom = transform.position;
        flyTo = perch;
        flyT = 0f;
        flyTime = Mathf.Clamp(Vector3.Distance(flyFrom, flyTo) / 9f, 0.8f, 2.2f);
        hopTimer = Random.Range(4f, 8f);
    }

    void Update()
    {
        if (dead)
            return;

        age += Time.deltaTime;

        if (flyT < 1f)
        {
            flyT = Mathf.Min(1f, flyT + Time.deltaTime / flyTime);
            Vector3 pos = Vector3.Lerp(flyFrom, flyTo, flyT) + Vector3.up * Mathf.Sin(flyT * Mathf.PI) * 2f;
            Face(pos - transform.position);
            transform.position = pos;
            Flap(Mathf.Sin(age * 28f) * 55f);
            return;
        }

        transform.position = flyTo + Vector3.up * (Mathf.Abs(Mathf.Sin(age * 3f)) * 0.04f);
        Face(lookTarget - transform.position);
        Flap(Mathf.Sin(age * 2f) * 4f - 6f);

        hopTimer -= Time.deltaTime;
        if (hopTimer <= 0f && owner != null)
            owner.RequestHop(this);
    }

    void Face(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 8f * Time.deltaTime);
    }

    void Flap(float angle)
    {
        wingLeft.localRotation = Quaternion.Euler(0f, 0f, angle);
        wingRight.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    public void Hit()
    {
        if (dead)
            return;

        dead = true;
        GetComponent<Collider>().enabled = false;
        if (owner != null)
            owner.BirdKilled(this);
        StartCoroutine(Fall());
    }

    System.Collections.IEnumerator Fall()
    {
        Vector3 velocity = Vector3.up * 1.5f;
        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.deltaTime;
            velocity += Physics.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(220f * Time.deltaTime, 0f, 160f * Time.deltaTime);
            yield return null;
        }

        Destroy(gameObject);
    }
}
