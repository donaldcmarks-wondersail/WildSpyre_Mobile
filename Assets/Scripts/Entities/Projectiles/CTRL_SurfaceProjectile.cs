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
    // Which way the X-flip in UpdateFacingScale() last pointed — kept separately so ApplyScale() can
    // rebuild localScale every frame from _baseScale × Scale Curve without losing the facing.
    private float _facingXSign = 1f;

    // Fire trail — only active when Leave Fire Trail is on AND a spawner handed us an emitter via
    // SetFireTrail() (a runtime-spawned prefab can't hold that scene reference itself).
    private FireTrailEmitterBase _fireTrailEmitter;
    private FireTrailController _trailRun;
    private Vector2 _lastStampPos;

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
        ApplyScale();
    }

    /// <summary>Rebuilds localScale from the prefab's authored scale × Scale Curve at the current
    /// lifetime progress, keeping the current facing flip. Runs every frame in every state, so the
    /// curve applies during free flight too, not just while crawling.</summary>
    private void ApplyScale()
    {
        if (_data == null) return;

        float multiplier = Mathf.Max(0f, _data.scaleCurve.Evaluate(LifetimeProgress01()));
        transform.localScale = new Vector3(Mathf.Abs(_baseScale.x) * _facingXSign * multiplier,
                                           _baseScale.y * multiplier,
                                           _baseScale.z);
    }

    /// <summary>0 at spawn, 1 at Lifetime Seconds — the shared normalized time-domain both
    /// lifetime-driven curves are evaluated against, matching Unity's own "over Lifetime" curve
    /// convention (Particle System's Size/Color over Lifetime, etc.) rather than raw elapsed
    /// seconds, so a curve authored the standard way (keys spanning 0 to 1) behaves the same
    /// regardless of how long Lifetime Seconds actually is.</summary>
    private float LifetimeProgress01() =>
        Mathf.Clamp01((Time.time - _spawnTime) / Mathf.Max(0.0001f, _data.lifetimeSeconds));

    /// <summary>Applies Sprite Curve: evaluates it at the current lifetime progress as a normalized
    /// 0-1 value (0 = first frame, 1 = last frame), scales that across the frame list, rounds to the
    /// nearest frame index, and only touches the renderer when that index actually changes.</summary>
    private void UpdateSpriteFrame()
    {
        if (_data == null || _spriteRenderer == null) return;
        if (_data.spriteFrames == null || _data.spriteFrames.Length == 0) return;

        int lastIndex = _data.spriteFrames.Length - 1;
        float normalized = Mathf.Clamp01(_data.spriteCurve.Evaluate(LifetimeProgress01()));
        int index = Mathf.RoundToInt(normalized * lastIndex);

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

    /// <summary>Optional, called right after Launch() by spawners that want this projectile to lay
    /// a fire trail (still gated by Leave Fire Trail on the data asset).</summary>
    public void SetFireTrail(FireTrailEmitterBase emitter)
    {
        _fireTrailEmitter = emitter;
    }

    private void OnDestroy()
    {
        EndFireTrail();
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

        // Snap onto the surface by projecting the CURRENT position onto the hit surface's line, not
        // by starting from hit.point itself — hit.point sits Surface Probe Ahead Distance in front of
        // us, so building from it advanced the projectile by that distance every physics step on top
        // of its real speed (0.15 per step = ~7.5 units/sec), which is why it never stopped even with
        // Velocity Curve at 0. The ahead-probe is still what detects the next face at corners.
        Vector2 pos = transform.position;
        Vector2 onSurface = pos - Vector2.Dot(pos - hit.point, _normal) * _normal;

        float effectiveSpeed = CurrentCrawlSpeed();

        Vector2 newPos = onSurface + _normal * _data.surfaceSkin + _lastTangentDir * effectiveSpeed * Time.fixedDeltaTime;
        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);

        UpdateFireTrail(newPos - _normal * _data.surfaceSkin, hit.collider.gameObject.layer);

        EaseRotationToward(_normal);
        UpdateFacingScale();
    }

    /// <summary>Velocity Curve multiplies the configured crawl Speed over the course of the
    /// projectile's life — flat at 1 (the default curve) reproduces constant-speed behavior.</summary>
    private float CurrentCrawlSpeed() =>
        _data.speed * Mathf.Max(0f, _data.velocityCurve.Evaluate(LifetimeProgress01()));

    // ── Fire trail ───────────────────────────────────────────────────────────
    /// <summary>Stamps fire at surfacePoint every Fire Trail Spacing units crawled, while the surface
    /// under it is on Fire Trail Mask. Crawling onto a non-trail layer ends the current run, so
    /// coming back onto a trail layer starts a fresh, disconnected one — same as the player's trail
    /// splitting per platform.</summary>
    private void UpdateFireTrail(Vector2 surfacePoint, int surfaceLayer)
    {
        if (!_data.leaveFireTrail || _fireTrailEmitter == null) return;

        int mask = _data.fireTrailMask.value != 0 ? _data.fireTrailMask.value : _data.groundMask.value;
        if (((1 << surfaceLayer) & mask) == 0)
        {
            EndFireTrail();
            return;
        }

        if (_trailRun != null && Vector2.Distance(surfacePoint, _lastStampPos) < _data.fireTrailSpacing)
            return;

        if (_trailRun == null)
        {
            _trailRun = _fireTrailEmitter.CheckOutExternalRun();
            if (_trailRun == null) return;
        }

        _trailRun.PlaceStamp(surfacePoint, _normal, alignToNormal: true);
        _lastStampPos = surfacePoint;
    }

    /// <summary>Hands the current run back so it burns out and returns to the emitter's pool on its own.</summary>
    private void EndFireTrail()
    {
        if (_trailRun == null) return;
        _trailRun.Release();
        _trailRun = null;
    }

    private void Detach()
    {
        _state = State.Detached;
        EndFireTrail();

        switch (_data.detachBehavior)
        {
            case SurfaceDetachBehavior.Destroy:
                Destroy(gameObject);
                break;
            case SurfaceDetachBehavior.FreeFall:
                _rb.gravityScale = 1f;
                _rb.linearVelocity = _lastTangentDir * CurrentCrawlSpeed();
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
        _facingXSign = movingAgainstLocalRight ? -1f : 1f;
        ApplyScale();
    }
}
