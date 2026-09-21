using UnityEngine;

/// <summary>
/// Lightweight damage-dealing hitbox for player abilities (combo hits, the charge
/// projectile) — deals damage to enemies with no side effects on the player, unlike
/// Damager (used for the stomp), whose DamagerReaction() always gives the player an
/// upward bounce. Read directly by CTRL_EnemyHealth.OnTriggerEnter2D via layer + this
/// component type, independent of the Damager pathway.
///
/// Never enables or disables anything itself: whether the hitbox is live is decided purely
/// by whatever switches its GameObject/collider on and off (the combo animations, or simply
/// being an active projectile prefab). This component only carries data and reacts to contact.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerAbilityDamager : MonoBehaviour
{
    public int damage = 1;

    /// <summary>Knockback / interrupt this hit applies. Set per hit by CTRL_PlayerAbilityController.</summary>
    public HitEffects effects;

    [Header("Impact FX")]
    [Tooltip("Particle prefabs spawned where this hitbox touches an enemy — one picked at random from the list " +
             "per hit, once per enemy per swing (or per projectile), at the contact point on the enemy. Empty " +
             "entries are skipped. Give each prefab a Stop Action of Destroy, or it's cleaned up automatically " +
             "after its longest particle finishes. Leave the list empty for none. Separate from " +
             "PlayerProjectile's own Hit Particles, which spawn at the projectile on enemies AND ground — " +
             "use only one of them or enemy hits will show both.")]
    [SerializeField] private GameObject[] _hitParticlePrefabs;

    private Collider2D _collider;
    private bool _hasFixedDirection;
    private Vector2 _fixedDirection;

    // Enemies already given an impact particle since this hitbox was last enabled. An enemy can present
    // several trigger colliders (root collider plus its body-contact hitbox), each firing its own
    // OnTriggerEnter2D for the same swing.
    private readonly System.Collections.Generic.HashSet<CTRL_EnemyHealth> _fxSpawnedFor =
        new System.Collections.Generic.HashSet<CTRL_EnemyHealth>();

    /// <summary>
    /// Pins the knockback direction (e.g. a projectile's flight direction) instead of deriving it
    /// from where the hit landed relative to the attacker.
    /// </summary>
    public void SetFixedDirection(Vector2 direction)
    {
        _hasFixedDirection = direction.sqrMagnitude > 0.0001f;
        _fixedDirection = direction.normalized;
    }

    /// <summary>
    /// Direction to push a target at targetPos: the pinned direction if one was set, otherwise
    /// away from the player (the root of this hitbox's hierarchy) toward the target.
    /// </summary>
    public Vector2 ResolveDirection(Vector2 targetPos)
    {
        if (_hasFixedDirection) return _fixedDirection;

        Vector2 away = targetPos - (Vector2)transform.root.position;
        return away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right;
    }

    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("PlayerDmg");
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;
    }

    // Enemies already damaged by the current swing. CTRL_EnemyHealth checks this on every trigger contact
    // (Enter and Stay) so each swing hits each enemy exactly once, however long the hitbox overlaps it.
    private readonly System.Collections.Generic.HashSet<CTRL_EnemyHealth> _hitEnemies =
        new System.Collections.Generic.HashSet<CTRL_EnemyHealth>();

    public bool HasHit(CTRL_EnemyHealth enemy) => _hitEnemies.Contains(enemy);
    public void MarkHit(CTRL_EnemyHealth enemy) => _hitEnemies.Add(enemy);

    /// <summary>
    /// Starts a fresh swing: every enemy becomes hittable (and gets impact FX) again. Called on enable,
    /// and by CTRL_PlayerAbilityController when a new combo hit is triggered — the hitbox can stay on
    /// across back-to-back swings, so enabling alone isn't enough. Doesn't enable or disable anything.
    /// </summary>
    public void ResetHits()
    {
        _hitEnemies.Clear();
        _fxSpawnedFor.Clear();
    }

    // Animations switch this hitbox on per swing — each activation is a fresh hit.
    private void OnEnable()
    {
        ResetHits();
    }

    /// <summary>A random non-empty entry from the list, or null if there isn't one.</summary>
    private GameObject PickRandomPrefab()
    {
        int filled = 0;
        foreach (GameObject p in _hitParticlePrefabs)
            if (p != null) filled++;
        if (filled == 0) return null;

        // Pick among the non-empty entries only, so a blank slot doesn't skew or swallow the roll.
        int pick = Random.Range(0, filled);
        foreach (GameObject p in _hitParticlePrefabs)
        {
            if (p == null) continue;
            if (pick-- == 0) return p;
        }
        return null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hitParticlePrefabs == null || _hitParticlePrefabs.Length == 0) return;

        CTRL_EnemyHealth enemy = other.GetComponentInParent<CTRL_EnemyHealth>();
        if (enemy == null) return;

        // Skip corpses — but not one this very swing just killed. CTRL_EnemyHealth's own trigger callback
        // can run before this one and mark the enemy dead first, and the killing blow should still spark.
        if (enemy.IsDead && !HasHit(enemy)) return;

        GameObject prefab = PickRandomPrefab();
        if (prefab == null) return;   // every entry empty

        // Only once the hit definitely spawns something — a rejected/empty pick shouldn't burn this
        // enemy's one particle for the swing.
        if (!_fxSpawnedFor.Add(enemy)) return;

        // Closest point on the enemy to this hitbox's centre — the surface it actually touched.
        // If the hitbox centre is already inside the enemy, that's simply the centre itself.
        Vector2 contact = other.ClosestPoint(_collider.bounds.center);

        GameObject fx = Instantiate(prefab, contact, Quaternion.identity);

        // Safety net for prefabs whose Stop Action isn't Destroy: clean up after the longest particle.
        ParticleSystem[] systems = fx.GetComponentsInChildren<ParticleSystem>();
        if (systems.Length > 0)
        {
            float longest = 0f;
            foreach (ParticleSystem ps in systems)
            {
                ParticleSystem.MainModule main = ps.main;
                // constantMax covers Constant/TwoConstants modes, curveMultiplier the curve modes.
                float lifetime = Mathf.Max(main.startLifetime.constantMax, main.startLifetime.curveMultiplier);
                longest = Mathf.Max(longest, main.duration + lifetime);
            }
            Destroy(fx, longest + 0.1f);
        }
    }

}
