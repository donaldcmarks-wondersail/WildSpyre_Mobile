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
/// </summary>
public class CTRL_EnemyHealth : MonoBehaviour
{
    [Header("Runtime State (Read Only)")]
    [SerializeField] private int _currentHP;
    [SerializeField] private bool _isInvulnerable;

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
        if (_isInvulnerable || IsDead) return;

        if (other.gameObject.layer != LayerMask.NameToLayer("PlayerDmg")) return;

        // Read damage amount and trigger stomp reaction on the damager (gives player bounce-back)
        int amount = 1;
        Damager damager = other.GetComponentInParent<Damager>();
        if (damager != null)
        {
            amount = damager.damage;
            damager.DamagerReaction();
        }

        Vector2 knockbackDir = ((Vector2)transform.position - (Vector2)other.transform.position).normalized;
        TakeDamage(amount, knockbackDir);
    }

    // ── Public damage API ────────────────────────────────────────────────────
    public void TakeDamage(int amount, Vector2 knockbackDir)
    {
        if (_isInvulnerable || IsDead) return;

        _currentHP -= amount;
        onDamaged?.Invoke();

        if (_stats.hitParticlesPrefab != null)
            Instantiate(_stats.hitParticlesPrefab, transform.position, Quaternion.identity);

        if (_currentHP <= 0)
        {
            Die();
            return;
        }

        // Apply knockback impulse
        Vector2 knockback = new Vector2(
            knockbackDir.x * _stats.knockbackForce,
            _stats.knockbackDirection.y * _stats.knockbackForce
        );
        _rb.linearVelocity = knockback;

        _stateMachine.TransitionTo("TakeDamage");
        StartCoroutine(InvulnerabilityWindowCO());
    }

    // ── Private helpers ───────────────────────────────────────────────────────
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
