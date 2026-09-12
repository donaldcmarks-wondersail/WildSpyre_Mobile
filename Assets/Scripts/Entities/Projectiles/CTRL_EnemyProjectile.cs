using UnityEngine;

/// <summary>
/// Physics-based enemy projectile. Launched by Ability_ThrowProjectile.
///
/// Damage is handled automatically by the existing player system:
///   - This GameObject is tagged "DamagePlayer" (set in Awake).
///   - MNGR_PlayerLife.OnTriggerEnter2D already listens for this tag.
///   - No manual playerLoseLife() call needed.
///
/// Layer: EnemyDmg (9) — ensure the Physics2D collision matrix allows
/// EnemyDmg to interact with PlayerCol (6) in Project Settings.
///
/// Self-destructs on contact with any solid object or after lifetimeSeconds.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class CTRL_EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float _lifetimeSeconds = 5f;
    [SerializeField] private GameObject _hitParticlesPrefab;

    private Rigidbody2D _rb;

    private void Awake()
    {
        gameObject.tag = "DamagePlayer";

        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        GetComponent<Collider2D>().isTrigger = true;

        Destroy(gameObject, _lifetimeSeconds);
    }

    /// <summary>Called by Ability_ThrowProjectile immediately after instantiation.</summary>
    public void Launch(Vector2 direction, float speed)
    {
        _rb.linearVelocity = direction * speed;

        // Rotate sprite to face travel direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore other projectiles and enemies
        if (other.CompareTag("DamagePlayer") || other.CompareTag("Enemy")) return;

        if (_hitParticlesPrefab != null)
            Instantiate(_hitParticlesPrefab, transform.position, Quaternion.identity);

        // Player damage is handled by MNGR_PlayerLife detecting "DamagePlayer" tag
        Destroy(gameObject);
    }
}
