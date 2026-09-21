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

    /// <summary>Starting values for a freshly created field — toggles stay off, variables get usable numbers.</summary>
    public static HitEffects Default => new HitEffects { knockbackForce = 5f, interruptDuration = 0.75f };
}
