using UnityEngine;

/// <summary>
/// Root coordinator for all enemy subsystems. Attach this to the root enemy GameObject.
///
/// Setup in Inspector:
///   1. Assign an SO_EnemyProfile asset.
///   2. Assign Rigidbody2D, Animator, SpriteRenderer (or leave null for enemies without visuals).
///   3. For a hand-placed enemy that patrols, drag a scene PatrolRoute into Patrol Route.
///      Spawned enemies instead receive a route from the spawner via AssignPatrolRoute().
///   4. Ensure the GameObject has tag "Enemy" and is on the EnemyCol layer (7).
///   5. Add a Collider2D to the root (non-trigger) for passive contact damage.
///   6. For melee attacks, add a child GameObject named to match SO_AbilityMelee.hitboxChildName
///      with a Collider2D and CTRL_EnemyDamager component.
///
/// A* Pathfinding Project must be imported and a scene-level AstarPath GameObject must exist.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CTRL_Enemy : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private SO_EnemyProfile _profile;

    [Header("Component References")]
    public Rigidbody2D rb;
    public Animator    anim;
    public SpriteRenderer spriteRend;

    [Header("Visuals")]
    [Tooltip("Child object flipped on X to face movement direction, or the attack " +
             "target while attacking (any ability). Knockback/hit-stun (which pushes " +
             "the Rigidbody2D directly) never affects it — facing holds steady through " +
             "a hit and through standing still, changing only on real movement or attack.")]
    [SerializeField] private Transform _characterArt;

    [Header("Patrol")]
    [Tooltip("For hand-placed enemies. Spawned enemies get their route from the spawner " +
             "via AssignPatrolRoute(); leave this empty for those.")]
    [SerializeField] private PatrolRoute _patrolRoute;

    [Header("Debug")]
    [Tooltip("Logs jump-link detection and trigger checks from CTRL_EnemyMover.")]
    [SerializeField] private bool _debugJumpLinks;

    // ── Private systems ───────────────────────────────────────────────────────
    private EnemyBlackboard _board;
    private CTRL_EnemyStateMachine _stateMachine;
    private CTRL_EnemyHealth _health;
    private CTRL_AbilityController _abilityController;

    // ── Detection ─────────────────────────────────────────────────────────────
    private Transform _playerTransform;
    private float _detectionTimer;
    private const float DetectionInterval = 0.2f;
    private float _lastLineOfSightTime = -999f;   // Time.time of the last clear look at the player

    // ── Facing ────────────────────────────────────────────────────────────────
    private float _artBaseScaleX = 1f;
    private bool  _facingRight = true;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponent<Animator>();   // expects the Animator on the enemy root, not a child

        if (_characterArt == null && spriteRend != null)
            _characterArt = spriteRend.transform;   // fall back to the sprite's own transform for facing flips

        if (_characterArt != null)
        {
            _artBaseScaleX = Mathf.Abs(_characterArt.localScale.x);
            if (_artBaseScaleX < 0.0001f) _artBaseScaleX = 1f;   // never lock the art to zero width
            _facingRight   = _characterArt.localScale.x >= 0f;
        }
#if UNITY_EDITOR
        else
        {
            Debug.LogError($"[CTRL_Enemy] '{name}': no Character Art transform and no SpriteRenderer — " +
                           $"facing flips are disabled. Assign the Character Art field.", this);
        }
