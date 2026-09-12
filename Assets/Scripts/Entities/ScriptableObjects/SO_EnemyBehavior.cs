using UnityEngine;

public enum PatrolType { Static, LoopWaypoints, PingPong, RandomWaypoints }

/// <summary>
/// How an enemy keeps track of the player once it has engaged (Chase / Attack).
///   LineOfSight — losing sight of the player drops the live target; the enemy heads
///                 to the last-known position and gives up after <see cref="SO_EnemyBehavior.loseTargetTime"/>.
///   Persistent  — the enemy keeps pathing to the player's real position regardless of
///                 line of sight or facing, until EITHER the player leaves
///                 <see cref="SO_EnemyBehavior.chaseLeashRadius"/> OR line of sight has been
///                 broken continuously for <see cref="SO_EnemyBehavior.persistentLoseSightTime"/>.
///                 On give-up it falls back to the LineOfSight wind-down (last-known position,
///                 then exit after loseTargetTime).
/// Initial acquisition always respects detection radius, FOV and (optionally) line of sight.
/// </summary>
public enum ChaseTracking { LineOfSight, Persistent }

[CreateAssetMenu(fileName = "NewEnemyBehavior", menuName = "WildSpyre/Enemy/Behavior")]
public class SO_EnemyBehavior : ScriptableObject
{
    [Header("Detection")]
    public float detectionRadius = 8f;
    public float attackRadius = 1.5f;
    public bool requireLineOfSight = true;
    [Range(0f, 360f)] public float fovAngle = 180f;

    [Header("Patrol")]
    public PatrolType patrolType = PatrolType.LoopWaypoints;

    [Tooltip("Fallback patrol path for spawned enemies with no PatrolRoute assigned. " +
             "Each point is an offset from the enemy's spawn position, in world units " +
             "(e.g. (-3,0) and (3,0) to pace 3m either side of the spawn point).")]
    public Vector2[] localPatrolOffsets;

    [Header("Chase")]
    [Tooltip("LineOfSight: losing sight of the player ends the chase after Lose Target Time. " +
             "Persistent: keep chasing the player's real position even with no line of sight, " +
             "until they leave Chase Leash Radius OR sight stays broken for Persistent Lose Sight Time.")]
    public ChaseTracking chaseTracking = ChaseTracking.LineOfSight;

    [Tooltip("Persistent tracking only — abandon the chase once the player is farther than " +
             "this from the enemy. Keep it >= Detection Radius, or the enemy acquires and " +
             "immediately drops the target.")]
    public float chaseLeashRadius = 20f;

    [Tooltip("Persistent tracking only — give up the live chase after line of sight to the " +
             "player has been broken continuously for this long. The enemy then heads to the " +
             "last-known position and winds down over Lose Target Time, as in LineOfSight mode. " +
             "Ignored when Require Line Of Sight is off (nothing to lose).")]
    public float persistentLoseSightTime = 7f;

    public float loseTargetTime = 3f;

    [Header("Aggro Alert")]
    [Tooltip("On first spotting the player (Patrol/Idle → Chase), fire an animation trigger and " +
             "hold still — turned toward the player — before the chase movement starts. Does not " +
             "re-fire when Chase is re-entered from Attack or a hit reaction; only on a fresh engage.")]
    public bool useAggroAlert = false;

    [Tooltip("Animator trigger fired the instant the player is spotted. Blank = no trigger, delay only.")]
    public string aggroAlertTrigger = "Aggro";

    [Tooltip("Seconds the enemy holds still, facing the player, after the aggro trigger before it " +
             "begins chasing.")]
    public float aggroAlertDelay = 0.5f;

    [Header("Attack")]
    public float attackCooldown = 1.5f;
    public string preferredAbilityName = "";

    [Tooltip("Fire a telegraph animation trigger before every ability use, with a wind-up delay " +
             "during which the enemy holds position.")]
    public bool useAbilityAlert = false;

    [Tooltip("Animator trigger fired at the start of the attack wind-up. Blank = no trigger, delay only.")]
    public string abilityAlertTrigger = "AbilityAlert";

    [Tooltip("Seconds between the ability alert trigger and the ability actually firing. Adds on top " +
             "of Attack Cooldown for each attack.")]
    public float abilityAlertDelay = 0.4f;
}
