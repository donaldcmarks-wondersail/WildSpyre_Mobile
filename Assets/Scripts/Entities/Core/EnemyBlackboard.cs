using UnityEngine;

/// <summary>
/// Shared runtime data container passed by reference into every state and system.
/// Populated by CTRL_Enemy on Awake and kept up to date each frame.
/// </summary>
[System.Serializable]
public class EnemyBlackboard
{
    [Header("Identity")]
    public MonoBehaviour owner;             // CTRL_Enemy — used for StartCoroutine calls
    public SO_EnemyProfile profile;
    public bool isStatic;                   // True when MovementCapability == None

    [Header("Physics")]
    public Rigidbody2D rb;
    public Animator anim;
    public SpriteRenderer spriteRend;
    public bool isGrounded;

    [Header("Detection / Target")]
    public Transform target;
    public bool hasTarget;
    public Vector2 lastKnownTargetPos;

    /// <summary>
    /// True once the enemy has played its aggro-alert reaction for the current
    /// engagement. Set when Chase is first entered from a non-combat state; cleared
    /// when the enemy fully disengages (Idle / Patrol). Stops the alert re-firing
    /// on every Chase re-entry from Attack or a hit reaction.
    /// </summary>
    public bool hasAggroed;

    /// <summary>
    /// True only during the aggro-alert hold (the delay after spotting the player,
    /// before chase movement starts). While set, CTRL_Enemy faces the art at the
    /// player instead of at the travel direction.
    /// </summary>
    public bool aggroAlerting;

    [Header("Subsystems")]
    public CTRL_EnemyHealth health;
    public CTRL_AbilityController abilities;
    public IEnemyMover mover;

    [Header("Patrol")]
    public Vector2[] patrolPoints;
    public int currentWaypointIndex;
    public bool patrolForward = true;

    /// <summary>True when a usable patrol route is set (spawn-assigned or authored).</summary>
    public bool HasPatrolRoute => patrolPoints != null && patrolPoints.Length > 0;

    /// <summary>
    /// Distance at which this enemy engages: driven by its abilities' range
    /// (preferred ability, or the longest range of any), falling back to
    /// SO_EnemyBehavior.attackRadius when no ability specifies a range.
    /// </summary>
    public float AttackRange => abilities != null
        ? abilities.AttackRange(profile.behavior.preferredAbilityName, profile.behavior.attackRadius)
        : profile.behavior.attackRadius;

    [Header("Timers")]
    public float attackCooldownTimer;
    public float loseTargetTimer;
}
