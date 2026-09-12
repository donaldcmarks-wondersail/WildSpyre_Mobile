using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

/// <summary>
/// Single configurable mover that routes movement physics based on MovementCapability flags.
///
/// Walk | Jump  → Ground-based platformer movement. Requests A* paths through the ground graph.
///               Detects height differences to trigger jumps when the next waypoint is higher.
///
/// Fly          → Free-flight. Requests A* paths through air nodes. Zero gravity.
///
/// ClimbWalls   → Wall/ceiling traversal (combined with Walk). When a wall surface is detected
///               via side raycast, switches to surface-normal movement.
///
/// Graph setup (Unity Editor):
///   - Add an "A* Pathfinding" GameObject to the scene with an AstarPath component.
///   - Add a Grid Graph, configure it to scan with 2D physics against the Ground layer.
///   - For flying enemies, set the graph to treat all non-solid cells as walkable.
///   - Place NodeLink2 components at platform edges to define jumpable connections.
/// </summary>
[RequireComponent(typeof(Seeker))]
public class CTRL_EnemyMover : MonoBehaviour, IEnemyMover
{
    // ── Serialized state (Inspector-readable for debugging) ──────────────────
    [Header("Runtime State (Read Only)")]
    [SerializeField] private bool _isAtTarget;
    [SerializeField] private bool _isGrounded;
    [SerializeField] private bool _isJumping;

    // ── Private references ───────────────────────────────────────────────────
    private Rigidbody2D _rb;
    private Seeker _seeker;
    private SO_EnemyMovement _profile;

    // ── Path state ───────────────────────────────────────────────────────────
    private List<Vector3> _waypoints = new List<Vector3>();
    private int _waypointIndex;
    private Vector2 _targetPos;
    private bool _hasTarget;

    // ── Timers ───────────────────────────────────────────────────────────────
    private float _pathUpdateTimer;
    private float _jumpCooldownTimer;
    private const float JumpCooldown = 0.4f;

    // ── Off-mesh (jump) link traversal ───────────────────────────────────────
    [Header("Jump Link Debug")]
    [Tooltip("Logs why a jump link does or doesn't trigger while the enemy is near one.")]
    [SerializeField] private bool _debugLinks;
    private PlatformJumpLink _pendingLink;   // link the current path routes through, if any
    private bool _traversingLink;
    private bool _linkLeftGround;
    private Vector2 _linkLandPos;
    private float _linkBoost;
    private float _linkDirX;      // travel direction across the link (+1 / -1)
    private float _linkTimeout;
    private bool _postLinkCoast;      // hold walk speed after landing until the fresh path arrives
    private float _postLinkCoastTimeout;
    private const float LinkMaxAirTime = 2.5f;
    private const float PostLinkCoastMax = 0.5f;

    // ── Speed ────────────────────────────────────────────────────────────────
    private float _currentSpeed;
    private float _currentSmoothTime;   // active fly/climb smoothing (patrol vs chase)
    private Vector2 _smoothVelRef;      // SmoothDamp state for fly / climb easing

    // ── IEnemyMover ─────────────────────────────────────────────────────────
    public bool IsAtTarget => _isAtTarget;
    public float FacingX { get; private set; }
    public void SetFacing(float dirX) => FacingX = dirX;

    // ── Initialization ───────────────────────────────────────────────────────
    public void Initialize(Rigidbody2D rb, SO_EnemyMovement profile)
    {
        _rb = rb;
        _profile = profile;
        _currentSpeed = profile.walkSpeed;
        _currentSmoothTime = profile.velocitySmoothTime;
        _seeker = GetComponent<Seeker>();

        if (profile.capabilities.HasFlag(MovementCapability.Fly))
            _rb.gravityScale = 0f;
    }

    // ── IEnemyMover implementation ───────────────────────────────────────────
    public void MoveTo(Vector2 worldTarget)
    {
        _targetPos = worldTarget;
        _hasTarget = true;
        _isAtTarget = false;
        RequestPath();
    }

    public void Stop()
    {
        _hasTarget = false;
        _isAtTarget = false;
        _traversingLink = false;
        _postLinkCoast = false;
        _pendingLink = null;
        _isJumping = false;
        _smoothVelRef = Vector2.zero;
        _waypoints.Clear();
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }

