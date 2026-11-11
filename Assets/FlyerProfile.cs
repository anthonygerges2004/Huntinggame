using UnityEngine;

public enum BirdShape
{
    Song,
    Water,
    Owl,
    LongTail
}

/// <summary>
/// Look, flight and hunt values for one bird. Sparrow, crow, duck and hawk stay;
/// thrush, robin, jay, woodpecker, pigeon, pheasant, goose and owl are the rest of the woods.
/// </summary>
public struct FlyerProfile
{
    public string objectName;
    public string title;
    public BirdShape shape;
    public float scale;
    public float collider;
    public float wingSpan;
    public float tail;
    public float beakLength;
    public int breastSpots;
    public bool crest;
    public bool redCap;
    public bool neckBand;
    public Color body;
    public Color wing;
    public Color beak;
    public Color accent;
    public float cruiseMin;
    public float cruiseMax;
    public float cruiseSpeed;
    public float fleeSpeed;
    public float wander;
    public float perchChance;
    public float flapRate;
    public float flapAmount;
    public float spawnHeight;
    public int meat;
    public float xp;
    public float gunSize;
    public bool water;
    public bool nocturnal;

    public static FlyerProfile Of(FlyerKind kind)
    {
        switch (kind)
        {
            case FlyerKind.Crow: return Crow();
            case FlyerKind.Duck: return Duck();
            case FlyerKind.Hawk: return Hawk();
            case FlyerKind.Thrush: return Thrush();
            case FlyerKind.Robin: return Robin();
            case FlyerKind.Jay: return Jay();
            case FlyerKind.Woodpecker: return Woodpecker();
            case FlyerKind.Pigeon: return Pigeon();
            case FlyerKind.Pheasant: return Pheasant();
            case FlyerKind.Goose: return Goose();
            case FlyerKind.Owl: return Owl();
            default: return Sparrow();
        }
    }

    static FlyerProfile Sparrow()
    {
        return new FlyerProfile
        {
            objectName = "WildSparrow", title = "SPARROW", shape = BirdShape.Song,
            scale = 1f, collider = 0.3f, wingSpan = 1f, tail = 0.3f, beakLength = 0.14f,
            breastSpots = 1,
            body = new Color(0.46f, 0.36f, 0.24f), wing = new Color(0.28f, 0.2f, 0.12f),
            beak = new Color(0.42f, 0.3f, 0.18f), accent = new Color(0.1f, 0.08f, 0.08f),
            cruiseMin = 5f, cruiseMax = 11f, cruiseSpeed = 6.5f, fleeSpeed = 13f, wander = 34f,
            perchChance = 0.7f, flapRate = 22f, flapAmount = 50f, spawnHeight = 8f,
            meat = 1, xp = 8f, gunSize = 0f
        };
    }

    static FlyerProfile Crow()
    {
        return new FlyerProfile
        {
            objectName = "WildCrow", title = "CROW", shape = BirdShape.Song,
            scale = 1.2f, collider = 0.36f, wingSpan = 1.15f, tail = 0.34f, beakLength = 0.16f,
            body = new Color(0.12f, 0.12f, 0.14f), wing = new Color(0.08f, 0.08f, 0.1f),
            beak = new Color(0.2f, 0.2f, 0.22f), accent = new Color(0.12f, 0.12f, 0.14f),
            cruiseMin = 8f, cruiseMax = 16f, cruiseSpeed = 7.5f, fleeSpeed = 14f, wander = 48f,
            perchChance = 0.62f, flapRate = 16f, flapAmount = 42f, spawnHeight = 12f,
            meat = 1, xp = 12f, gunSize = 0.08f
        };
    }

    static FlyerProfile Duck()
    {
        return new FlyerProfile
        {
            objectName = "WildDuck", title = "DUCK", shape = BirdShape.Water,
            scale = 1.35f, collider = 0.42f, wingSpan = 1.2f, tail = 0.28f, beakLength = 0.2f,
            body = new Color(0.5f, 0.53f, 0.55f), wing = new Color(0.32f, 0.28f, 0.22f),
            beak = new Color(0.93f, 0.75f, 0.12f), accent = new Color(0.05f, 0.32f, 0.16f),
            cruiseMin = 2.2f, cruiseMax = 6.2f, cruiseSpeed = 6f, fleeSpeed = 12f, wander = 26f,
            perchChance = 0.12f, flapRate = 14f, flapAmount = 36f, spawnHeight = 4f,
            meat = 2, xp = 14f, gunSize = 0.15f, water = true
        };
    }

