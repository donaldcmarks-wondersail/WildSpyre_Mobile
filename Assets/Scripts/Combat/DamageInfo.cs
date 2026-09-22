using UnityEngine;

/// <summary>
/// Everything CTRL_EnemyHealth needs to resolve one hit: how much damage, and whether it also
/// knocks back / interrupts. Built by CTRL_EnemyHealth from whatever component landed the hit
/// (the stomp Damager, or a PlayerAbilityDamager carrying HitEffects).
/// </summary>
public struct DamageInfo
{
    public int amount;

    public bool applyKnockback;
    /// <summary>Knockback velocity BEFORE the enemy's weight (Knockback Magnitude) is applied.</summary>
    public Vector2 knockback;

    public bool interrupt;
    public float interruptDuration;

    /// <summary>Real-time seconds to freeze the game on impact (0 = none).</summary>
    public float hitStopDuration;
    /// <summary>Camera shake amplitude (world units) and real-time length on impact (0 = none).</summary>
    public float shakeStrength;
    public float shakeDuration;
}