    public void SetSpeed(float speed)
    {
        _currentSpeed = speed;
    }

    public void SetSmoothTime(float seconds)
    {
        _currentSmoothTime = seconds;
    }

    public void SetDebugLinks(bool on)
    {
        _debugLinks = on;
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────
    private void FixedUpdate()
    {
        UpdateGroundState();
        DecrementTimers();

        if (_traversingLink)
        {
            UpdateLinkTraversal();
            return;
        }

        // Just landed off a link: hold walk speed forward and wait for the fresh
        // path so we never steer along the stale one (which would brake toward a
        // now-behind waypoint — the "brief stop"). OnPathComplete ends this.
        if (_postLinkCoast)
        {
            _postLinkCoastTimeout -= Time.fixedDeltaTime;
            _rb.linearVelocity = new Vector2(_linkDirX * _currentSpeed, _rb.linearVelocity.y);

            if (_hasTarget && _pathUpdateTimer <= 0f)
            {
                _pathUpdateTimer = _profile.pathUpdateInterval;
                RequestPath();
            }
            if (_postLinkCoastTimeout <= 0f) _postLinkCoast = false;
            return;
        }

        if (!_hasTarget) return;

        if (_pathUpdateTimer <= 0f)
        {
            _pathUpdateTimer = _profile.pathUpdateInterval;
            RequestPath();
        }

        if (_waypoints == null || _waypoints.Count == 0) return;

        FollowPath();
    }

    // ── Path requests ────────────────────────────────────────────────────────
    private void RequestPath()
    {
        if (_seeker == null || !_hasTarget) return;
        _seeker.StartPath(_rb.position, _targetPos, OnPathComplete);
    }

    private void OnPathComplete(Path p)
    {
        if (p.error) return;
        _waypoints = p.vectorPath;
        _isAtTarget = false;
        _postLinkCoast = false;   // a path from the current position is here — resume normally

        // Find whether this route traverses one of our jump links. The link shows
        // up as a LinkNode in the raw node list (p.path); vectorPath modifiers
        // (funnel/raycast) strip the anchor points, so we can't rely on those.
        _pendingLink = null;
        int linkNodesInPath = 0;
        if (p.path != null)
        {
            for (int i = 0; i < p.path.Count; i++)
            {
                NodeLink2 nl = NodeLink2.GetNodeLink(p.path[i]);
                if (nl == null) continue;
                linkNodesInPath++;

                for (int k = 0; k < PlatformJumpLink.All.Count; k++)
                {
                    PlatformJumpLink jl = PlatformJumpLink.All[k];
                    if (jl != null && jl.Link == nl) { _pendingLink = jl; break; }
                }
                if (_pendingLink != null) break;
            }
        }

        if (_debugLinks)
        {
            string detail = "";
            if (PlatformJumpLink.All.Count > 0 && p.path != null && p.path.Count > 0)
            {
                PlatformJumpLink jl = PlatformJumpLink.All[0];
                float nearStart = float.MaxValue, nearEnd = float.MaxValue;
                for (int i = 0; i < p.path.Count; i++)
                {
                    Vector2 np = (Vector3)p.path[i].position;
                    nearStart = Mathf.Min(nearStart, Vector2.Distance(np, jl.StartPos));
                    nearEnd   = Mathf.Min(nearEnd,   Vector2.Distance(np, jl.EndPos));
                }
                detail = $" | link0 '{jl.name}' closestPathNode→start={nearStart:F2} →end={nearEnd:F2}";
            }
            Debug.Log($"[JumpLink] recalc — pathNodes={(p.path != null ? p.path.Count : 0)} " +
                      $"linkNodesInPath={linkNodesInPath} linksRegistered={PlatformJumpLink.All.Count} " +
                      $"pendingLink={(_pendingLink != null ? _pendingLink.name : "none")} " +
                      $"from={p.vectorPath[0]} to={p.vectorPath[p.vectorPath.Count - 1]}{detail}", this);
        }

        // Don't restart at index 0 — a fresh path's first point is the current
        // position snapped to a grid node, which is often underfoot or slightly
        // behind. Steering to it makes the enemy brake/reverse for a frame every
        // repath (the "burst" stutter). Start from the closest point, then aim at
        // the one after it.
        ResumeFromNearestWaypoint();
    }

    /// <summary>
    /// Point <see cref="_waypointIndex"/> at the path point closest to the enemy,
    /// then step one past it so movement continues forward rather than back toward
    /// a node it's already on. Used on repath and after a jump-link landing.
    /// </summary>
    private void ResumeFromNearestWaypoint()
    {
        if (_waypoints == null || _waypoints.Count == 0) return;

        _waypointIndex = 0;
        float best = float.MaxValue;
        for (int i = 0; i < _waypoints.Count; i++)
        {
            float d = Vector2.Distance(_rb.position, _waypoints[i]);
            if (d < best) { best = d; _waypointIndex = i; }
        }
        if (_waypointIndex < _waypoints.Count - 1)
            _waypointIndex++;
    }

    // ── Movement routing ─────────────────────────────────────────────────────
    private void FollowPath()
    {
        // Consume every waypoint already within reach before steering, so we never
        // spend a physics tick braking toward a node at the enemy's own feet.
        while (_waypointIndex < _waypoints.Count && ReachedWaypoint(_waypoints[_waypointIndex]))
            _waypointIndex++;

        if (_waypointIndex >= _waypoints.Count)
        {
            _isAtTarget = true;
            return;
        }

        if (TryStartLinkJump())
            return;

        Vector2 waypoint = _waypoints[_waypointIndex];

        // Intentional travel direction only — this is what facing/art flips should
        // read, since it's untouched by external pushes like knockback.
        float toWaypointX = waypoint.x - _rb.position.x;
        if (Mathf.Abs(toWaypointX) > 0.05f) FacingX = Mathf.Sign(toWaypointX);

        bool isFlying = _profile.capabilities.HasFlag(MovementCapability.Fly);
        bool canClimb = _profile.capabilities.HasFlag(MovementCapability.ClimbWalls)
                     || _profile.capabilities.HasFlag(MovementCapability.ClimbCeiling);

        if (isFlying)
        {
            MoveFly(waypoint);
        }
        else if (canClimb && IsOnSurface(out Vector2 surfaceNormal))
        {
            MoveClimb(waypoint, surfaceNormal);
        }
        else
        {
            MoveWalk(waypoint);
        }
    }

    /// <summary>
    /// Whether the enemy has effectively arrived at <paramref name="waypoint"/>.
    /// Ground walkers judge this on horizontal distance only — the grid graph
    /// plane sits at a different height than the Rigidbody2D centre, so a full
    /// 2D distance never closes to the threshold on flat ground. Waypoints that
    /// sit a jump above the enemy are never treated as reached from the ground.
    /// </summary>
    private bool ReachedWaypoint(Vector2 waypoint)
    {
        bool isFlying = _profile.capabilities.HasFlag(MovementCapability.Fly);
        bool canClimb = _profile.capabilities.HasFlag(MovementCapability.ClimbWalls)
                     || _profile.capabilities.HasFlag(MovementCapability.ClimbCeiling);

        if (isFlying)
            return Vector2.Distance(_rb.position, waypoint) < 0.5f;

        if (canClimb)
            return Vector2.Distance(_rb.position, waypoint) < _profile.waypointReachedDistance;

        float dx = Mathf.Abs(waypoint.x - _rb.position.x);
        float dy = waypoint.y - _rb.position.y;
        return dx < _profile.waypointReachedDistance && dy <= _profile.jumpHeightThreshold;
    }

    // ── Off-mesh (jump) link traversal ───────────────────────────────────────
    /// <summary>
    /// If the enemy is standing on a jump link's take-off point and its current
    /// path continues on the far side, launch a scripted jump arc toward the
    /// landing. Returns true once a traversal has been started.
    /// </summary>
    private const float LinkTriggerX = 0.6f;   // horizontal window at an anchor
    private const float LinkTriggerY = 1.3f;   // vertical window at an anchor

    private bool TryStartLinkJump()
    {
        if (_pendingLink == null || !_pendingLink.HasLanding) return false;
        if (!_profile.capabilities.HasFlag(MovementCapability.Jump)) return false;
        if (_isJumping || _jumpCooldownTimer > 0f) return false;

        // Trust "settled" over the ground raycast, which may be miscalibrated for
        // this collider: nearly-zero vertical speed means the enemy is standing.
        bool settled = _isGrounded || Mathf.Abs(_rb.linearVelocity.y) < 0.75f;

        Vector2 a = _pendingLink.StartPos;
        Vector2 b = _pendingLink.EndPos;
        float dax = Mathf.Abs(a.x - _rb.position.x), day = Mathf.Abs(a.y - _rb.position.y);
        float dbx = Mathf.Abs(b.x - _rb.position.x), dby = Mathf.Abs(b.y - _rb.position.y);
        bool atA = dax <= LinkTriggerX && day <= LinkTriggerY;
        bool atB = dbx <= LinkTriggerX && dby <= LinkTriggerY;

        if (_debugLinks && (dax < 3f || dbx < 3f))
        {
            Debug.Log($"[JumpLink] '{_pendingLink.name}'  settled={settled} grounded={_isGrounded} " +
                      $"vy={_rb.linearVelocity.y:F2} jumping={_isJumping} cooldown={_jumpCooldownTimer:F2}  " +
                      $"A(dx={dax:F2},dy={day:F2}) B(dx={dbx:F2},dy={dby:F2})  atA={atA} atB={atB}", this);
        }

        if (!settled) return false;
        if (atA) { BeginLinkJump(_pendingLink, b); _pendingLink = null; return true; }
        if (atB) { BeginLinkJump(_pendingLink, a); _pendingLink = null; return true; }
        return false;
    }

    private void BeginLinkJump(PlatformJumpLink jl, Vector2 land)
    {
        _traversingLink    = true;
        _linkLeftGround    = false;
        _linkLandPos       = land;
        _linkBoost         = jl.horizontalBoostMultiplier;
        _linkTimeout       = LinkMaxAirTime;
        _isJumping         = true;
        _jumpCooldownTimer = JumpCooldown;

        _linkDirX = Mathf.Sign(land.x - _rb.position.x);
        FacingX = _linkDirX;
        _rb.linearVelocity = new Vector2(_linkDirX * _currentSpeed * _linkBoost, jl.requiredJumpForce);
    }

    private void UpdateLinkTraversal()
    {
        _linkTimeout -= Time.fixedDeltaTime;
        if (!_isGrounded) _linkLeftGround = true;

        // Drive horizontally across the link — but stop pushing once we're past the
        // landing X, so we don't keep accelerating into a slide.
        bool pastLandingX = (_linkLandPos.x - _rb.position.x) * _linkDirX <= 0f;
        if (!pastLandingX)
            _rb.linearVelocity = new Vector2(_linkDirX * _currentSpeed * _linkBoost, _rb.linearVelocity.y);

        // End the traversal on arrival at the landing point. Position-based, so a
        // miscalibrated ground raycast (stuck true or stuck false) can't leave us
        // driving velocity across the platform.
        bool airborne = _linkLeftGround || _linkTimeout < LinkMaxAirTime - 0.15f;
        bool nearLanding = Vector2.Distance(_rb.position, _linkLandPos) < 0.6f;
        bool landed = airborne && (_isGrounded || nearLanding);

        if (landed || _linkTimeout <= 0f)
        {
            _traversingLink = false;
            _isJumping = false;
            _isAtTarget = false;

            // Exit at standard walk speed in the travel direction — not the boosted
            // arc speed, not a stop.
            _rb.linearVelocity = new Vector2(_linkDirX * _currentSpeed, _rb.linearVelocity.y);

            // Coast forward at walk speed until a path computed from *here* arrives,
            // then FollowPath resumes cleanly. Force that repath now.
            _postLinkCoast = true;
            _postLinkCoastTimeout = PostLinkCoastMax;
            _pathUpdateTimer = 0f;
        }
    }

    // ── Movement implementations ─────────────────────────────────────────────
    private void MoveFly(Vector2 waypoint)
    {
        Vector2 direction = (waypoint - _rb.position).normalized;
        _rb.linearVelocity = SmoothVelocity(direction * _profile.flySpeed);
    }

    /// <summary>
    /// Eases the current velocity toward <paramref name="target"/> over the active
    /// smoothing time (patrol or chase, set via <see cref="SetSmoothTime"/>) — rounds
    /// corners and softens starts/stops so fly/climb movement doesn't snap between
    /// headings. smoothTime 0 = instant.
    /// </summary>
    private Vector2 SmoothVelocity(Vector2 target)
    {
        if (_currentSmoothTime <= 0f)
        {
            _smoothVelRef = Vector2.zero;
            return target;
        }
        return Vector2.SmoothDamp(_rb.linearVelocity, target, ref _smoothVelRef,
                                  _currentSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);
    }

    private void MoveWalk(Vector2 waypoint)
    {
        Vector2 direction = (waypoint - _rb.position).normalized;
        float targetVelocityX = direction.x * _currentSpeed;
        float newVelocityX = Mathf.MoveTowards(
            _rb.linearVelocity.x,
            targetVelocityX,
            _profile.acceleration * Time.fixedDeltaTime
        );

        _rb.linearVelocity = new Vector2(newVelocityX, _rb.linearVelocity.y);

        // Jump if the next waypoint is significantly above the enemy
        if (_profile.capabilities.HasFlag(MovementCapability.Jump))
        {
            float heightDiff = waypoint.y - _rb.position.y;
            if (heightDiff > _profile.jumpHeightThreshold && _isGrounded && !_isJumping && _jumpCooldownTimer <= 0f)
            {
                ExecuteJump();
            }
        }
    }

    private void MoveClimb(Vector2 waypoint, Vector2 surfaceNormal)
    {
        // Move along the surface by projecting movement onto the surface tangent
        Vector2 tangent = new Vector2(-surfaceNormal.y, surfaceNormal.x);
        Vector2 toWaypoint = (waypoint - _rb.position).normalized;
        float dot = Vector2.Dot(toWaypoint, tangent);

        _rb.linearVelocity = SmoothVelocity(tangent * Mathf.Sign(dot) * _profile.climbSpeed);

        // Stick to surface
        _rb.AddForce(-surfaceNormal * 5f);
    }

    // ── Jump ─────────────────────────────────────────────────────────────────
    private void ExecuteJump()
    {
        _isJumping = true;
        _jumpCooldownTimer = JumpCooldown;
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _profile.jumpForce);
    }