    static FlyerProfile Hawk()
    {
        return new FlyerProfile
        {
            objectName = "WildHawk", title = "HAWK", shape = BirdShape.Song,
            scale = 1.7f, collider = 0.55f, wingSpan = 1.55f, tail = 0.42f, beakLength = 0.16f,
            body = new Color(0.4f, 0.28f, 0.16f), wing = new Color(0.26f, 0.18f, 0.11f),
            beak = new Color(0.85f, 0.68f, 0.22f), accent = new Color(0.84f, 0.76f, 0.58f),
            cruiseMin = 16f, cruiseMax = 28f, cruiseSpeed = 9f, fleeSpeed = 16f, wander = 78f,
            perchChance = 0.22f, flapRate = 10f, flapAmount = 28f, spawnHeight = 20f,
            meat = 3, xp = 22f, gunSize = 0.18f
        };
    }

    static FlyerProfile Thrush()
    {
        return new FlyerProfile
        {
            objectName = "WildThrush", title = "THRUSH", shape = BirdShape.Song,
            scale = 1.05f, collider = 0.32f, wingSpan = 1.02f, tail = 0.32f, beakLength = 0.13f,
            breastSpots = 3,
            body = new Color(0.4f, 0.26f, 0.14f), wing = new Color(0.32f, 0.2f, 0.11f),
            beak = new Color(0.62f, 0.42f, 0.18f), accent = new Color(0.16f, 0.09f, 0.05f),
            cruiseMin = 4.2f, cruiseMax = 9.5f, cruiseSpeed = 6.2f, fleeSpeed = 12.5f, wander = 30f,
            perchChance = 0.78f, flapRate = 20f, flapAmount = 46f, spawnHeight = 7f,
            meat = 1, xp = 10f, gunSize = 0.02f
        };
    }

    static FlyerProfile Robin()
    {
        return new FlyerProfile
        {
            objectName = "WildRobin", title = "ROBIN", shape = BirdShape.Song,
            scale = 0.92f, collider = 0.28f, wingSpan = 0.96f, tail = 0.28f, beakLength = 0.12f,
            breastSpots = 1,
            body = new Color(0.32f, 0.24f, 0.16f), wing = new Color(0.22f, 0.16f, 0.11f),
            beak = new Color(0.55f, 0.32f, 0.16f), accent = new Color(0.78f, 0.22f, 0.12f),
            cruiseMin = 3.2f, cruiseMax = 7.5f, cruiseSpeed = 6f, fleeSpeed = 12f, wander = 20f,
            perchChance = 0.82f, flapRate = 24f, flapAmount = 48f, spawnHeight = 5.5f,
            meat = 1, xp = 8f, gunSize = 0f
        };
    }

    static FlyerProfile Jay()
    {
        return new FlyerProfile
        {
            objectName = "WildJay", title = "JAY", shape = BirdShape.Song,
            scale = 1.18f, collider = 0.36f, wingSpan = 1.12f, tail = 0.4f, beakLength = 0.15f,
            body = new Color(0.72f, 0.5f, 0.46f), wing = new Color(0.12f, 0.38f, 0.78f),
            beak = new Color(0.08f, 0.07f, 0.07f), accent = new Color(0.93f, 0.93f, 0.9f),
            cruiseMin = 6f, cruiseMax = 13f, cruiseSpeed = 7.4f, fleeSpeed = 14f, wander = 42f,
            perchChance = 0.48f, flapRate = 16f, flapAmount = 40f, spawnHeight = 9f,
            meat = 1, xp = 12f, gunSize = 0.05f
        };
    }

