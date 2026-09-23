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
    [SerializeField] private bool _isInvulnerable;   // mirrors the i-frame window, for the Inspector only
    private float _invulnerableUntil;                 // Time.time deadline of the post-hit i-frame window
    private bool _abilityInvulnerable;   // driven by abilities (e.g. Ability_Dash), independent of hit i-frames
    private Vector2 _lastHitDirection = Vector2.up;    // see LastHitDirection

    [Header("Events")]
    public UnityEvent onDamaged = new UnityEvent();
    public UnityEvent onDeath   = new UnityEvent();

    // ── Private references ───────────────────────────────────────────────────
    private SO_EnemyStats _stats;
    private CTRL_EnemyStateMachine _stateMachine;
    private Rigidbody2D _rb;
    private EnemyBlackboard _board;
    private EnemyHitFlash _hitFlash;

    // ── Public accessors ─────────────────────────────────────────────────────
    public int  CurrentHP => _currentHP;
    public bool IsDead    => _currentHP <= 0;
    /// <summary>Normalized knockback direction of the most recent hit that actually carried one —
    /// e.g. CTRL_EnemyPickupDrop reads this on death to bias a pickup burst toward wherever the
    /// killing blow was pushing (shoot left-to-right, drops pop out to the right). Stays at its
    /// Vector2.up default for an enemy that's only ever taken non-directional damage (a burn tick,
    /// which goes through ApplyBurnTick and never touches this).</summary>
    public Vector2 LastHitDirection => _lastHitDirection;
    private bool InHitIFrames => Time.time < _invulnerableUntil;
    private bool IsInvulnerable => InHitIFrames || _abilityInvulnerable;

    private void Update()
    {
        _isInvulnerable = InHitIFrames;
    }

    /// <summary>
    /// Lets an ability (e.g. a dash) grant invulnerability for its own duration,
    /// separate from the post-hit i-frame window so the two never stomp each other.
    /// </summary>
    public void SetAbilityInvulnerable(bool on) => _abilityInvulnerable = on;

    // ── Initialization ───────────────────────────────────────────────────────
    public void Initialize(SO_EnemyStats stats, CTRL_EnemyStateMachine stateMachine, Rigidbody2D rb, EnemyBlackboard board)
    {
        _stats        = stats;
        _stateMachine = stateMachine;
        _rb           = rb;
        _board        = board;
        _currentHP    = stats.maxHP;
        // Defensive: a pooled/reused enemy must never inherit a stuck invulnerability flag from
        // its previous life (see the try/finally fix in Ability_Dash.DashCO for the actual source).
        _abilityInvulnerable = false;
        _invulnerableUntil   = 0f;

        if (stats.hitFlashMaterial != null)
        {
            _hitFlash = GetComponent<EnemyHitFlash>();
            if (_hitFlash == null) _hitFlash = gameObject.AddComponent<EnemyHitFlash>();
            _hitFlash.Initialize(stats.hitFlashMaterial, stats.hitFlashDuration);
        }
        Debug.Log($"[EnemyHP] {name}#{GetInstanceID()} INIT hp={_currentHP} stats={stats.name} t={Time.time:F2}");
    }

    // ── Physics callbacks ────────────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other) => HandleContact(other, isStay: false);

    // Ability hits keep checking while they overlap: OnTriggerEnter2D fires only once per overlap, so a
    // swing that first touched the enemy while it was invulnerable (dash) would otherwise be lost.
    private void OnTriggerStay2D(Collider2D other) => HandleContact(other, isStay: true);

    private void HandleContact(Collider2D other, bool isStay)
    {
        if (IsDead)
        {
            Debug.Log($"[EnemyHP] {name}#{GetInstanceID()} contact IGNORED — already dead (src={other.name})");
            return;
        }
        if (other.gameObject.layer != LayerMask.NameToLayer("PlayerDmg")) return;

        // Player ability hits (combo, charge/aim projectiles) — no stomp-style reaction; whether they
        // knock back or interrupt is up to the ability's authored HitEffects. Checked before the stomp
        // Damager so a hitbox parented under a Damager can never be mistaken for a stomp.
        PlayerAbilityDamager abilityDamager = other.GetComponentInParent<PlayerAbilityDamager>();
        if (abilityDamager != null)
        {
            // Dash invulnerability blocks the hit without using it up, so it lands once the dash ends.
            if (_abilityInvulnerable) { Debug.Log($"[EnemyHP] {name}#{GetInstanceID()} ability hit BLOCKED by dash invulnerability, src={other.name}"); return; }
            if (abilityDamager.HasHit(this))
            {
                Debug.Log($"[EnemyHP] {name}#{GetInstanceID()} ability hit IGNORED — already hit this swing, src={other.name}");
                return;
            }

            HitEffects fx = abilityDamager.effects;
            DamageInfo abilityInfo = new DamageInfo
            {
                amount = abilityDamager.damage,
                applyKnockback = fx.causesKnockback,
                knockback = abilityDamager.ResolveDirection(transform.position) * fx.knockbackForce,
                interrupt = fx.causesInterrupt,
                interruptDuration = fx.interruptDuration,
                hitStopDuration = fx.hitStopDuration,
                shakeStrength = fx.shakeStrength,
                shakeDuration = fx.shakeDuration
            };

            Debug.Log($"[EnemyHP] {name}#{GetInstanceID()} ability hit src={other.name} dmg={abilityInfo.amount} hpBefore={_currentHP} t={Time.time:F2}");
            abilityDamager.MarkHit(this);
            // Each swing hits each enemy once (tracked above), so the post-hit i-frames don't gate it.
            TakeDamage(abilityInfo, ignoreHitIFrames: true);
            return;
        }

        // Everything else stays one-shot per contact: no Stay handling, still gated by the i-frame window.
        if (isStay || IsInvulnerable) return;

        DamageInfo info = new DamageInfo { amount = 1 };

        Damager damager = other.GetComponentInParent<Damager>();
        if (damager != null)
        {
            // Player stomp — triggers the stomp reaction on the damager (bounce-back for the player).
            // Keeps its long-standing behaviour: knocked back with the enemy's own stats and cut off
            // into hit-stun, with no extra ability lockout beyond that stun.
            damager.DamagerReaction();

            Vector2 dir = ((Vector2)transform.position - (Vector2)other.transform.position).normalized;
            info.amount = damager.damage;
            info.applyKnockback = true;
            info.knockback = new Vector2(dir.x * _stats.knockbackForce, _stats.knockbackDirection.y * _stats.knockbackForce);
            info.interrupt = true;
            info.interruptDuration = 0f;
        }

        TakeDamage(info);
    }

    // ── Public damage API ────────────────────────────────────────────────────
    /// <param name="ignoreHitIFrames">Skip the post-hit i-frame window (dash invulnerability still blocks).</param>
    public void TakeDamage(DamageInfo info, bool ignoreHitIFrames = false)
    {
        if (IsDead || _abilityInvulnerable) return;
        if (!ignoreHitIFrames && InHitIFrames) return;

        // Captured before ReduceHP (which may kill and fire onDeath this same call) so a death
        // burst spawned from onDeath already sees the killing blow's direction.
        if (info.knockback.sqrMagnitude > 0.0001f)
            _lastHitDirection = info.knockback.normalized;

        // Impact feedback on every landed hit, killing blows included (so it goes before ReduceHP).
        _hitFlash?.Play();
        HitStop.Trigger(info.hitStopDuration);
        CameraShake.Shake(info.shakeStrength, info.shakeDuration);

        if (ReduceHP(info.amount)) return;   // died

        // Every non-fatal hit plays the damage reaction, whether or not it interrupts or knocks back.
        _board.anim?.SetTrigger("TakeDamage");

        // Interrupt: cancel the current ability (done by the TakeDamage state on entry) and lock
        // abilities out until the timer passes. Skipped while an ability has interrupt armor up.
        if (info.interrupt && !_board.InterruptImmune)
        {
            _board.abilitiesLockedUntil = Mathf.Max(_board.abilitiesLockedUntil, Time.time + info.interruptDuration);
            _stateMachine.TransitionTo("TakeDamage");
        }

        // Knockback, scaled by this enemy's weight. Applied AFTER the state transition on purpose:
        // entering TakeDamage calls mover.Stop(), which zeroes the velocity — setting it before would
        // silently erase the knockback. (While an ability's own movement is running — a dash, an
        // anticipation pullback — that movement writes position every frame and overrides this.)
        if (info.applyKnockback && !_stats.ignoreKnockback && !_board.KnockbackImmune)
            _rb.linearVelocity = info.knockback * _stats.knockbackMagnitude;

        _invulnerableUntil = Time.time + _stats.invulnerabilityDuration;
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
        if (amount <= 0) return;   // a zero/negative tick would heal (HP -= amount) — burning never does that
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
        Debug.Log($"[EnemyHP] {name}#{GetInstanceID()} ReduceHP amount={amount} hpAfter={_currentHP}");
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
        Debug.Log($"[EnemyHP] {name}#{GetInstanceID()} DIE");

        // onDeath can have arbitrary listeners (CTRL_EnemyPickupDrop, etc.) — one throwing must
        // never be able to skip the state transition and Destroy below it. Before this try/catch,
        // an exception here left a permanent corpse: IsDead was already true (correctly rejecting
        // every further hit, per the [EnemyHP] ... contact IGNORED — already dead logs), but the
        // GameObject, its collider, and its Dead-state transition never happened, so it just stood
        // there forever, un-killable and un-removable.
        try
        {
            onDeath?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e, this);
        }

        _stateMachine.TransitionTo("Dead");
        // Per-enemy delay (SO_EnemyStats.deathDestroyDelay) so the death animation has time to play
        Destroy(gameObject, _stats.deathDestroyDelay);
    }

}