    // ── Ground / surface detection ────────────────────────────────────────────
    private void UpdateGroundState()
    {
        bool wasGrounded = _isGrounded;
        _isGrounded = Physics2D.BoxCast(
            _rb.position,
            new Vector2(0.4f, 0.1f),
            0f,
            Vector2.down,
            _profile.groundCheckDistance,
            _profile.groundMask
        );

        if (_isGrounded && !wasGrounded)
            _isJumping = false;
    }

    private bool IsOnSurface(out Vector2 normal)
    {
        // Check left wall
        RaycastHit2D leftHit = Physics2D.Raycast(_rb.position, Vector2.left, 0.35f, _profile.groundMask);
        if (leftHit.collider != null) { normal = leftHit.normal; return true; }

        // Check right wall
        RaycastHit2D rightHit = Physics2D.Raycast(_rb.position, Vector2.right, 0.35f, _profile.groundMask);
        if (rightHit.collider != null) { normal = rightHit.normal; return true; }

        // Check ceiling (if ClimbCeiling flag is set)
        if (_profile.capabilities.HasFlag(MovementCapability.ClimbCeiling))
        {
            RaycastHit2D ceilHit = Physics2D.Raycast(_rb.position, Vector2.up, 0.35f, _profile.groundMask);
            if (ceilHit.collider != null) { normal = ceilHit.normal; return true; }
        }

        normal = Vector2.up;
        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void DecrementTimers()
    {
        _pathUpdateTimer -= Time.fixedDeltaTime;
        if (_jumpCooldownTimer > 0f)
            _jumpCooldownTimer -= Time.fixedDeltaTime;
    }
}
