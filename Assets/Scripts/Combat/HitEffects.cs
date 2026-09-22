using UnityEngine;

/// <summary>
/// What a player ability hit does to an enemy beyond damage. Authored per hit / per ability
/// on the player ability ScriptableObjects, carried by PlayerAbilityDamager, and read by
/// CTRL_EnemyHealth when the hit lands.
/// </summary>
[System.Serializable]
public struct HitEffects
{
    [Tooltip("Pushes the enemy on hit. The force is scaled by the enemy's Knockback Magnitude (its weight), " +
             "and is ignored by enemies set to Ignore Knockback or currently protected by an ability's knockback armor.")]
    public bool causesKnockback;
    [Tooltip("Knockback velocity applied at the enemy's default Knockback Magnitude of 1.0.")]
    public float knockbackForce;

    [Tooltip("Cancels whatever ability the enemy is using and stops it using any ability for Interrupt Duration " +
             "seconds. Ignored while the enemy is protected by an ability's interrupt armor.")]
    public bool causesInterrupt;
    [Tooltip("Seconds the interrupted enemy is locked out of using abilities.")]
    public float interruptDuration;

    [Tooltip("Freezes the whole game for this many real-time seconds when the hit lands, for impact. " +
             "0 = none. ~0.03-0.06 is a light hit (2-4 frames), ~0.08-0.12 a heavy one. Skipped while " +
             "another effect (the sling slow-motion) already has the time scale.")]
    public float hitStopDuration;
    [Tooltip("Camera shake amplitude in world units when the hit lands. 0 = none. ~0.05 light, ~0.2 heavy.")]
    public float shakeStrength;
    [Tooltip("How long the camera shake lasts, in real-time seconds.")]
    public float shakeDuration;

    /// <summary>Starting values for a freshly created field — toggles stay off, variables get usable numbers.</summary>
    public static HitEffects Default => new HitEffects
    {
        knockbackForce = 5f,
        interruptDuration = 0.75f,
        hitStopDuration = 0.04f,
        shakeStrength = 0.05f,
        shakeDuration = 0.15f
    };
}
