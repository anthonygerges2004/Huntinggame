using UnityEngine;

namespace HuntingGame.Wind
{
    public class WindSystem : MonoBehaviour, IWind
    {
        public static WindSystem Current { get; private set; }

        [SerializeField] WindSettings settings;

        Vector3 direction = Vector3.forward;
        float strength = 0.45f;
        float yaw;
        float targetYaw;
        float targetStrength = 0.45f;
        float timer;
        float changeInterval = 18f;
        float turnDegrees = 50f;
        float degreesPerSecond = 8f;
        float strengthPerSecond = 0.08f;
        float minStrength = 0.25f;
        float maxStrength = 1f;

        public Vector3 Direction => direction;
        public float Strength => strength;

        public void SetFixed(Vector3 blowToward, float windStrength)
        {
            blowToward.y = 0f;
            if (blowToward.sqrMagnitude < 0.0001f)
                blowToward = Vector3.forward;
            direction = blowToward.normalized;
            strength = Mathf.Clamp(windStrength, 0.05f, 1.5f);
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            targetYaw = yaw;
            targetStrength = strength;
        }

        void Awake()
        {
            if (Current != null && Current != this)
            {
                enabled = false;
                return;
            }

            Current = this;
            if (settings == null)
                settings = ScriptableObject.CreateInstance<WindSettings>();

            changeInterval = settings.changeInterval < 4f ? 4f : settings.changeInterval;
            turnDegrees = settings.turnDegrees < 5f ? 5f : settings.turnDegrees;
            degreesPerSecond = settings.degreesPerSecond < 1f ? 1f : settings.degreesPerSecond;
            strengthPerSecond = settings.strengthPerSecond < 0.01f ? 0.01f : settings.strengthPerSecond;
            minStrength = Mathf.Clamp(settings.minStrength, 0.05f, 1f);
            maxStrength = Mathf.Clamp(settings.maxStrength, minStrength, 1.5f);
            yaw = Random.Range(0f, 360f);
            targetYaw = yaw;
            strength = (minStrength + maxStrength) * 0.5f;
            targetStrength = strength;
            timer = changeInterval;
            direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = changeInterval;
                targetYaw = yaw + Random.Range(-turnDegrees, turnDegrees);
                targetStrength = Random.Range(minStrength, maxStrength);
            }

            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, degreesPerSecond * Time.deltaTime);
            strength = Mathf.MoveTowards(strength, targetStrength, strengthPerSecond * Time.deltaTime);
            direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        }

        void OnDrawGizmos()
        {
            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction : Vector3.forward;
            Gizmos.color = new Color(0.45f, 0.82f, 1f, 0.95f);
            Vector3 from = transform.position + Vector3.up * 3f;
            Vector3 tip = from + dir * (6f + strength * 10f);
            Gizmos.DrawLine(from, tip);
            Gizmos.DrawLine(tip, tip - dir * 1.4f + Vector3.Cross(Vector3.up, dir) * 0.6f);
            Gizmos.DrawLine(tip, tip - dir * 1.4f - Vector3.Cross(Vector3.up, dir) * 0.6f);
        }
    }
}
