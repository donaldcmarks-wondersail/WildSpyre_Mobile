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

    [Header("Impact")]
    [Tooltip("On hitting Ground, places a fire trail stamp on that surface, rotated to the surface " +
             "normal at the impact point. Always uses the ground fire trail's prefab on every surface " +
             "(floor, wall or ceiling) — the wall trail's separately-authored prefab is never used. " +
             "Hitting an enemy never leaves a stamp. Leave off for projectiles that shouldn't burn the level.")]
    [SerializeField] private bool _spawnFireTrailOnImpact = false;

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private Vector2 _travelDir = Vector2.right;
    private FireTrailSlingEmitter _fireTrail;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;

        Destroy(gameObject, _lifetimeSeconds);
    }

    /// <summary>
    /// Called by CTRL_PlayerAbilityController immediately after instantiation. fireTrail is the
    /// player's scene-wired impact stamper; only used if this prefab has Spawn Fire Trail On
    /// Impact enabled (a runtime-spawned prefab can't hold that scene reference itself).
    /// </summary>
    public void Launch(Vector2 direction, float speed, int damage, HitEffects effects, FireTrailSlingEmitter fireTrail = null)
    {
        _travelDir = direction;
        _fireTrail = fireTrail;
        _rb.linearVelocity = direction * speed;

        PlayerAbilityDamager damager = GetComponent<PlayerAbilityDamager>();
        if (damager != null)
        {
            damager.damage = damage;
            damager.effects = effects;
            damager.SetFixedDirection(direction);   // knock enemies along the shot, not away from the player
        }

        // Rotate sprite to face travel direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        bool hitEnemy = other.CompareTag("Enemy");
        bool hitGround = other.gameObject.layer == LayerMask.NameToLayer("Ground");
        if (!hitEnemy && !hitGround) return;

        if (hitGround && _spawnFireTrailOnImpact && _fireTrail != null)
            PlaceImpactFireTrail(other);

        if (_hitParticlesPrefab != null)
            Instantiate(_hitParticlesPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    /// <summary>
    /// The projectile is a trigger, so there's no contact normal to read — probe for the actual
    /// surface instead, in order of reliability:
    ///  1. Raycast forward along the travel direction from well behind the projectile. The reach
    ///     scales with the projectile's own collider size: OnTriggerEnter2D fires when the collider's
    ///     EDGE touches the surface, so the centre can sit a full collider-radius short of it — a
    ///     fixed short ray misses for anything but a tiny projectile. The origin is backed off past
    ///     the collider so it never starts inside the ground (queriesStartInColliders would return a
    ///     meaningless normal for a ray that starts inside).
    ///  2. Collider distance against the ground collider that was hit — gives the closest surface
    ///     point and its outward normal directly, no dependence on the flight path.
    ///  3. Last resort: the projectile's own position, facing back along the flight path.
    /// </summary>
    private void PlaceImpactFireTrail(Collider2D ground)
    {
        int groundMask = 1 << LayerMask.NameToLayer("Ground");
        float reach = _collider.bounds.extents.magnitude + _rb.linearVelocity.magnitude * Time.fixedDeltaTime * 2f + 0.1f;
        Vector2 origin = (Vector2)transform.position - _travelDir * reach;

        RaycastHit2D hit = Physics2D.Raycast(origin, _travelDir, reach * 2f, groundMask);
        if (hit.collider != null)
        {
            _fireTrail.PlaceAlignedStamp(hit.point, hit.normal);
            return;
        }

        // ColliderDistance2D.normal points from pointB (on the ground) toward pointA (on this
        // projectile) — i.e. the ground surface's outward normal, valid whether or not the two overlap.
        ColliderDistance2D distance = _collider.Distance(ground);
        if (distance.isValid)
        {
            _fireTrail.PlaceAlignedStamp(distance.pointB, distance.normal);
            return;
        }

        _fireTrail.PlaceAlignedStamp(transform.position, -_travelDir);
    }
}
