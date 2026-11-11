using UnityEngine;

public enum QuarryKind
{
    None,
    Pig,
    Boar,
    Fox,
    Rabbit,
    Wolf
}

/// <summary>
/// Ground game besides deer and horses: pigs, boar, foxes, rabbits and wolves.
/// </summary>
public struct QuarryProfile
{
    public string objectName;
    public string title;
    public string downed;
    public int meat;
    public float nightDamage;
    public float attackRange;
    public bool nocturnal;
    public bool fieldDress;
    public float xp;
    public float gunSize;
    public float weightKg;
    public float markerHeight;
    public float trackScale;
    public float capsuleRadius;
    public float capsuleHeight;
    public Vector3 capsuleCenter;
    public Color body;
    public Color shade;
    public Color accent;
    public float grazeSpeed;
    public float fleeSpeed;
    public float idleSeconds;
    public float grazeSeconds;
    public float alertSeconds;
    public float fleeSeconds;
    public float grazeRadius;
    public float downwind;
    public float sight;
    public float sightDegrees;
    public float sprintHear;
    public float walkHear;
    public float crouchHear;
    public float smell;
    public float smellDegrees;

    public static QuarryProfile Of(QuarryKind kind)
    {
        switch (kind)
        {
            case QuarryKind.Boar: return Boar();
            case QuarryKind.Fox: return Fox();
            case QuarryKind.Rabbit: return Rabbit();
            case QuarryKind.Wolf: return Wolf();
            default: return Pig();
        }
    }

    static QuarryProfile Pig()
    {
        return new QuarryProfile
        {
            objectName = "WildPig", title = "PIG", downed = "Pig down.",
            meat = 4, nightDamage = 0f, attackRange = 2f, fieldDress = true,
            xp = 35f, gunSize = 0.55f, weightKg = 110f, markerHeight = 0.9f, trackScale = 0.85f,
            capsuleRadius = 0.34f, capsuleHeight = 1.15f, capsuleCenter = new Vector3(0f, 0.36f, 0.02f),
            body = new Color(0.4f, 0.31f, 0.22f), shade = new Color(0.26f, 0.18f, 0.13f),
            accent = new Color(0.55f, 0.44f, 0.34f),
            grazeSpeed = 1.5f, fleeSpeed = 6.2f, idleSeconds = 2.8f, grazeSeconds = 8f,
            alertSeconds = 1.4f, fleeSeconds = 5f, grazeRadius = 5f, downwind = 1.5f,
            sight = 16f, sightDegrees = 70f, sprintHear = 12f, walkHear = 6f, crouchHear = 2f,
            smell = 32f, smellDegrees = 100f
        };
    }

    static QuarryProfile Boar()
    {
        return new QuarryProfile
        {
            objectName = "WildBoar", title = "BOAR", downed = "Boar down.",
            meat = 6, nightDamage = 9f, attackRange = 2.8f, fieldDress = true,
            xp = 55f, gunSize = 0.78f, weightKg = 160f, markerHeight = 1.05f, trackScale = 1.05f,
            capsuleRadius = 0.4f, capsuleHeight = 1.35f, capsuleCenter = new Vector3(0f, 0.42f, 0.04f),
            body = new Color(0.2f, 0.14f, 0.1f), shade = new Color(0.08f, 0.06f, 0.05f),
            accent = new Color(0.86f, 0.82f, 0.68f),
            grazeSpeed = 2.1f, fleeSpeed = 8.5f, idleSeconds = 2f, grazeSeconds = 6f,
            alertSeconds = 0.8f, fleeSeconds = 6f, grazeRadius = 7f, downwind = 1.1f,
            sight = 20f, sightDegrees = 80f, sprintHear = 14f, walkHear = 7f, crouchHear = 2.5f,
            smell = 36f, smellDegrees = 110f
        };
    }

    static QuarryProfile Fox()
    {
        return new QuarryProfile
        {
            objectName = "WildFox", title = "FOX", downed = "Fox down.",
            meat = 2, nightDamage = 0f, attackRange = 2f, fieldDress = false,
            xp = 22f, gunSize = 0.32f, weightKg = 9f, markerHeight = 0.7f, trackScale = 0.5f,
            capsuleRadius = 0.22f, capsuleHeight = 1.05f, capsuleCenter = new Vector3(0f, 0.32f, 0.08f),
            body = new Color(0.82f, 0.42f, 0.16f), shade = new Color(0.45f, 0.22f, 0.1f),
            accent = new Color(0.93f, 0.9f, 0.82f),
            grazeSpeed = 3.2f, fleeSpeed = 11.5f, idleSeconds = 1.6f, grazeSeconds = 5f,
            alertSeconds = 0.7f, fleeSeconds = 5f, grazeRadius = 10f, downwind = 0.85f,
            sight = 36f, sightDegrees = 110f, sprintHear = 16f, walkHear = 8f, crouchHear = 3f,
            smell = 30f, smellDegrees = 90f
        };
    }

    static QuarryProfile Rabbit()
    {
        return new QuarryProfile
        {
            objectName = "WildRabbit", title = "RABBIT", downed = "Rabbit down.",
            meat = 1, nightDamage = 0f, attackRange = 2f, fieldDress = false,
            xp = 12f, gunSize = 0.12f, weightKg = 2.2f, markerHeight = 0.45f, trackScale = 0.35f,
            capsuleRadius = 0.16f, capsuleHeight = 0.5f, capsuleCenter = new Vector3(0f, 0.22f, 0f),
            body = new Color(0.55f, 0.42f, 0.32f), shade = new Color(0.32f, 0.24f, 0.18f),
            accent = new Color(0.92f, 0.9f, 0.84f),
            grazeSpeed = 2.4f, fleeSpeed = 13f, idleSeconds = 0.8f, grazeSeconds = 3f,
            alertSeconds = 0.35f, fleeSeconds = 3.5f, grazeRadius = 8f, downwind = 1f,
            sight = 22f, sightDegrees = 140f, sprintHear = 10f, walkHear = 7f, crouchHear = 3.5f,
            smell = 14f, smellDegrees = 80f
        };
    }

    static QuarryProfile Wolf()
    {
        return new QuarryProfile
        {
            objectName = "WildWolf", title = "WOLF", downed = "Wolf down.",
            meat = 4, nightDamage = 12f, attackRange = 3.4f, nocturnal = true, fieldDress = true,
            xp = 60f, gunSize = 0.6f, weightKg = 45f, markerHeight = 0.95f, trackScale = 0.9f,
            capsuleRadius = 0.28f, capsuleHeight = 1.35f, capsuleCenter = new Vector3(0f, 0.42f, 0.06f),
            body = new Color(0.45f, 0.45f, 0.48f), shade = new Color(0.22f, 0.22f, 0.26f),
            accent = new Color(0.75f, 0.74f, 0.7f),
            grazeSpeed = 2.8f, fleeSpeed = 10.5f, idleSeconds = 2.2f, grazeSeconds = 6f,
            alertSeconds = 0.9f, fleeSeconds = 7f, grazeRadius = 14f, downwind = 0.5f,
            sight = 34f, sightDegrees = 100f, sprintHear = 20f, walkHear = 10f, crouchHear = 4f,
            smell = 40f, smellDegrees = 120f
        };
    }
}
