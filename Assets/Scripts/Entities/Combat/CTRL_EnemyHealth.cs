using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages enemy HP, damage intake, invulnerability window, and death.
///
/// Damage detection uses Unity's layer system:
///   - Listens on the "PlayerDmg" layer (10) via OnTriggerEnter2D.
///   - If the incoming collider has a Damager component, reads its damage value
///     and triggers its DamagerReaction() so the player gets the stomp bounce-back.
///
/// Also implements IFireDamageable for sustained fire sources (e.g. the player's
/// ground fire trail) — see ApplyBurnTick for how that differs from TakeDamage.
/// </summary>
public class CTRL_EnemyHealth : MonoBehaviour, IFireDamageable
{
    [Header("Runtime State (Read Only)")]
    [SerializeField] private int _currentHP;
    [SerializeField] private bool _isInvulnerable;
    private bool _abilityInvulnerable;   // driven by abilities (e.g. Ability_Dash), independent of hit i-frames

    [Header("Events")]
    public UnityEvent onDamaged = new UnityEvent();
    public UnityEvent onDeath   = new UnityEvent();

    // ── Private references ───────────────────────────────────────────────────
    private SO_EnemyStats _stats;
    private CTRL_EnemyStateMachine _stateMachine;
    private Rigidbody2D _rb;

    // ── Public accessors ─────────────────────────────────────────────────────
    public int  CurrentHP => _currentHP;
    public bool IsDead    => _currentHP <= 0;
    private bool IsInvulnerable => _isInvulnerable || _abilityInvulnerable;

    /// <summary>
    /// Lets an ability (e.g. a dash) grant invulnerability for its own duration,
    /// separate from the post-hit i-frame window so the two never stomp each other.
    /// </summary>
    public void SetAbilityInvulnerable(bool on) => _abilityInvulnerable = on;

    // ── Initialization ───────────────────────────────────────────────────────
    public void Initialize(SO_EnemyStats stats, CTRL_EnemyStateMachine stateMachine, Rigidbody2D rb)
    {
        _stats        = stats;
        _stateMachine = stateMachine;
        _rb           = rb;
        _currentHP    = stats.maxHP;
    }

    // ── Physics callbacks ────────────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsInvulnerable || IsDead) return;

        if (other.gameObject.layer != LayerMask.NameToLayer("PlayerDmg")) return;

        // Read damage amount and trigger stomp reaction on the damager (gives player bounce-back)
        int amount = 1;
        Damager damager = other.GetComponentInParent<Damager>();
        if (damager != null)
        {
            amount = damager.damage;
            damager.DamagerReaction();
        }
        else
        {
            // Player ability hits (combo, charge projectile) — no stomp-style reaction,
            // just damage.
            PlayerAbilityDamager abilityDamager = other.GetComponentInParent<PlayerAbilityDamager>();
            if (abilityDamager != null)
                amount = abilityDamager.damage;
        }

        Vector2 knockbackDir = ((Vector2)transform.position - (Vector2)other.transform.position).normalized;
        TakeDamage(amount, knockbackDir);
    }

    // ── Public damage API ────────────────────────────────────────────────────
    public void TakeDamage(int amount, Vector2 knockbackDir)
    {
        if (IsInvulnerable || IsDead) return;
        if (ReduceHP(amount)) return;   // died

        // Apply knockback impulse
        Vector2 knockback = new Vector2(
            knockbackDir.x * _stats.knockbackForce,
            _stats.knockbackDirection.y * _stats.knockbackForce
        );
        _rb.linearVelocity = knockback;

        _stateMachine.TransitionTo("TakeDamage");
        StartCoroutine(InvulnerabilityWindowCO());
    }

    /// <summary>
    /// IFireDamageable — a damage-over-time tick from a sustained fire source. Unlike
    /// TakeDamage this never applies knockback and never starts the hit-invulnerability
    /// window itself (an existing one is still respected, so fire can't hurt an enemy
    /// mid-hitstun), so it can be called repeatedly without fighting the melee/stomp
    /// hit-reaction tuning.
    /// </summary>
    public void ApplyBurnTick(int amount)
    {
        if (IsInvulnerable || IsDead) return;
        ReduceHP(amount);
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    /// <summary>
    /// Shared HP-reduction path for every damage source: lowers HP, fires onDamaged,
    /// spawns hit particles, and kills the enemy at 0. Returns true if this reduction
    /// killed the enemy, so callers can skip hit-reaction effects (knockback, i-frames)
    /// that don't make sense on a corpse.
    /// </summary>
    private bool ReduceHP(int amount)
    {
        _currentHP -= amount;
        onDamaged?.Invoke();

        if (_stats.hitParticlesPrefab != null)
            Instantiate(_stats.hitParticlesPrefab, transform.position, Quaternion.identity);

        if (_currentHP <= 0)
        {
            Die();
            return true;
        }

        return false;
    }

    private void Die()
    {
        onDeath?.Invoke();
        _stateMachine.TransitionTo("Dead");
        // Short delay so the death animation has time to play
        Destroy(gameObject, 1.5f);
    }

    private IEnumerator InvulnerabilityWindowCO()
    {
        _isInvulnerable = true;
        float startTime = Time.time;
        while (Time.time < startTime + _stats.invulnerabilityDuration)
            yield return null;
        _isInvulnerable = false;
    }
}
