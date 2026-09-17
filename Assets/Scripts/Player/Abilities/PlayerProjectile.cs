using UnityEngine;

/// <summary>
/// Physics-based player projectile (the charge ability's Fireball). Launched by
/// CTRL_PlayerAbilityController. This script owns movement, lifetime, and hit/expiry
/// cleanup only — damage is dealt via a PlayerAbilityDamager component on the same
/// GameObject (set to Start Active, since this hitbox is live the instant it spawns),
/// mirroring how CTRL_EnemyProjectile is shaped for the enemy side.
///
/// Layer: PlayerDmg — CTRL_EnemyHealth.OnTriggerEnter2D already reads that layer plus
/// a PlayerAbilityDamager for the damage amount, so no manual damage call is needed here.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float _lifetimeSeconds = 5f;
    [SerializeField] private GameObject _hitParticlesPrefab;

    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        GetComponent<Collider2D>().isTrigger = true;

        Destroy(gameObject, _lifetimeSeconds);
    }

    /// <summary>Called by CTRL_PlayerAbilityController immediately after instantiation.</summary>
    public void Launch(Vector2 direction, float speed, int damage)
    {
        _rb.linearVelocity = direction * speed;

        PlayerAbilityDamager damager = GetComponent<PlayerAbilityDamager>();
        if (damager != null)
            damager.damage = damage;

        // Rotate sprite to face travel direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        bool hitEnemy = other.CompareTag("Enemy");
        bool hitGround = other.gameObject.layer == LayerMask.NameToLayer("Ground");
        if (!hitEnemy && !hitGround) return;

        if (_hitParticlesPrefab != null)
            Instantiate(_hitParticlesPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
