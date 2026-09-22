using UnityEngine;

/// <summary>What a pickup does when collected. Interpreted by MNGR_PickupManager.CollectPickup —
/// Coin is handled directly (adds to the coin total); the rest are left for other systems
/// (health, ability/buff controllers) to react to via MNGR_PickupManager.onPickupCollected, so
/// this data asset never needs to know who consumes it.</summary>
public enum PickupKind
{
    Coin,
    Health,
    PowerUp,
    Custom
}

/// <summary>Movement pattern a pickup plays while idle, driven by Pickup's IPickupMotion. See
/// PickupMotions.cs for exactly what each one does.</summary>
public enum PickupBehaviorType
{
    Static,
    Move,
    Bounce,
    MoveAndBounce,
    Fly,
    Evade,
    Chase
}

/// <summary>
/// All the tuning for one kind of pickup (a coin, a health orb, a buff...), as a shared asset so
/// new pickup variants are authored as data rather than new C# classes. A Pickup component
/// references one of these; PickupCoin-style subclassing is no longer needed for pickups that
/// only differ by number and kind.
/// </summary>
[CreateAssetMenu(fileName = "NewPickupData", menuName = "WildSpyre/Pickups/Pickup Data")]
public class SO_PickupData : ScriptableObject
{
    [Header("Identity")]
    public PickupKind kind = PickupKind.Coin;
    [Tooltip("Meaning depends on Kind — coins granted, health restored, buff strength, etc.")]
    public int amount = 1;

    [Header("Lifetime")]
    [Tooltip("Seconds an uncollected pickup stays in the world before despawning itself. 0 = never expires.")]
    public float lifetime = 5f;

    [Header("Magnetize")]
    [Tooltip("Default state of this pickup's PickupMagnetize component. Can still be toggled at " +
             "runtime per-instance (e.g. a player 'magnet' ability turning it on for everything nearby).")]
    public bool magnetizeToPlayer = false;
    public float magnetizeRadius = 2.5f;
    [Tooltip("World units/second the pickup closes distance to the player once magnetizing.")]
    public float magnetizePower = 5f;
    [Tooltip("When off, a magnetizing pickup won't pull through anything on Wall Layer Mask.")]
    public bool ignoreWallsForMagnetize = false;
    public LayerMask wallLayerMask;

    [Header("Behavior")]
    public PickupBehaviorType behavior = PickupBehaviorType.Static;

    [Header("Behavior — Move / Fly / Evade / Chase")]
    public float movementSpeed = 1.5f;
    [Tooltip("Move only: seconds to ease from standstill up to Movement Speed.")]
    public float movementDamping = 5f;

    [Header("Behavior — Bounce / Fly")]
    [Tooltip("Bounce: vertical bob height. Fly: orbit radius around the spawn point.")]
    public float bounceAmount = 0.25f;

    [Header("Behavior — Evade")]
    [Tooltip("Player must be within this range for the pickup to flee.")]
    public float evadeRadius = 4f;

    [Header("FX")]
    public ParticleSystem spawnFXPrefab;
    public ParticleSystem pickupFXPrefab;
    public AudioClip pickupSFX;
}
