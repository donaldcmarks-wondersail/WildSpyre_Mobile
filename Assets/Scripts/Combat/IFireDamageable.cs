/// <summary>
/// Implemented by anything that can be hurt by a sustained fire source (currently the
/// player's ground fire trail) rather than a single discrete hit. Kept separate from
/// CTRL_EnemyHealth.TakeDamage so damage-over-time never triggers hit-reaction side
/// effects (knockback, the hit-invulnerability window) meant for one-shot attacks.
/// </summary>
public interface IFireDamageable
{
    /// <summary>
    /// Applies one damage-over-time tick. Implementations should still respect their
    /// own "can't be hurt right now" state (e.g. an existing invulnerability window)
    /// but must not start a new one or apply knockback from this call.
    /// </summary>
    void ApplyBurnTick(int amount);
}
