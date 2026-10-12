using UnityEngine;

/// <summary>
/// Projectile that flies freely until it first touches a surface, then sticks to and crawls along
/// it — ground, wall, or ceiling, independently configurable via SO_SurfaceProjectileData — tilting
/// to match the surface normal and able to travel from ground to wall to ceiling around corners, as
/// long as the angle jump at each corner stays under Max Transition Angle Delta.
///
/// Surface-tracking technique: each FixedUpdate it casts a short probe opposite the currently-
/// tracked normal, from a point already stepped slightly ahead along the current tangent direction
/// — not a fixed world-axis ray. That forward offset is what lets the probe "round" a convex
/// corner: right at the corner edge the old face stops being hit and the probe picks up the new
/// adjacent face instead, the same fundamental idea as a 2D slope-walker. This generalizes
/// CTRL_EnemyMover's IsOnSurface()/MoveClimb(), which only ever checks three fixed world-axis
/// directions (left/right/up) and so can't track an arbitrary slope or round a corner smoothly.
///
/// Damage/collision with the player reuses the existing tag/layer pipeline unchanged: this object
/// is tagged "DamagePlayer" in Awake, same as CTRL_EnemyProjectile, and MNGR_PlayerLife already
/// listens for that tag — no manual player-damage call needed here.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class CTRL_SurfaceProjectile : MonoBehaviour, IThrownProjectile
{
    [SerializeField] private SO_SurfaceProjectileData _data;
    [Tooltip("Where Sprite Curve's frames get applied. Auto-found on this GameObject in Awake if " +
             "left empty.")]
    [SerializeField] private SpriteRenderer _spriteRenderer;

    private enum State { FreeFlight, Attached, Detached }

    private Rigidbody2D _rb;
    private State _state;
    private Vector2 _normal = Vector2.up;
    private Vector2 _lastTangentDir = Vector2.right;
    private float _spawnTime;
    private int _lastSpriteIndex = -1;
    // Captured once, before anything ever touches localScale, so the X-flip in UpdateFacingScale()
    // always derives from the prefab's own authored scale rather than compounding off whatever it
    // last set — same "rebuild from the original, never from the previous frame's result" principle
    // as the trail-gradient scaling elsewhere in this project.
    private Vector3 _baseScale;

    private void Awake()
    {
        gameObject.tag = "DamagePlayer";

        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        GetComponent<Collider2D>().isTrigger = true;

        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        _baseScale = transform.localScale;
        _state = State.FreeFlight;
        _spawnTime = Time.time;

        if (_data != null)
            Destroy(gameObject, _data.lifetimeSeconds);
    }

    private void Update()
    {
        UpdateSpriteFrame();
    }

    /// <summary>0 at spawn, 1 at Lifetime Seconds — the shared normalized time-domain both
    /// lifetime-driven curves are evaluated against, matching Unity's own "over Lifetime" curve
    /// convention (Particle System's Size/Color over Lifetime, etc.) rather than raw elapsed
    /// seconds, so a curve authored the standard way (keys spanning 0 to 1) behaves the same
    /// regardless of how long Lifetime Seconds actually is.</summary>
    private float LifetimeProgress01() =>
        Mathf.Clamp01((Time.time - _spawnTime) / Mathf.Max(0.0001f, _data.lifetimeSeconds));

    /// <summary>Applies Sprite Curve: evaluates it at the current lifetime progress, rounds to the
    /// nearest frame index, and only touches the renderer when that index actually changes.</summary>
    private void UpdateSpriteFrame()
    {
        if (_data == null || _spriteRenderer == null) return;
        if (_data.spriteFrames == null || _data.spriteFrames.Length == 0) return;

        int index = Mathf.Clamp(Mathf.RoundToInt(_data.spriteCurve.Evaluate(LifetimeProgress01())),
            0, _data.spriteFrames.Length - 1);

        if (index == _lastSpriteIndex) return;
        _lastSpriteIndex = index;
        _spriteRenderer.sprite = _data.spriteFrames[index];
    }

    /// <summary>Called immediately after instantiation (e.g. by Ability_ThrowProjectile).</summary>
    public void Launch(Vector2 direction, float speed)
    {
        _rb.linearVelocity = direction.normalized * speed;
        FaceDirection(direction);
    }

    private void FixedUpdate()
    {
        if (_data == null || _state != State.Attached) return;
        TickSurfaceFollow();
    }

    // ── Contact ──────────────────────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_data == null) return;
        if (other.CompareTag("DamagePlayer") || other.CompareTag("Enemy")) return;

        bool isGround = ((1 << other.gameObject.layer) & _data.groundMask.value) != 0;

        if (_state == State.FreeFlight)
        {
            if (isGround)
            {
                Vector2 point = other.ClosestPoint(transform.position);
                Vector2 normal = (Vector2)transform.position - point;
                normal = normal.sqrMagnitude > 0.0001f
                    ? normal.normalized
                    : (_rb.linearVelocity.sqrMagnitude > 0.0001f ? -_rb.linearVelocity.normalized : Vector2.up);

                AttachTo(point, normal);
                return;
            }
        }
        else if (isGround)
        {
            // Already riding this surface — continuous overlap with it every frame is expected,
            // not a new hit.
            return;
        }

        // Spawn grace: something spawned very close to whatever launched it (a burst point, an
        // impact point, a hand) can easily overlap that same thing on its very first physics step,
        // before velocity has carried it clear — don't let that count as "hit something, die."
        // Ground-layer overlaps are handled above regardless of grace, since attaching to ground
        // immediately on spawn is always fine, never a reason to destroy.
        if (Time.time - _spawnTime < _data.spawnGracePeriod) return;

        // Hit the player, a wall while still in free flight, or anything else solid — destroy,
        // same as CTRL_EnemyProjectile. Also where a crawling attachment ends if it runs into the
        // player mid-surface.
        if (_data.hitParticlesPrefab != null)
            Instantiate(_data.hitParticlesPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    // ── Surface attach / follow ─────────────────────────────────────────────
    private void AttachTo(Vector2 point, Vector2 normal)
    {
        _normal = normal;

        Vector2 travelDir = _rb.linearVelocity.sqrMagnitude > 0.0001f
            ? _rb.linearVelocity.normalized
            : (Vector2)transform.right;
        _lastTangentDir = TangentContinuing(normal, travelDir);

        Vector2 snapped = point + normal * _data.surfaceSkin;
        transform.position = new Vector3(snapped.x, snapped.y, transform.position.z);

        _rb.linearVelocity = Vector2.zero;
        FaceDirection(normal);
        UpdateFacingScale();
        _state = State.Attached;
    }

    private void TickSurfaceFollow()
    {
        Vector2 probeOrigin = (Vector2)transform.position
                             + _lastTangentDir * _data.surfaceProbeAheadDistance
                             + _normal * _data.surfaceSkin;
        RaycastHit2D hit = Physics2D.Raycast(probeOrigin, -_normal, _data.surfaceProbeDistance, _data.groundMask);

        if (hit.collider == null)
        {
            Detach();
            return;
        }

        float angleFromUp = Vector2.Angle(Vector2.up, hit.normal);
        float transitionDelta = Vector2.Angle(_normal, hit.normal);

        bool supported = _data.groundSupport.Contains(angleFromUp)
                       || _data.wallSupport.Contains(angleFromUp)
                       || _data.ceilingSupport.Contains(angleFromUp);

        if (!supported || transitionDelta > _data.maxTransitionAngleDelta)
        {
            Detach();
            return;
        }

        _normal = hit.normal;
        _lastTangentDir = TangentContinuing(_normal, _lastTangentDir);

        // Velocity Curve multiplies the configured crawl Speed over the course of the projectile's
        // life — flat at 1 (the default curve) reproduces the old constant-speed behavior exactly.
        float speedMultiplier = Mathf.Max(0f, _data.velocityCurve.Evaluate(LifetimeProgress01()));
        float effectiveSpeed = _data.speed * speedMultiplier;

        Vector2 newPos = hit.point + _normal * _data.surfaceSkin + _lastTangentDir * effectiveSpeed * Time.fixedDeltaTime;
        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);

        EaseRotationToward(_normal);
        UpdateFacingScale();
    }

    private void Detach()
    {
        _state = State.Detached;

        switch (_data.detachBehavior)
        {
            case SurfaceDetachBehavior.Destroy:
                Destroy(gameObject);
                break;
            case SurfaceDetachBehavior.FreeFall:
                _rb.gravityScale = 1f;
                _rb.linearVelocity = _lastTangentDir * _data.speed;
                break;
            case SurfaceDetachBehavior.Stop:
                _rb.linearVelocity = Vector2.zero;
                break;
        }
    }

    /// <summary>The tangent direction (perpendicular to normal) that best continues
    /// previousDirection — i.e. doesn't reverse travel just because the normal flipped sign
    /// across a corner.</summary>
    private static Vector2 TangentContinuing(Vector2 normal, Vector2 previousDirection)
    {
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        return Vector2.Dot(tangent, previousDirection) >= 0f ? tangent : -tangent;
    }

    // ── Orientation ──────────────────────────────────────────────────────────
    private void FaceDirection(Vector2 dir)
    {
        transform.rotation = Quaternion.AngleAxis(AngleFor(dir), Vector3.forward);
    }

    private void EaseRotationToward(Vector2 dir)
    {
        float targetAngle = AngleFor(dir);
        float currentAngle = transform.rotation.eulerAngles.z;
        float eased = Mathf.MoveTowardsAngle(currentAngle, targetAngle, _data.rotationSmoothSpeed * Time.fixedDeltaTime);
        transform.rotation = Quaternion.AngleAxis(eased, Vector3.forward);
    }

    private float AngleFor(Vector2 dir) =>
        Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + _data.orientationOffsetDegrees;

    /// <summary>Rotation alone (above) keeps local+Y pointed at the surface normal — set
    /// Orientation Offset Degrees to -90 on the data asset so that's actually true for a sprite
    /// drawn facing +X by default. That rotation is the SAME regardless of which way along the
    /// tangent this is crawling (a flat floor's normal is (0,1) whether moving left or right), so
    /// it alone can't show travel direction. Flipping 180 degrees to show it would also flip "up"
    /// upside-down. Flipping local scale.x instead keeps the object right-side-up (Y still at the
    /// normal) while mirroring it to face wherever _lastTangentDir actually points.</summary>
    private void UpdateFacingScale()
    {
        bool movingAgainstLocalRight = Vector2.Dot(transform.right, _lastTangentDir) < 0f;
        float xSign = movingAgainstLocalRight ? -1f : 1f;
        transform.localScale = new Vector3(Mathf.Abs(_baseScale.x) * xSign, _baseScale.y, _baseScale.z);
    }
}