#endif

        CachePlayerReference();
        BuildBlackboard();
        InitStateMachine();
        InitHealth();
        InitAbilityController();
        InitMover();

        _stateMachine.SetInitialState(DetermineInitialState());
    }

    private void Start()
    {
        MNGR_EnemyManager.Instance.RegisterEnemy(this);
    }

    private void OnDestroy()
    {
        if (MNGR_EnemyManager.Instance != null)
            MNGR_EnemyManager.Instance.UnregisterEnemy(this);
    }

    private void Update()
    {
        EvaluateDetection();
        _stateMachine.Update();
    }

    private void LateUpdate()
    {
        // After the Animator has evaluated, so a scale curve on the art hierarchy
        // can't fight the facing flip.
        UpdateFacing();
    }

    private void FixedUpdate()
    {
        _stateMachine.FixedUpdate();
    }

    // ── Initialization ────────────────────────────────────────────────────────
    private void CachePlayerReference()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            _playerTransform = playerObj.transform;
    }

    private void BuildBlackboard()
    {
        _board = new EnemyBlackboard
        {
            owner           = this,
            rb              = rb,
            anim            = anim,
            spriteRend      = spriteRend,
            profile         = _profile,
            patrolPoints    = ResolvePatrolPoints(),
            isStatic        = (_profile.movement.capabilities == MovementCapability.None)
        };
    }

    private void InitStateMachine()
    {
        _stateMachine = new CTRL_EnemyStateMachine(_board);
        _stateMachine.RegisterState("Idle",       new EnemyState_Idle());
        _stateMachine.RegisterState("Patrol",     new EnemyState_Patrol());
        _stateMachine.RegisterState("Chase",      new EnemyState_Chase());
        _stateMachine.RegisterState("Attack",     new EnemyState_Attack());
        _stateMachine.RegisterState("TakeDamage", new EnemyState_TakeDamage());
        _stateMachine.RegisterState("Stunned",    new EnemyState_Stunned());
        _stateMachine.RegisterState("Dead",       new EnemyState_Dead());
    }

    private void InitHealth()
    {
        _health = GetComponent<CTRL_EnemyHealth>();
        if (_health == null) _health = gameObject.AddComponent<CTRL_EnemyHealth>();
        _health.Initialize(_profile.stats, _stateMachine, rb, _board);
        _board.health = _health;
    }

    private void InitAbilityController()
    {
        _abilityController = GetComponent<CTRL_AbilityController>();
        if (_abilityController == null) _abilityController = gameObject.AddComponent<CTRL_AbilityController>();
        _abilityController.Initialize(_profile.abilitySet, _board);
        _board.abilities = _abilityController;
    }

    private void InitMover()
    {
        if (_board.isStatic) return;

        CTRL_EnemyMover moverComp = gameObject.AddComponent<CTRL_EnemyMover>();
        moverComp.Initialize(rb, _profile.movement);
        moverComp.SetDebugLinks(_debugJumpLinks);
        _board.mover = moverComp;
        _board.mover.SetSpeed(_profile.movement.walkSpeed);
    }

    private string DetermineInitialState()
    {
        if (_board.isStatic || _profile.behavior.patrolType == PatrolType.Static)
            return "Idle";

        return _board.HasPatrolRoute ? "Patrol" : "Idle";
    }

    // ── Patrol route ──────────────────────────────────────────────────────────
    /// <summary>
    /// Builds the patrol point set at Awake, in priority order:
    ///   1. A PatrolRoute assigned in the Inspector (hand-placed enemies).
    ///   2. SO_EnemyBehavior.localPatrolOffsets, resolved relative to the spawn
    ///      position (procedurally-placed enemies with no authored route).
    /// A spawner can override the result any time via <see cref="AssignPatrolRoute"/>.
    /// </summary>
    private Vector2[] ResolvePatrolPoints()
    {
        if (_patrolRoute != null && _patrolRoute.Count > 0)
            return _patrolRoute.ToWorldPoints();

        Vector2[] offsets = _profile.behavior.localPatrolOffsets;
        if (offsets != null && offsets.Length > 0)
        {
            Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
            var pts = new Vector2[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
                pts[i] = origin + offsets[i];
            return pts;
        }

        return System.Array.Empty<Vector2>();
    }

    /// <summary>
    /// Assigns a patrol route at runtime — call this right after Instantiate() when
    /// spawning enemies. Safe to call after Awake: if the enemy is idling and not
    /// engaged, it starts patrolling immediately. Pass null to clear the route.
    /// </summary>
    public void AssignPatrolRoute(PatrolRoute route)
    {
        _board.patrolPoints = (route != null && route.Count > 0)
            ? route.ToWorldPoints()
            : System.Array.Empty<Vector2>();
        _board.currentWaypointIndex = 0;
        _board.patrolForward = true;

        bool canPatrol = _board.HasPatrolRoute
                      && !_board.isStatic
                      && _profile.behavior.patrolType != PatrolType.Static;

        if (canPatrol && _stateMachine.ActiveStateName == "Idle")
            _stateMachine.TransitionTo("Patrol");
        else if (!canPatrol && _stateMachine.ActiveStateName == "Patrol")
            _stateMachine.TransitionTo("Idle");
    }

    // ── Detection ─────────────────────────────────────────────────────────────
    private void EvaluateDetection()
    {
        if (_playerTransform == null) return;

        _detectionTimer -= Time.deltaTime;
        if (_detectionTimer > 0f) return;
        _detectionTimer = DetectionInterval;

        float dist = Vector2.Distance(rb.position, _playerTransform.position);
        SO_EnemyBehavior behavior = _profile.behavior;
        Vector2 toPlayer = ((Vector2)_playerTransform.position - rb.position).normalized;

        bool engaged = _stateMachine.ActiveStateName == "Chase"
                    || _stateMachine.ActiveStateName == "Attack";

        // Persistent tracking: while engaged, keep pathing to the player's real
        // position through cover — FOV ignored — until EITHER the player breaks the
        // leash distance OR line of sight stays broken for persistentLoseSightTime.
        // Initial acquisition (below) still uses detection radius, FOV and LoS.
        if (engaged && behavior.chaseTracking == ChaseTracking.Persistent)
        {
            if (HasLineOfSight(toPlayer, dist))
                _lastLineOfSightTime = Time.time;

            bool leashBroken      = dist > behavior.chaseLeashRadius;
            bool sightLostTooLong = Time.time - _lastLineOfSightTime >= behavior.persistentLoseSightTime;

            if (leashBroken || sightLostTooLong)
            {
                LoseTarget();
                return;
            }

            _board.hasTarget = true;
            _board.target    = _playerTransform;
            return;
        }

        if (dist > behavior.detectionRadius)
        {
            LoseTarget();
            return;
        }

        // Field-of-view check
        Vector2 facingDir = _facingRight ? Vector2.right : Vector2.left;
        float   angle     = Vector2.Angle(facingDir, toPlayer);

        if (angle > behavior.fovAngle * 0.5f)
        {
            LoseTarget();
            return;
        }

        if (!HasLineOfSight(toPlayer, dist))
        {
            LoseTarget();
            return;
        }

        _lastLineOfSightTime = Time.time;
        _board.hasTarget = true;
        _board.target    = _playerTransform;
    }

    /// <summary>
    /// Clear line from the enemy to the player, tested against the ground mask.
    /// Always true when the profile doesn't require line of sight.
    /// </summary>
    private bool HasLineOfSight(Vector2 toPlayer, float dist)
    {
        if (!_profile.behavior.requireLineOfSight) return true;
        RaycastHit2D hit = Physics2D.Raycast(rb.position, toPlayer, dist, _profile.movement.groundMask);
        return hit.collider == null;
    }

    private void LoseTarget()
    {
        _board.hasTarget = false;
        _board.target    = null;
    }

    // ── Visuals ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Flips CharacterArt on X each frame to face, in priority order:
    ///   1. the target — while attacking, or during the aggro-alert hold: the
    ///      enemy is stationary but should read as locked onto the player;
    ///   2. otherwise the intended travel direction (mover.FacingX). The mover
    ///      sets that from the active path, so once the aggro hold ends and the
    ///      chase moves, facing returns to "direction of travel" on its own.
    ///
    /// While facing the target it also pushes that direction back into the mover,
    /// so FacingX is already correct the instant the mover resumes.
    ///
    /// Runs from LateUpdate (after the Animator). Knockback/hit-stun bypass the
    /// mover, so facing holds steady through a hit and while simply standing still.
    /// </summary>
    private void UpdateFacing()
    {
        if (_characterArt == null) return;

        float dir;

        bool lockOnTarget = _board.target != null
            && (_stateMachine.ActiveStateName == "Attack" || _board.aggroAlerting);

        if (lockOnTarget)
        {
            dir = Mathf.Sign(_board.target.position.x - rb.position.x);
            _board.mover?.SetFacing(dir);
        }
        else if (_board.mover != null)
        {
            dir = _board.mover.FacingX;
        }
        else
        {
            return; // static enemy, not attacking — nothing to derive facing from
        }

        if (Mathf.Abs(dir) < 0.01f) return;   // no clear direction — hold current facing

        _facingRight = dir > 0f;

        Vector3 scale = _characterArt.localScale;
        scale.x = _artBaseScaleX * (_facingRight ? 1f : -1f);
        _characterArt.localScale = scale;
    }
}