    static FlyerProfile Woodpecker()
    {
        return new FlyerProfile
        {
            objectName = "WildWoodpecker", title = "WOODPECKER", shape = BirdShape.Song,
            scale = 1.02f, collider = 0.32f, wingSpan = 1f, tail = 0.36f, beakLength = 0.32f,
            redCap = true,
            body = new Color(0.08f, 0.08f, 0.09f), wing = new Color(0.82f, 0.82f, 0.78f),
            beak = new Color(0.55f, 0.48f, 0.32f), accent = new Color(0.75f, 0.12f, 0.1f),
            cruiseMin = 3.5f, cruiseMax = 8f, cruiseSpeed = 6.6f, fleeSpeed = 13f, wander = 16f,
            perchChance = 0.9f, flapRate = 26f, flapAmount = 36f, spawnHeight = 6f,
            meat = 1, xp = 12f, gunSize = 0.05f
        };
    }

    static FlyerProfile Pigeon()
    {
        return new FlyerProfile
        {
            objectName = "WildPigeon", title = "PIGEON", shape = BirdShape.Song,
            scale = 1.2f, collider = 0.38f, wingSpan = 1.15f, tail = 0.32f, beakLength = 0.12f,
            neckBand = true,
            body = new Color(0.45f, 0.46f, 0.48f), wing = new Color(0.32f, 0.33f, 0.36f),
            beak = new Color(0.55f, 0.32f, 0.28f), accent = new Color(0.25f, 0.55f, 0.42f),
            cruiseMin = 8f, cruiseMax = 15f, cruiseSpeed = 8.2f, fleeSpeed = 14f, wander = 52f,
            perchChance = 0.35f, flapRate = 14f, flapAmount = 40f, spawnHeight = 11f,
            meat = 1, xp = 10f, gunSize = 0.08f
        };
    }

    static FlyerProfile Pheasant()
    {
        return new FlyerProfile
        {
            objectName = "WildPheasant", title = "PHEASANT", shape = BirdShape.LongTail,
            scale = 1.4f, collider = 0.48f, wingSpan = 1.05f, tail = 0.85f, beakLength = 0.14f,
            body = new Color(0.62f, 0.34f, 0.12f), wing = new Color(0.36f, 0.22f, 0.1f),
            beak = new Color(0.72f, 0.56f, 0.26f), accent = new Color(0.06f, 0.16f, 0.08f),
            cruiseMin = 1.4f, cruiseMax = 3.8f, cruiseSpeed = 8.5f, fleeSpeed = 15f, wander = 18f,
            perchChance = 0.18f, flapRate = 18f, flapAmount = 32f, spawnHeight = 2.2f,
            meat = 2, xp = 18f, gunSize = 0.22f
        };
    }

    static FlyerProfile Goose()
    {
        return new FlyerProfile
        {
            objectName = "WildGoose", title = "GOOSE", shape = BirdShape.Water,
            scale = 1.65f, collider = 0.58f, wingSpan = 1.45f, tail = 0.34f, beakLength = 0.22f,
            body = new Color(0.5f, 0.46f, 0.38f), wing = new Color(0.3f, 0.28f, 0.24f),
            beak = new Color(0.9f, 0.45f, 0.12f), accent = new Color(0.72f, 0.68f, 0.58f),
            cruiseMin = 3f, cruiseMax = 7.5f, cruiseSpeed = 7.2f, fleeSpeed = 13f, wander = 36f,
            perchChance = 0.1f, flapRate = 12f, flapAmount = 34f, spawnHeight = 5f,
            meat = 3, xp = 20f, gunSize = 0.28f, water = true
        };
    }

    static FlyerProfile Owl()
    {
        return new FlyerProfile
        {
            objectName = "WildOwl", title = "OWL", shape = BirdShape.Owl,
            scale = 1.35f, collider = 0.46f, wingSpan = 1.4f, tail = 0.2f, beakLength = 0.1f,
            body = new Color(0.48f, 0.4f, 0.28f), wing = new Color(0.32f, 0.26f, 0.18f),
            beak = new Color(0.55f, 0.4f, 0.16f), accent = new Color(0.92f, 0.88f, 0.7f),
            cruiseMin = 7f, cruiseMax = 15f, cruiseSpeed = 5.4f, fleeSpeed = 11f, wander = 40f,
            perchChance = 0.72f, flapRate = 8f, flapAmount = 26f, spawnHeight = 10f,
            meat = 2, xp = 24f, gunSize = 0.16f, nocturnal = true
        };
    }
}
